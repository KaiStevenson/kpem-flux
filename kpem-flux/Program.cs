using System;
using network_base;
using System.Security.Cryptography;
namespace kpem_flux;
class Program
{
    private byte[] aesKey;
    private ConnectionHandler handler;
    public Program()
    {
        ConsoleHelper.WriteLine("Initializing Flux");
        ConsoleHelper.WriteLine("Generating AES key");
        var aes = Aes.Create();
        aesKey = aes.Key;
        ConsoleHelper.WriteLine("AES initialized");
        var serverIP = ConsoleHelper.GetInput(prompt: "Server IP");
        //Run this synchronously. We have to wait for a connection before proceeding.
        handler = ConnectAsync(serverIP).Result;
        if (handler.Available)
        {
            ConsoleHelper.WriteLine(String.Format("Connected succesfully to {0} on ports {1}, {2}", serverIP, "9852", "9853"));
            ConsoleHelper.WriteLine("Waiting to receive the server's RSA key");
            Message keyMessage = handler.GetMessageAsync("sendrsa", 10000).Result;
            var key = Convert.FromBase64String(keyMessage.Content["key"]);
            ConsoleHelper.WriteLine("Received RSA key " + keyMessage.Content["key"]);
            ConsoleHelper.WriteLine("Encrypting and sending AES key " + Convert.ToBase64String(aesKey));
            //encrypt AES key with server RSA
            handler.SendMessage(new Message("sendaes", new Message.Parameter("key", Convert.ToBase64String(aesKey))),
                NetworkHelper.EncryptionMode.RSA, key);
        }
        else //If the handler isn't available here, it probably timed out.
        {
            ConsoleHelper.WriteLine("Connection failed or timed out. Restart the application and try again");
        }
        ConsoleHelper.WriteLine("Exiting constructor");
        ConsoleHelper.WriteLine("Press any key");
        Console.ReadKey(true);
    }
    static void Main(string[] args)
    {
        var p = new Program();
        p.Run();
    }
    public void Run()
    {
        ConsoleHelper.PanDownAndClearAsync(1250).Wait();
        ConsoleHelper.WriteLine("--Flux v0.1--", ConsoleColor.Magenta);
        var toCreateNewAccount = ConsoleHelper.GetBinaryChoice("Create new account?");
        if (toCreateNewAccount)
        {
            ConsoleHelper.WriteLine("Create an account");
            CreateNewUser();
        }
        ConsoleHelper.WriteLine("Sign in");
        AuthenticateUser();
        //End of program reached
        handler.Disconnect();
    }
    private void CreateNewUser()
    {
        var username = ConsoleHelper.GetInput(prompt: "Username");
        var password = ConsoleHelper.GetInput(prompt: "Password", obscurity: ConsoleHelper.ObscurityType.LengthOnly);
        ConsoleHelper.WriteLine("Attempting to create a new account...");
        //TODO: This should be AES encrypted
        handler.SendMessage(new Message("createaccount",
            new Message.Parameter("user", username),
            new Message.Parameter("password", password)), NetworkHelper.EncryptionMode.AES, aesKey);
        ConsoleHelper.WriteLine("Account created!");
    }
    private void AuthenticateUser()
    {
        while (true)
        {
            var username = ConsoleHelper.GetInput(prompt: "Username");
            var password = ConsoleHelper.GetInput(prompt: "Password", obscurity: ConsoleHelper.ObscurityType.LengthOnly);
            ConsoleHelper.WriteLine("Attempting to authenticate...");
            //TODO: This should be AES encrypted
            handler.SendMessage(new Message("authenticate",
                new Message.Parameter("user", username),
                new Message.Parameter("password", password)), NetworkHelper.EncryptionMode.AES, aesKey);
            ConsoleHelper.WriteLine("Authenticated succesfully!");
            break;
        }
    }
    public async Task<ConnectionHandler> ConnectAsync(string serverIP)
    {
        int timeout = 10000;
        ConsoleHelper.WriteLine("Attempting server connection, timing out in " + timeout / 1000 + " seconds...");
        var handler = new ConnectionHandler(serverIP, timeout);
        while (!handler.Available && timeout > 0)
        {
            await Task.Delay(1000);
            timeout--;
        }
        return handler;
    }
}