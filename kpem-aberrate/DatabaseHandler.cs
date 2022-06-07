using System;
using Microsoft.Data.Sqlite;
namespace kpem_aberrate;
public class DatabaseHandler
{
    public SqliteConnection connection;
    public DatabaseHandler(string databasePath)
    {
        connection = new SqliteConnection(String.Format("Data Source={0}", databasePath));
        connection.Open();
    }
    public void AddUser(string username, byte[] hash, byte[] salt)
    {
        //TODO: Check to make sure that user doesn't already exist
        //This should also be done at a higher level to provide feedback to the client
        Console.WriteLine("Attempting to add a new user record");
        var command = new SqliteCommand();
        command.Connection = connection;
        command.CommandText = @"
            INSERT INTO users (name, hash, salt)
            VALUES ($username, $hash, $salt)";
        command.Parameters.AddWithValue("$username", username);
        command.Parameters.AddWithValue("$hash", Convert.ToBase64String(hash));
        command.Parameters.AddWithValue("$salt", Convert.ToBase64String(salt));
        command.ExecuteNonQuery();
    }
    public UserInfo? GetUserInfo(string username)
    {
        var command = new SqliteCommand();
        command.Connection = connection;
        command.CommandText = @"SELECT hash, salt
            FROM users
            WHERE name = $username";
        command.Parameters.AddWithValue("$username", username);
        try
        {
            var reader = command.ExecuteReader();
            reader.Read();
            var hash = Convert.FromBase64String(reader.GetString(0));
            var salt = Convert.FromBase64String(reader.GetString(1));
            return new UserInfo(username, hash, salt);
        }
        catch
        {
            return null;
        }
    }
    public class UserInfo
    {
        public string username;
        public byte[] hash;
        public byte[] salt;
        public UserInfo(string username, byte[] hash, byte[] salt)
        {
            this.username = username;
            this.hash = hash;
            this.salt = salt;
        }
    }
}
