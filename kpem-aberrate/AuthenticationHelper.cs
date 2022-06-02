using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
namespace kpem_aberrate;
public static class AuthenticationHelper
{
    //This method should add a new user to the database with the given username and password
    public static void CreateAccount(string username, string password, DatabaseHandler handler)
    {
        var salt = GenerateSalt(32);
        var hash = Hash(Encoding.UTF8.GetBytes(password), salt);
        Console.WriteLine(Convert.ToBase64String(hash));
        handler.AddUser(username, hash, salt);
    }
    public static bool TryAuthenticate(string username, string password, DatabaseHandler handler)
    {
        //TODO: Check to make sure the user exists before doing anything cryptographic
        try
        {
            var userInfo = handler.GetUserInfo(username);
            var salt = userInfo.salt;
            var hashAttempt = Hash(Encoding.UTF8.GetBytes(password), salt);
            Console.WriteLine(Convert.ToBase64String(hashAttempt));
            Console.WriteLine(Convert.ToBase64String(userInfo.hash));
            return Enumerable.SequenceEqual(hashAttempt, userInfo.hash);
        }
        catch (SqliteException)
        {
            Console.WriteLine("SQL error during authentication");
            return false;
        }
    }
    public static byte[] GenerateSalt(int length)
    {
        return RandomNumberGenerator.GetBytes(length);
    }
    public static byte[] Hash(byte[] password, byte[] salt)
    {
        //Add the salt to the password and hash it
        return SHA256.HashData(password.Concat(salt).ToArray());
    }
}