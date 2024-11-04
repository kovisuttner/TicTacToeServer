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


    private async void ReceiveMessagesFromClient(int clientId)
    {
        byte[] buffer = new byte[1024];
        int bytesRead;

        while (true)
        {
            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;

            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            Debug.Log($"Received message from client {clientId}: {message}");
            ReceiveMessageFromClient(clientId, message);
        }

        Debug.Log($"Client {clientId} disconnected.");
        if (client != null)
        {
            client.Close();
        }
    }

    public void HandleLoginRequest(int clientId, string username, string password)
    {
        Account existingAccount = accounts.Find(account => account.username == username);
        if (existingAccount != null)
        {
            if (existingAccount.password == password)
            {
                Debug.Log($"Login successful for {username}");
                AssociateClientWithAccount(clientId, existingAccount);
                SendResponseToClient("LOGIN_SUCCESS");
                return;
            }
            else
            {
                Debug.Log($"Login failed for {username}: Incorrect password.");
                SendResponseToClient("LOGIN_FAILED");
                return;
            }
        }
        else
        {
            Debug.Log($"Login failed for {username}: Account does not exist.");
            SendResponseToClient("ACCOUNT_NOT_FOUND");
            return;
        }
    }

    public void HandleCreateAccountRequest(int clientId, string username, string password)
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
        SaveAccounts();
        Debug.Log($"Account created successfully for {username}");
        SendResponseToClient("ACCOUNT_CREATION_SUCCESS");
    }


    private void AssociateClientWithAccount(int clientId, Account account)
    {
        if (!connectedClients.ContainsKey(clientId))
        {
            connectedClients[clientId] = account;
            Debug.Log($"Client {clientId} associated with account {account.username}");
        }
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

    public void ReceiveMessageFromClient(int clientId, string message)
    {
        string[] parts = message.Split('|');
        if (parts.Length < 1) return;

        string command = parts[0];
        if (command == "LOGIN")
        {
            if (parts.Length != 3) return;
            string username = parts[1];
            string password = parts[2];
            HandleLoginRequest(clientId, username, password);
        }
        else if (command == "CREATE_ACCOUNT")
        {
            if (parts.Length != 3) return;
            string username = parts[1];
            string password = parts[2];
            HandleCreateAccountRequest(clientId, username, password);
        }
    }

    private void SaveAccounts()
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
