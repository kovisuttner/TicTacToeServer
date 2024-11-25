using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using System.Text;
using System.Collections.Generic;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver networkDriver;
    private NativeList<NetworkConnection> networkConnections;

    NetworkPipeline reliableAndInOrderPipeline;
    NetworkPipeline nonReliableNotInOrderedPipeline;

    const ushort NetworkPort = 8080;
    const int MaxNumberOfClientConnections = 1000;

    public AccountManager accountManager;

    private Dictionary<string, List<NetworkConnection>> rooms = new Dictionary<string, List<NetworkConnection>>();

    void Start()
    {
        accountManager = FindObjectOfType<AccountManager>();
        networkDriver = NetworkDriver.Create();
        reliableAndInOrderPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage), typeof(ReliableSequencedPipelineStage));
        nonReliableNotInOrderedPipeline = networkDriver.CreatePipeline(typeof(FragmentationPipelineStage));
        NetworkEndpoint endpoint = NetworkEndpoint.AnyIpv4;
        endpoint.Port = NetworkPort;

        int error = networkDriver.Bind(endpoint);
        if (error != 0)
            Debug.Log("Failed to bind to port " + NetworkPort);
        else
            networkDriver.Listen();

        networkConnections = new NativeList<NetworkConnection>(MaxNumberOfClientConnections, Allocator.Persistent);
    }

    void OnDestroy()
    {
        networkDriver.Dispose();
        networkConnections.Dispose();
    }

    void Update()
    {
        networkDriver.ScheduleUpdate().Complete();

        #region Remove Unused Connections
        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (!networkConnections[i].IsCreated)
            {
                networkConnections.RemoveAtSwapBack(i);
                i--;
            }
        }
        #endregion

        #region Accept New Connections
        while (AcceptIncomingConnection())
        {
            Debug.Log("Accepted a client connection");
        }
        #endregion

        #region Manage Network Events
        DataStreamReader streamReader;
        NetworkPipeline pipelineUsedToSendEvent;
        NetworkEvent.Type networkEventType;

        for (int i = 0; i < networkConnections.Length; i++)
        {
            if (!networkConnections[i].IsCreated)
                continue;

            while (PopNetworkEventAndCheckForData(networkConnections[i], out networkEventType, out streamReader, out pipelineUsedToSendEvent))
            {
                if (networkEventType == NetworkEvent.Type.Data)
                {
                    int sizeOfDataBuffer = streamReader.ReadInt();
                    NativeArray<byte> buffer = new NativeArray<byte>(sizeOfDataBuffer, Allocator.Persistent);
                    streamReader.ReadBytes(buffer);
                    byte[] byteBuffer = buffer.ToArray();
                    string msg = Encoding.Unicode.GetString(byteBuffer);
                    ProcessReceivedMsg(msg, networkConnections[i]);
                    buffer.Dispose();
                }
                else if (networkEventType == NetworkEvent.Type.Disconnect)
                {
                    Debug.Log("Client has disconnected from server");
                    networkConnections[i] = default(NetworkConnection);
                }
            }
        }
        #endregion
    }

    private bool AcceptIncomingConnection()
    {
        NetworkConnection connection = networkDriver.Accept();
        if (!connection.IsCreated)
            return false;

        networkConnections.Add(connection);
        return true;
    }

    private bool PopNetworkEventAndCheckForData(NetworkConnection networkConnection, out NetworkEvent.Type networkEventType, out DataStreamReader streamReader, out NetworkPipeline pipelineUsedToSendEvent)
    {
        networkEventType = networkConnection.PopEvent(networkDriver, out streamReader, out pipelineUsedToSendEvent);

        if (networkEventType == NetworkEvent.Type.Empty)
            return false;
        return true;
    }

    private void ProcessReceivedMsg(string message, NetworkConnection connection)
    {
        string[] parts = message.Split('|');

        if (parts.Length < 2)
        {
            Debug.LogError("Invalid message format: Not enough parts.");
            return;
        }

        string messageType = parts[0];

        switch (messageType)
        {
            case "LOGIN":
                if (parts.Length == 3) 
                {
                    HandleLogin(parts[1], parts[2], connection);
                }
                else
                {
                    Debug.LogError("Invalid LOGIN message format.");
                }
                break;

            case "CREATE_ACCOUNT":
                if (parts.Length == 3) 
                {
                    HandleCreateAccount(parts[1], parts[2], connection);
                }
                else
                {
                    Debug.LogError("Invalid CREATE_ACCOUNT message format.");
                }
                break;

            case "JOIN_OR_CREATE_ROOM":
                if (parts.Length == 2) 
                {
                    HandleJoinOrCreateRoom(parts[1], connection);
                }
                else
                {
                    Debug.LogError("Invalid JOIN_OR_CREATE_ROOM message format.");
                }
                break;

            case "LEAVE_ROOM":
                HandleLeaveRoom(connection);
                break;

            case "MOVE":
                if (parts.Length == 3 && int.TryParse(parts[2], out int index))
                {
                    string playerSymbol = parts[1]; 
                    HandlePlayerMove(playerSymbol, index, connection);
                }
                else
                {
                    Debug.LogError("Invalid MOVE message format.");
                }
                break;

            default:
                Debug.LogError("Unknown request type: " + messageType);
                break;
        }
    }


    private void HandlePlayerMove(string playerSymbol, int index, NetworkConnection connection)
    {
        Debug.Log($"Player {playerSymbol} made a move at index {index}");

        foreach (var room in rooms)
        {
            if (room.Value.Contains(connection))
            {
                foreach (var participant in room.Value)
                {
                    string moveMessage = $"MOVE|{playerSymbol}|{index}";
                    SendMessageToClient(moveMessage, participant);
                }
            }
        }
    }



    private void HandleLogin(string username, string password, NetworkConnection connection)
    {
        Account account = accountManager.GetAccount(username);
        if (account != null && account.password == password)
        {
            SendMessageToClient("LOGIN_SUCCESS|" + username, connection);
        }
        else
        {
            SendMessageToClient("LOGIN_FAILED|" + username, connection);
        }
    }

    private void HandleCreateAccount(string username, string password, NetworkConnection connection)
    {
        if (accountManager.AccountExists(username))
        {
            SendMessageToClient("ACCOUNT_CREATION_FAILED", connection);
        }
        else
        {
            Account newAccount = new Account(username, password);
            accountManager.AddAccount(newAccount);
            SendMessageToClient("ACCOUNT_CREATION_SUCCESS", connection);
        }
    }

    private void HandleJoinOrCreateRoom(string roomName, NetworkConnection connection)
    {
    if (!rooms.ContainsKey(roomName))
    {
        rooms[roomName] = new List<NetworkConnection> { connection };
        SendMessageToClient("ROOM_CREATED|" + roomName, connection);
        Debug.Log($"Room {roomName} created and client joined.");
    }
    else
    {
        if (rooms[roomName].Count < 2)
        {
            rooms[roomName].Add(connection);

            if (rooms[roomName].Count == 2)
            {
                foreach (var conn in rooms[roomName])
                {
                    SendMessageToClient("START_GAME", conn);
                }
            }
        }
        else
        {
            // Room is full, add as an observer
            SendMessageToClient("ROOM_FULL_OBSERVER", connection);
            Debug.Log($"Room {roomName} is full. Client added as observer.");
        }
    }
    }


    private void HandleLeaveRoom(NetworkConnection connection)
    {
        foreach (var room in rooms)
        {
            if (room.Value.Contains(connection))
            {
                room.Value.Remove(connection);
                SendMessageToClient("LEFT_ROOM|" + room.Key, connection);
                Debug.Log($"Client left room {room.Key}");
                return;
            }
        }
        SendMessageToClient("NOT_IN_ROOM", connection);
    }

    private void SendMessageToClient(string msg, NetworkConnection networkConnection)
    {
        byte[] msgAsByteArray = Encoding.Unicode.GetBytes(msg);
        NativeArray<byte> buffer = new NativeArray<byte>(msgAsByteArray, Allocator.Persistent);

        int result = networkDriver.BeginSend(reliableAndInOrderPipeline, networkConnection, out var writer);

        if (result != 0)
        {
            Debug.LogError("Failed to begin send.");
            return;
        }
        writer.WriteInt(buffer.Length);
        writer.WriteBytes(buffer);

        networkDriver.EndSend(writer);

        buffer.Dispose();
    }
}
