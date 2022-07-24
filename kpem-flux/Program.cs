using System;
using network_base;
using System.Security.Cryptography;
namespace kpem_flux;
class Program
{
    private ConnectionHandler handler;
    public Program()
    {
        ConsoleHelper.WriteLine("Initializing Flux");
        ConsoleHelper.WriteLine("Generating AES key");
        var aes = Aes.Create();
        var aesKey = aes.Key;
        ConsoleHelper.WriteLine("AES initialized");
        while (true)
        {
            try
            {
                var serverIP = ConsoleHelper.GetInput(prompt: "Server IP");
                //Run this synchronously. We have to wait for a connection before proceeding.
                var handlerTask = ConnectionHandler.CreateAsync(serverIP, aesKey);
                handlerTask.Wait(5000);
                handler = handlerTask.Result;
                ConsoleHelper.WriteLine(String.Format("Connected succesfully to {0}", serverIP));
                ConsoleHelper.WriteLine("Waiting to receive the server's RSA key");
                Message keyMessage = handler.GetMessageAsync("sendrsa", 10000).Result;
                var rsaKey = Convert.FromBase64String(keyMessage.Content["key"]);
                ConsoleHelper.WriteLine("Received RSA key " + keyMessage.Content["key"]);
                ConsoleHelper.WriteLine("Encrypting and sending AES key " + Convert.ToBase64String(aesKey));
                //encrypt AES key with server RSA
                handler.SendMessage(new Message("sendaes", new Message.Parameter("key", Convert.ToBase64String(aesKey))),
                    NetworkHelper.EncryptionMode.RSA, rsaKey);
                break;
            }
            catch
            {

                ConsoleHelper.WriteLine("Connection failed or timed out. Try again");
                Thread.Sleep(1000);
                Console.Clear();
            }
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
        ConsoleHelper.PanDownAndClearAsync(1500).Wait();
        while (true)
        {
            ConsoleHelper.Clear();
            ConsoleHelper.WriteLine("--Flux v0.1 Main Menu--", ConsoleColor.Magenta);
            ConsoleHelper.WriteLine(GetStatus());
            //Define all the layers of the menu so that we can interconnnect them
            MenuLevel mainMenu = new MenuLevel(false, menuName: "Main");
            MenuLevel userMenu = new MenuLevel(true, parentLevel: mainMenu, menuName: "User");
            //Now define the items in each layer
            mainMenu.items = new MenuLevel.MenuItem[] {
                new MenuLevel.MenuItem("Sign in/Create account", userMenu),
                new MenuLevel.MenuItem("Start messaging another user", OpenChat),
                new MenuLevel.MenuItem("Open config file", AuthenticateUser)
            };
            userMenu.items = new MenuLevel.MenuItem[] {
                new MenuLevel.MenuItem("Sign in", AuthenticateUser),
                new MenuLevel.MenuItem("Create account", CreateNewUser, askForConfirmation: true)
            };
            //Now run the menu
            mainMenu.EnterMenu();
        }
    }
    private string GetStatus()
    {
        return "Connection status: " + (handler.Available ? "Connected" : "Not connected") +
            "\nAccount status: " + (handler.AuthenticatedUsername != null ?
            String.Format("Signed in as {0}", handler.AuthenticatedUsername) : "Not signed in");
    }
    private void OpenChat()
    {
        var otherUser = ConsoleHelper.GetInput("User to message");
        handler.SendMessage(new Message("getuserinfo", new Message.Parameter("name", otherUser)), NetworkHelper.EncryptionMode.AES);
        var userInfo = handler.GetMessageAsync("senduserinfo", 5000).Result;
        bool userExists = userInfo.Content["exists"] == "true";
        bool userOnline = userInfo.Content["online"] == "true";
        if (userExists)
        {
            if (!userOnline)
            {
                ConsoleHelper.WriteLine(String.Format("{0} isn't online right now. If the server supports message caching," +
                    " they will see your message the next time they connect.", otherUser), ConsoleColor.Red);
                Thread.Sleep(3000);
                //TODO: Wait for confirmation to continue?
            }
            var room = new ChatRoom(handler, otherUser);
            room.Open();
            //User closed room
        }
        else
        {
            ConsoleHelper.WriteLine(String.Format("The user '{0}' doesn't exist", otherUser));
        }
    }
    private void CreateNewUser()
    {
        var username = ConsoleHelper.GetInput(prompt: "Username");
        var clearPoint = new ConsoleHelper.ConsoleClearPoint();
        string? password = null;
        string? passwordReentry = null;
        while (password == null || password != passwordReentry)
        {
            clearPoint.Back();
            password = ConsoleHelper.GetInput(prompt: "Password", obscurity: ConsoleHelper.ObscurityType.LastCharOnly);
            passwordReentry = ConsoleHelper.GetInput(prompt: "Re-enter Password", obscurity: ConsoleHelper.ObscurityType.LastCharOnly);
            if (password != passwordReentry)
            {
                ConsoleHelper.WriteLine("Passwords don't match", ConsoleColor.Red);
                Thread.Sleep(1000);
            }
        }
        ConsoleHelper.WriteLine("Attempting to create a new account...");
        handler.SendMessage(new Message("createaccount",
            new Message.Parameter("user", username),
            new Message.Parameter("password", password)), NetworkHelper.EncryptionMode.AES);
        ConsoleHelper.WriteLine("Account created!");
        if (ConsoleHelper.GetBinaryChoice(String.Format("Sign in as {0}?", username)))
        {
            var success = handler.TryAuthenticateAsync(username, password).Result;
            if (success)
            {
                ConsoleHelper.WriteLine("Signed in!");
            }
            else
            {
                ConsoleHelper.WriteLine("Authentication failed");
            }
        }
        ConsoleHelper.WaitForKeypressToContinue();
    }
    private void AuthenticateUser()
    {
        var username = ConsoleHelper.GetInput(prompt: "Username");
        var password = ConsoleHelper.GetInput(prompt: "Password", obscurity: ConsoleHelper.ObscurityType.LengthOnly);
        ConsoleHelper.WriteLine("Attempting to authenticate...");
        var success = handler.TryAuthenticateAsync(username, password).Result;
        if (success)
        {
            ConsoleHelper.WriteLine("Signed in!");
        }
        else
        {
            ConsoleHelper.WriteLine("Authentication failed");
        }
        ConsoleHelper.WaitForKeypressToContinue();
    }
    private class ChatRoom
    {
        private ConnectionHandler handler;
        private ConnectionHandler.MessageListenerCallback callbackInstance;
        private ConsoleHelper.AsyncReaderInterruptToken? token;
        private string otherUser;
        public ChatRoom(ConnectionHandler handler, string otherUser)
        {
            this.handler = handler;
            this.otherUser = otherUser;
            this.callbackInstance = new ConnectionHandler.MessageListenerCallback("receivechatmessage", MessageReceived);
        }
        public void Open()
        {
            handler.AddMessageCallback(callbackInstance);
            ConsoleHelper.Clear();
            ConsoleHelper.WriteLine("Press ESC to exit");
            ConsoleHelper.WriteLine(String.Format("--{0}--", otherUser), ConsoleColor.Yellow);
            token = new ConsoleHelper.AsyncReaderInterruptToken();
            while (true)
            {
                var getInputTask = ConsoleHelper.GetInputAsync(token, String.Format("[{0}]", handler.AuthenticatedUsername!));
                try
                {
                    getInputTask.Wait();
                    handler.SendMessage(new Message("sendchatmessage", new Message.Parameter("target", otherUser),
                        new Message.Parameter("content", getInputTask.Result)), NetworkHelper.EncryptionMode.AES);
                }
                //Catch usercancelledoperationexception
                catch
                {
                    getInputTask.Dispose();
                    break;
                }
            }
            //This is executed before exiting the chatroom
            Close();
        }
        private void MessageReceived(Message message)
        {
            var messageText = message.Content["content"];
            var originatingUser = message.Content["originatinguser"];
            token!.InterruptWithContent(String.Format("{0} >> {1}", originatingUser, messageText));
        }
        private void Close()
        {
            handler.RemoveMessageCallback(callbackInstance);
        }
    }
}