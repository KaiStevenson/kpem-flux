using System;
using System.Net;
using System.Net.Sockets;
using network_base;
namespace kpem_flux;
public class ConnectionHandler
{
    private TcpClient? client;
    private List<MessageListener> listeners = new List<MessageListener>();
    public delegate void ListenerCallbackDelegate(Message message);
    private List<MessageListenerCallback> listenerCallbacks = new List<MessageListenerCallback>();
    private byte[]? aesKey;
    //If this is non-null, the user is authenticated with that name
    public string? AuthenticatedUsername { get; private set; }
    public bool Available { get { return (client != null && client.Connected); } }
    public async static Task<ConnectionHandler> CreateAsync(string serverIP, byte[] aesKey)
    {
        var handler = new ConnectionHandler();
        handler.aesKey = aesKey;
        var localEndpoint = new IPEndPoint(IPAddress.Any, 0);
        handler.client = new TcpClient(localEndpoint);
        //Multiple client support is only for testing
        await handler.client.ConnectAsync(serverIP, 9853);
        var messageLoop = Task.Run(handler.MessageLoop);
        return handler;
    }
    public void Disconnect()
    {
        //TODO: Tell the server we're disconnecting, to save CPU cycles.
        Console.WriteLine("Disconnecting handler");
        if (client != null)
        {
            client.Client.Disconnect(true);
        }
    }
    public void SendMessage(Message message, NetworkHelper.EncryptionMode mode, byte[]? rsaKey = null)
    {
        if (Available)
        {
            NetworkHelper.WriteMessageToNetworkStream(client!.GetStream(), message, mode, mode == NetworkHelper.EncryptionMode.AES ? aesKey : rsaKey);
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
            if (Available)
            {
                var incomingStream = client!.GetStream();
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
        for (int i = listeners.Count - 1; i >= 0; i--)
        {
            if (listeners[i].command == message.Command)
            {
                listeners[i].messageSource.SetResult(message);
                listeners.RemoveAt(i);
            }
        }
        foreach (var callback in listenerCallbacks)
        {
            try
            {
                callback.callback.Invoke(message);
            }
            catch
            {
                //The callback no longer exists
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
    public async Task<Message> GetMessageAsync(string command, int timeout = 0)
    {
        var tcs = new TaskCompletionSource<Message>();
        var messageTask = tcs.Task;
        var listener = new MessageListener(command, tcs);
        listeners.Add(listener);
        Message result;
        if (timeout != 0)
        {
            result = await messageTask.WaitAsync(TimeSpan.FromMilliseconds(timeout));
        }
        else
        {
            result = await messageTask;
        }
        if (result != null)
        {
            return result;
        }
        else
        {
            //It's safe to edit the list, since this exception can't have come from an attempt to handle the message
            listeners.Remove(listener);
            throw new TimeoutException("Timed out while waiting for a message with the command " + command);
        }
    }
    public void AddMessageCallback(MessageListenerCallback callback)
    {
        listenerCallbacks.Add(callback);
    }
    public void RemoveMessageCallback(MessageListenerCallback callback)
    {
        listenerCallbacks.Remove(callback);
    }
    private struct MessageListener
    {
        internal readonly string command;
        internal readonly TaskCompletionSource<Message> messageSource;
        public MessageListener(string command, TaskCompletionSource<Message> messageSource)
        {
            this.command = command;
            this.messageSource = messageSource;
        }
    }
    public struct MessageListenerCallback
    {
        internal readonly string command;
        internal readonly ListenerCallbackDelegate callback;
        public MessageListenerCallback(string command, ListenerCallbackDelegate callback)
        {
            this.command = command;
            this.callback = callback;
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