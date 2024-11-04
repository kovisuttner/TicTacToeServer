using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AccountManager : MonoBehaviour
{
    private List<Account> accounts = new List<Account>();

    public void SaveAccountsToFile()
    {
        string json = JsonUtility.ToJson(new AccountListWrapper { accounts = accounts });
        System.IO.File.WriteAllText("accounts.json", json);
    }

    public List<Account> LoadAccountsFromFile()
    {
        if (System.IO.File.Exists("accounts.json"))
        {
            string json = System.IO.File.ReadAllText("accounts.json");
            return JsonUtility.FromJson<AccountListWrapper>(json).accounts;
        }
        return new List<Account>();
    }

    [System.Serializable]
    private class AccountListWrapper
    {
        public List<Account> accounts;
    }
}
