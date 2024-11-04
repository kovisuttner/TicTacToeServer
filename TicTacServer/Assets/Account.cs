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
