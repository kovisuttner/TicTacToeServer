using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

[System.Serializable]
public class Account
{
    public string username;
    public string password;

    public Account(string username, string password)
    {
        this.username = username;
        this.password = password;
    }
}

public class ServerManager : MonoBehaviour
{
    private List<Account> accounts = new List<Account>();
    private TcpListener listener;
    private TcpClient client;
    private NetworkStream stream;

    public int port = 12345;

    private void Start()
    {
        StartServer();
    }

    private void StartServer()
    {
        listener = new TcpListener(IPAddress.Any, port);
        listener.Start();
        Debug.Log($"Server started on port {port}. Waiting for clients...");
        ListenForClients();
    }

    private async void ListenForClients()
    {
        while (true)
        {
            client = await listener.AcceptTcpClientAsync();
            Debug.Log("Client connected!");
            stream = client.GetStream();
            ReceiveMessagesFromClient();
        }
    }

    private async void ReceiveMessagesFromClient()
    {
        byte[] buffer = new byte[1024];
        int bytesRead;

        while (true)
        {
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;

            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Debug.Log($"Received message from client: {message}");
            ReceiveMessageFromClient(message);
        }

        Debug.Log("Client disconnected.");
        client.Close();
    }

    public void HandleLoginRequest(string username, string password)
    {
        foreach (Account account in accounts)
        {
            if (account.username == username && account.password == password)
            {
                Debug.Log($"Login successful for {username}");
                SendResponseToClient("LOGIN_SUCCESS");
                return;
            }
        }
        Debug.Log($"Login failed for {username}");
        SendResponseToClient("LOGIN_FAILED");
    }

    public void HandleCreateAccountRequest(string username, string password)
    {
        foreach (Account account in accounts)
        {
            if (account.username == username)
            {
                Debug.Log($"Account creation failed: Username {username} already exists.");
                SendResponseToClient("ACCOUNT_CREATION_FAILED");
                return;
            }
        }

        Account newAccount = new Account(username, password);
        accounts.Add(newAccount);
        Debug.Log($"Account created successfully for {username}");
        SendResponseToClient("ACCOUNT_CREATION_SUCCESS");
    }

    private void SendResponseToClient(string message)
    {
        if (client != null && client.Connected)
        {
            byte[] messageBuffer = Encoding.UTF8.GetBytes(message);
            stream.Write(messageBuffer, 0, messageBuffer.Length);
            Debug.Log($"Sending response to client: {message}");
        }
        else
        {
            Debug.LogWarning("No client connected to send response.");
        }
    }

    public void ReceiveMessageFromClient(string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 1) return;

        string command = parts[0];
        if (command == "LOGIN")
        {
            if (parts.Length != 3) return;
            string username = parts[1];
            string password = parts[2];
            HandleLoginRequest(username, password);
        }
        else if (command == "CREATE_ACCOUNT")
        {
            if (parts.Length != 3) return;
            string username = parts[1];
            string password = parts[2];
            HandleCreateAccountRequest(username, password);
        }
    }

    private void OnApplicationQuit()
    {
        listener.Stop();
        if (client != null)
        {
            client.Close();
        }
    }
}
