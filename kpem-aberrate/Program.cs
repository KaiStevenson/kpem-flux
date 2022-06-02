using System;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using network_base;
namespace kpem_aberrate;
class Program
{
    private List<User> users = new List<User>();
    private DatabaseHandler databaseHandler;
    private RSA rsa;
    private byte[] rsaPrivateKey;
    private byte[] rsaPublicKey;

    public Program()
    {
        Console.WriteLine("Connecting to database");
        //TODO: Implement config file with database location
        databaseHandler = new DatabaseHandler("/Users/kaistevenson/VS_Projects/kpem-flux/kpem-aberrate/users.db");
        Console.WriteLine("Initializing RSA");
        rsa = RSA.Create(4096);
        rsaPrivateKey = rsa.ExportRSAPrivateKey();
        rsaPublicKey = rsa.ExportRSAPublicKey();
    }
    static void Main(string[] args)
    {
        var p = new Program();
        p.Run();
    }
    public void Run()
    {
        Console.WriteLine("Aberrate v0.1");
        var listener = new TcpListener(IPAddress.Any, 9853);
        listener.Start();
        while (true)
        {
            var newUserCheck = CheckForNewUsers(listener);
            var userHandler = Task.Run(HandleUsers);
            newUserCheck.Wait();
            userHandler.Wait();
            Thread.Sleep(1000);
        }
    }
    private async Task CheckForNewUsers(TcpListener listener)
    {
        if (listener.Pending())
        {
            try
            {
                Console.WriteLine("Attempting to handle incoming connection request");
                var client = await listener.AcceptTcpClientAsync();
                Console.WriteLine("Incoming connection accepted");
                _ = HandleConnection(client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
    private async Task HandleConnection(TcpClient incomingClient)
    {
        var remoteEndpoint = (IPEndPoint?)incomingClient.Client.RemoteEndPoint;
        if (remoteEndpoint != null)
        {
            var outgoingClient = new TcpClient();
            Console.WriteLine("Attempting to connect outgoing client");
            await outgoingClient.ConnectAsync(remoteEndpoint.Address, 9852);
            var user = new User(outgoingClient, incomingClient);
            users.Add(user);
            Console.WriteLine("Outgoing client connected, user added");
            //Delay to make sure the user is listening before we send our key
            //TODO: Consider only sending the key when the user requests it
            if (rsaPublicKey != null)
            {
                await Task.Delay(500);
                Console.WriteLine("Sending RSA key to client");
                SendMessage(user, new Message("sendrsa", new Message.Parameter("key", Convert.ToBase64String(rsaPublicKey))),
                    NetworkHelper.EncryptionMode.None);
            }
            else
            {
                throw new Exception("RSA key not extant");
            }
        }
    }

    private void SendMessage(User user, Message message, NetworkHelper.EncryptionMode mode, byte[]? key = null)
    {
        try
        {
            NetworkHelper.WriteMessageToNetworkStream(user.outgoingClient.GetStream(), message, mode, key);
        }
        catch
        {
            Console.WriteLine("Messaging user failed");
            if (!user.incomingClient.Connected || !user.outgoingClient.Connected)
            {
                Console.WriteLine("Removed user as they were already disconnected");
                RemoveUser(user);
            }
        }
    }

    //This method is potentially CPU bound
    private void HandleUsers()
    {
        var removeList = new List<User>();
        foreach (var user in users)
        {
            var incomingStream = user.incomingClient.GetStream();
            if (incomingStream.DataAvailable)
            {
                user.timeOfLastMessage = DateTime.UtcNow;
                //Reset the inactivity warning flag, so that the user can be warned again
                user.inactivityWarned = false;
                _ = HandleMessages(incomingStream, user);
            }
            else
            {
                //Then, check for inactivity
                var timeSinceLastMessage = DateTime.UtcNow - user.timeOfLastMessage;
                if (timeSinceLastMessage.TotalSeconds > 120 && !user.inactivityWarned)
                {
                    SendMessage(user, new Message("ping"), NetworkHelper.EncryptionMode.None);
                    user.inactivityWarned = true;
                }
                else if (timeSinceLastMessage.TotalSeconds > 135)
                {
                    Console.WriteLine("Removed user due to inactivity");
                    removeList.Add(user);
                }
            }
        }
        foreach (var user in removeList)
        {
            RemoveUser(user);
        }
    }
    private void RemoveUser(User user)
    {
        user.incomingClient.Client.Disconnect(true);
        user.outgoingClient.Client.Disconnect(true);
        users.Remove(user);
    }
    //This method will handle every incoming message from a network stream. The user parameter is derived from the socket, but is probably not secure
    private async Task HandleMessages(NetworkStream ns, User user)
    {
        while (ns.DataAvailable)
        {
            var message = await NetworkHelper.GetMessageFromNetworkStreamAsync(ns, aesKey: user.AESKey, rsaKey: rsaPrivateKey);
            Console.WriteLine(String.Format("New message received with command {0} from user {1}", message.Command, user.username));
            switch (message.Command)
            {
                case "ping":
                    SendMessage(user, new Message("pong"), NetworkHelper.EncryptionMode.None);
                    break;
                case "sendaes":
                    Console.WriteLine("Received AES key " + message.Content["key"]);
                    user.AESKey = Convert.FromBase64String(message.Content["key"]);
                    break;
                case "authenticate":
                    Console.WriteLine(String.Format("User attempting to authenticate with username {0} and password {1}",
                        message.Content["user"], message.Content["password"]));
                    var authenticated = AuthenticationHelper.TryAuthenticate(message.Content["user"], message.Content["password"], databaseHandler);
                    if (authenticated)
                    {
                        user.username = message.Content["user"];
                        Console.WriteLine("User authenticated successfully");
                    }
                    else
                    {
                        Console.WriteLine("Authentication failed");
                    }
                    SendMessage(user, new Message("authenticationresult",
                        new Message.Parameter("result", authenticated ? "success" : "failure")), NetworkHelper.EncryptionMode.AES, user.AESKey);
                    break;
                case "createaccount":
                    Console.WriteLine(String.Format("Creating a new account for user {0} with password {1}",
                        message.Content["user"], message.Content["password"]));
                    AuthenticationHelper.CreateAccount(message.Content["user"], message.Content["password"], databaseHandler);
                    break;
            }
        }
    }
}
public class User
{
    public TcpClient outgoingClient;
    public TcpClient incomingClient;
    //The number of milliseconds since we've received a message from this user
    //At some point, the user will be asked to confirm that they're still connected
    //And will eventually be kicked
    public DateTime timeOfLastMessage;
    //This is set to true to prevent the user from receiving multiple inactivity warnings
    public bool inactivityWarned;
    //If the username field is non-null, the user has been authenticated
    //TODO: During the authentication process, the user should communicate some means of signing their messages
    //Otherwise, other devices on the same network could potentially impersonate the user
    public string? username;
    //It's the client's responsibility to pass the server an AES key
    //The server will make its public RSA key available
    public byte[]? AESKey;
    public User(TcpClient outgoingClient, TcpClient incomingClient)
    {
        this.outgoingClient = outgoingClient;
        this.incomingClient = incomingClient;
        timeOfLastMessage = DateTime.UtcNow;
    }
}