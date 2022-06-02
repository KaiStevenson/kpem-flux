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
        var handlerTask = ConnectionHandler.CreateAsync(serverIP, aesKey);
        handlerTask.Wait(5000);
        handler = handlerTask.Result;
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
        ConsoleHelper.WaitForKeypressToContinue();
    }
    static void Main(string[] args)
    {
        var p = new Program();
        p.Run();
    }
    public void Run()
    {
        UI();
    }
    private void UI()
    {
        while (true)
        {
            ConsoleHelper.PanDownAndClearAsync(50).Wait();
            ConsoleHelper.WriteLine("--Flux v0.1 Main Menu--", ConsoleColor.Magenta);
            ConsoleHelper.WriteLine(GetStatus());
            //Define all the layers of the menu so that we can interconnnect them
            MenuLevel mainMenu = new MenuLevel(false, menuName: "Main");
            MenuLevel userMenu = new MenuLevel(true, parentLevel: mainMenu, menuName: "User");
            //Now define the items in each layer
            mainMenu.items = new MenuLevel.MenuItem[] {
                new MenuLevel.MenuItem("Sign in/Create account", userMenu),
                new MenuLevel.MenuItem("Start messaging another user", AuthenticateUser),
                new MenuLevel.MenuItem("Open config file", AuthenticateUser)
            };
            userMenu.items = new MenuLevel.MenuItem[] {
                new MenuLevel.MenuItem("Sign in", AuthenticateUser),
                new MenuLevel.MenuItem("Create account", CreateNewUser, askForConfirmation: true)
            };
            //Now run the menu
            mainMenu.EnterMenu();
            ConsoleHelper.WaitForKeypressToContinue();
        }
        //var n = ConsoleHelper.GetNumericChoice("Make a choice", "Log in", "Create an account", "Open config file");
        //switch (n)
        //{
        //    case 1:
        //        AuthenticateUser();
        //        ConsoleHelper.WaitForKeypressToContinue();
        //        break;
        //    case 2:
        //        if (ConsoleHelper.GetBinaryChoice("Are you sure?"))
        //        {
        //            CreateNewUser();
        //        }
        //        ConsoleHelper.WaitForKeypressToContinue();
        //        break;
        //}
    }
    private string GetStatus()
    {
        return "Connection status: " + (handler.Available ? "Connected" : "Not connected") +
            "\nAccount status: " + (handler.AuthenticatedUsername != null ? "Signed in" : "Not signed in");
    }
    private void CreateNewUser()
    {
        var username = ConsoleHelper.GetInput(prompt: "Username");
        var password = ConsoleHelper.GetInput(prompt: "Password", obscurity: ConsoleHelper.ObscurityType.LengthOnly);
        ConsoleHelper.WriteLine("Attempting to create a new account...");
        handler.SendMessage(new Message("createaccount",
            new Message.Parameter("user", username),
            new Message.Parameter("password", password)), NetworkHelper.EncryptionMode.AES, aesKey);
        ConsoleHelper.WriteLine("Account created!");
    }
    private void AuthenticateUser()
    {
        var username = ConsoleHelper.GetInput(prompt: "Username");
        var password = ConsoleHelper.GetInput(prompt: "Password", obscurity: ConsoleHelper.ObscurityType.LengthOnly);
        ConsoleHelper.WriteLine("Attempting to authenticate...");
        var success = handler.TryAuthenticateAsync(username, password).Result;
        if (success)
        {
            ConsoleHelper.WriteLine("Authenticated succesfully!");
        }
        else
        {
            ConsoleHelper.WriteLine("Authentication failed");
        }
    }
}