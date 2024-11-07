using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.IO;
using UnityEngine;
using System;

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

[System.Serializable]
class AccountListWrapper
{
    public List<Account> accounts;
}

public class ServerManager : MonoBehaviour
{
    private List<Account> accounts = new List<Account>();
    private TcpListener listener;
    private Dictionary<int, Account> connectedClients = new Dictionary<int, Account>();
    private TcpClient client;
    private NetworkStream stream;

    public int port = 12345;
    private string accountsFilePath = "accounts.json";

    private void Start()
    {
        LoadAccounts();
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
        try
        {
            while (true)
            {
                if (listener == null)
                {
                    Debug.LogWarning("Listener is null or has been stopped.");
                    break;
                }

                client = await listener.AcceptTcpClientAsync();
                Debug.Log("Client connected!");

                if (client != null)
                {
                    stream = client.GetStream();
                    ReceiveMessagesFromClient(client.Client.RemoteEndPoint.GetHashCode());
                }
            }
        }
        catch (ObjectDisposedException)
        {
            Debug.Log("Listener has been stopped and disposed.");
        }
    }

    private void ReceiveMessagesFromClient(int clientId)
    {
        try
        {
            byte[] buffer = new byte[1024];
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);

            if (!string.IsNullOrEmpty(message))
            {
                ProcessMessageFromClient(clientId, message);
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Error receiving message: {ex.Message}");
        }
    }

    private void ProcessMessageFromClient(int clientId, string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 2)
        {
            Debug.Log("Invalid message format.");
            return;
        }

        switch (parts[0])
        {
            case "LOGIN":
                HandleLogin(clientId, parts[1], parts[2]); 
                break;
            case "CREATE_ACCOUNT":
                HandleCreateAccount(clientId, parts[1], parts[2]); 
                break;
            default:
                Debug.Log("Unknown request: " + parts[0]);
                break;
        }
    }

    private void HandleLogin(int clientId, string username, string password)
    {
        Account account = accounts.Find(a => a.username == username);
        if (account != null && account.password == password)
        {
            SendMessageToClient("LOGIN_SUCCESS|" + username, clientId);  
        }
        else if (account == null)
        {
            SendMessageToClient("ACCOUNT_NOT_FOUND", clientId);
        }
        else
        {
            SendMessageToClient("LOGIN_FAILED", clientId);
        }
    }

    private void HandleCreateAccount(int clientId, string username, string password)
    {
        if (accounts.Exists(a => a.username == username))
        {
            SendMessageToClient("ACCOUNT_CREATION_FAILED", clientId);
        }
        else
        {
            Account newAccount = new Account(username, password);
            accounts.Add(newAccount);
            SaveAccountsToFile(); 
            SendMessageToClient("ACCOUNT_CREATION_SUCCESS", clientId);
        }
    }

    private void SendMessageToClient(string message, int clientId)
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

    private void SaveAccountsToFile()
    {
        string json = JsonUtility.ToJson(new AccountListWrapper { accounts = this.accounts });
        File.WriteAllText(accountsFilePath, json);
        Debug.Log("Accounts saved.");
    }

    private void LoadAccounts()
    {
        if (File.Exists(accountsFilePath))
        {
            string json = File.ReadAllText(accountsFilePath);
            AccountListWrapper loadedData = JsonUtility.FromJson<AccountListWrapper>(json);
            this.accounts = loadedData.accounts ?? new List<Account>();
            Debug.Log("Accounts loaded.");
        }
        else
        {
            Debug.Log("No account data found. Starting with an empty account list.");
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
