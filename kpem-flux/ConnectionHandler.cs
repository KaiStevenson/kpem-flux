using System;
using System.Net;
using System.Net.Sockets;
using network_base;
namespace kpem_flux;
public class ConnectionHandler
{
    private TcpClient? outgoingClient;
    private TcpClient? incomingClient;
    private List<MessageListener> listeners = new List<MessageListener>();
    private byte[]? aesKey;
    //If this is non-null, the user is authenticated with that name
    public string? AuthenticatedUsername { get; private set; }
    public bool Available
    {
        get
        {
            return (outgoingClient != null && outgoingClient.Connected) &&
                (incomingClient != null && incomingClient.Connected);
        }
    }
    public ConnectionHandler()
    {
        
    }
    public async static Task<ConnectionHandler> CreateAsync(string serverIP, byte[] aesKey)
    {
        var handler = new ConnectionHandler();
        handler.aesKey = aesKey;
        handler.outgoingClient = new TcpClient();
        await handler.outgoingClient.ConnectAsync(serverIP, 9853);
        var listener = new TcpListener(IPAddress.Any, 9852);
        listener.Start();
        handler.incomingClient = await listener.AcceptTcpClientAsync();
        var messageLoop = handler.MessageLoop();
        return handler;
    }
    public void Disconnect()
    {
        //TODO: Tell the server we're disconnecting, to save CPU cycles.
        Console.WriteLine("Disconnecting handler");
        if (incomingClient != null)
        {
            incomingClient.Client.Disconnect(true);
        }
        if (outgoingClient != null)
        {
            outgoingClient.Client.Disconnect(true);
        }
    }
    public void SendMessage(Message message, NetworkHelper.EncryptionMode mode, byte[]? key = null)
    {
        if (Available)
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            NetworkHelper.WriteMessageToNetworkStream(outgoingClient.GetStream(), message, mode, key);
#pragma warning restore CS8602 // Dereference of a possibly null reference.
        }
        else
        {
            throw new Exception("Handler not available");
        }
    }
    private async Task MessageLoop()
    {
        int interval = 50;
        while (true)
        {
            if (incomingClient != null)
            {
                var incomingStream = incomingClient.GetStream();
                if (incomingStream.DataAvailable)
                {
                    var message = await NetworkHelper.GetMessageFromNetworkStreamAsync(incomingStream, aesKey: aesKey);
                    HandleMessage(message);
                }
                else
                {
                    await Task.Delay(interval);
                }
            }
        }
    }
    private void HandleMessage(Message message)
    {
        foreach (var listener in listeners)
        {
            if (listener.command == message.Command)
            {
                listener.message = message;
            }
        }
        //Switch case for consistently handled messages
        switch (message.Command)
        {
            case "ping":
                SendMessage(new Message("pong"), NetworkHelper.EncryptionMode.None);
                break;
        }
    }
    //This method returns control to calling thread until a message is received with a given command
    //Or until a timeout is reached
    public async Task<Message> GetMessageAsync(string command, int timeout)
    {
        var listener = new MessageListener(command);
        listeners.Add(listener);
        int timeElapsed = 0;
        while (listener.message == null && timeElapsed < timeout)
        {
            await Task.Delay(50);
            timeElapsed += 50;
        }
        listeners.Remove(listener);
        if (listener.message != null)
        {
            return listener.message;
        }
        else
        {
            throw new TimeoutException("Timed out while waiting for a message with the command " + command);
        }
    }
    private class MessageListener
    {
        internal readonly string command;
        internal Message? message;
        internal MessageListener(string command)
        {
            this.command = command;
        }
    }
    public async Task<bool> TryAuthenticateAsync(string username, string password)
    {
        SendMessage(new Message("authenticate",
            new Message.Parameter("user", username),
            new Message.Parameter("password", password)), NetworkHelper.EncryptionMode.AES, aesKey);
        try
        {
            var result = await GetMessageAsync("authenticationresult", 5000);
            if (result.Content["result"] == "success")
            {
                AuthenticatedUsername = username;
                return true;
            }
            else
            {
                return false;
            }
        }
        catch (TimeoutException)
        {
            return false;
        }
    }
}