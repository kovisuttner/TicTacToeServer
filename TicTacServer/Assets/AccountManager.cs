using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AccountManager : MonoBehaviour
{
    private List<Account> accounts = new List<Account>();

    void Start()
    {
        accounts = LoadAccountsFromFile();
    }

    private void SaveAccountsToFile()
    {
        string json = JsonUtility.ToJson(new AccountListWrapper { accounts = this.accounts });
        System.IO.File.WriteAllText("accounts.json", json);
    }

    private List<Account> LoadAccountsFromFile()
    {
        if (System.IO.File.Exists("accounts.json"))
        {
            string json = System.IO.File.ReadAllText("accounts.json");
            AccountListWrapper loadedData = JsonUtility.FromJson<AccountListWrapper>(json);
            return loadedData.accounts;
        }
        return new List<Account>();
    }

    [System.Serializable]
    private class AccountListWrapper
    {
        public List<Account> accounts;
    }

    public bool AccountExists(string username)
    {
        return accounts.Exists(a => a.username == username);
    }

    public Account GetAccount(string username)
    {
        return accounts.Find(a => a.username == username);
    }

    public void AddAccount(Account newAccount)
    {
        accounts.Add(newAccount);
        SaveAccountsToFile();
    }

    // Add ValidateLogin method to handle login checks
    public bool ValidateLogin(string username, string password)
    {
        Account account = GetAccount(username);
        if (account != null && account.password == password)
        {
            return true; // Valid login
        }
        return false; // Invalid login
    }
}
