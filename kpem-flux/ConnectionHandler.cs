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
    public bool Available
    {
        get
        {
            return (outgoingClient != null && outgoingClient.Connected) &&
                (incomingClient != null && incomingClient.Connected);
        }
    }
    public ConnectionHandler(string serverIP, int timeout)
    {
        InitializeHandlerAsync(serverIP, timeout);
    }
    private async void InitializeHandlerAsync(string serverIP, int timeout)
    {
        outgoingClient = new TcpClient();
        await outgoingClient.ConnectAsync(serverIP, 9853);
        var listener = new TcpListener(IPAddress.Any, 9852);
        listener.Start();
        incomingClient = await listener.AcceptTcpClientAsync();
        var messageLoop = MessageLoop();
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
        int interval = 500;
        while (true)
        {
            if (incomingClient != null)
            {
                var incomingStream = incomingClient.GetStream();
                if (incomingStream.DataAvailable)
                {
                    var message = await NetworkHelper.GetMessageFromNetworkStreamAsync(incomingStream);
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
    public async Task<Message> GetMessageAsync(string command, int timeout, bool verbose = false)
    {
        if (verbose)
        {
            ConsoleHelper.WriteLine(String.Format("Waiting for a message with the command {0}." +
                " Timing out in {1} seconds", command, timeout / 1000));
        }
        var listener = new MessageListener(command);
        listeners.Add(listener);
        int timeElapsed = 0;
        while (listener.message == null && timeElapsed < timeout)
        {
            await Task.Delay(500);
            timeElapsed += 500;
        }
        listeners.Remove(listener);
        if (listener.message != null)
        {
            if (verbose)
            {
                ConsoleHelper.WriteLine(String.Format("Message received after {0} seconds", timeElapsed / 1000f));
            }
            return listener.message;
        }
        else
        {
            throw new Exception("Timed out while waiting for a message with the command " + command);
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
}