using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
namespace network_base;
public static class NetworkHelper
{
    public static async Task<Message> GetMessageFromNetworkStreamAsync(NetworkStream ns, byte[]? aesKey = null, byte[]? rsaKey = null)
    {
        //Get the encryption type byte
        byte[] encryptionByte = new byte[1];
        ns.Read(encryptionByte, 0, 1);
        //Get the length bytes
        var lengthBytes = new byte[2];
        ns.Read(lengthBytes, 0, 2);
        if (BitConverter.IsLittleEndian)
        {
            lengthBytes.Reverse();
        }
        var length = BitConverter.ToInt16(lengthBytes);
        var bytes = new byte[length];
        int bytesRead = 0;
        //If we get some data but not a whole message, allow 5 seconds to receive the rest before throwing an exception
        ns.ReadTimeout = 5000;
        while (bytesRead < length)
        {
            bytesRead += await ns.ReadAsync(bytes, 0, length);
        }
        //Decrypt the message, if needed
        Console.WriteLine("Message received with encryption type " + encryptionByte[0]);
        if (encryptionByte[0] == 0)
        {
            return new Message(bytes);
        }
        if (encryptionByte[0] == 1)
        {
            if (aesKey == null)
            {
                throw new Exception("Can't read AES encrypted message without AES key");
            }
            //AES
            var aes = Aes.Create();
            aes.Key = aesKey;
            //The first 16 bytes are the IV, since the block size is 128
            var iv = bytes.Take(16).ToArray();
            //Now create an array without those bytes
            var messageBytes = bytes.Skip(16).ToArray();
            //Decrypt and return the message
            var decryptedMessageBytes = aes.DecryptCbc(messageBytes, iv, PaddingMode.PKCS7);
            return new Message(decryptedMessageBytes);
        }
        else if (encryptionByte[0] == 2)
        {
            if(rsaKey == null)
            {
                throw new Exception("Can't read RSA encrypted message without RSA private key");
            }
            //RSA
            var rsa = RSA.Create();
            rsa.ImportRSAPrivateKey(rsaKey, out _);
            var decryptedMessageBytes = rsa.Decrypt(bytes, RSAEncryptionPadding.Pkcs1);
            return new Message(decryptedMessageBytes);
        }
        else
        {
            throw new Exception("Malformed message header");
        }
    }
    public static void WriteMessageToNetworkStream(NetworkStream ns, Message message, EncryptionMode mode, byte[]? key = null)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General);
        var json = JsonSerializer.Serialize(message, options);
        var bytes = Encoding.UTF8.GetBytes(json);

        if(mode == EncryptionMode.None)
        {
            WriteMessageBytesToNetworkStream(ns, bytes, mode);
        }
        else if(mode == EncryptionMode.AES)
        {
            if(key == null)
            {
                throw new Exception("Cannot AES encrypt message without AES key");
            }
            var aes = Aes.Create();
            aes.Key = key;
            var iv = RandomNumberGenerator.GetBytes(16);
            byte[] encryptedMessageBytes = aes.EncryptCbc(bytes, iv, PaddingMode.PKCS7);
            WriteMessageBytesToNetworkStream(ns, iv.Concat(encryptedMessageBytes).ToArray(), mode);
        }
        else if (mode == EncryptionMode.RSA)
        {
            if (key == null)
            {
                throw new Exception("Cannot RSA encrypt message without RSA public key");
            }
            var rsa = RSA.Create();
            rsa.ImportRSAPublicKey(key, out _);
            byte[] encryptedMessageBytes = rsa.Encrypt(bytes, RSAEncryptionPadding.Pkcs1);
            WriteMessageBytesToNetworkStream(ns, encryptedMessageBytes, EncryptionMode.RSA);
        }
    }
    private static void WriteMessageBytesToNetworkStream(NetworkStream ns, byte[] bytes, EncryptionMode mode)
    {
        short length = (short)bytes.Length;
        var lengthBytes = BitConverter.GetBytes(length);
        if (BitConverter.IsLittleEndian)
        {
            lengthBytes.Reverse();
        }
        byte[] encryptionByte = new byte[1];
        encryptionByte[0] = (byte)mode;
        ns.Write(encryptionByte);
        ns.Write(lengthBytes);
        ns.Write(bytes);
    }
    public enum EncryptionMode
    {
        None,
        AES,
        RSA
    }
}
public class Message
{
    public string Command { get; set; }
    public Dictionary<string, string> Content { get; set; }

    public Message()
    {
        this.Command = "";
        this.Content = new Dictionary<string, string>();
    }
    public Message(string command, Dictionary<string, string> parameters)
    {
        this.Command = command;
        this.Content = parameters;
    }
    public Message(string command, params Parameter[] parameters)
    {
        this.Command = command;
        var contentDictionary = new Dictionary<string, string>();
        foreach (var parameter in parameters)
        {
            contentDictionary.Add(parameter.key, parameter.value);
        }
        this.Content = contentDictionary;
    }
    public Message(byte[] bytes)
    {
        var json = Encoding.UTF8.GetString(bytes);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General);
        var m = JsonSerializer.Deserialize<Message>(json, options);
        if (m != null)
        {
            this.Command = m.Command;
            this.Content = m.Content;
        }
        else
        {
            throw new Exception("Deserialization error");
        }
    }
    public struct Parameter
    {
        public string key;
        public string value;
        public Parameter(string key, string value)
        {
            this.key = key;
            this.value = value;
        }
    }
}