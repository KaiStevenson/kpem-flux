# kpem-flux

### Intro:

KPEM (Kai and Preston Encrypted Messenger) is a simple standard for encrypted messaging. It is designed for maximum interoperability while still maintaining protcol support for a large featureset.
KPEM uses an AES encrypted JSON message format, and does *not* support peer to peer connections.
kpem-flux is my open-source KPEM client, and is bundled with kpem-aberrate, my KPEM server.

### Using the kpem-flux client:

kpem-flux is a terminal application. Upon first running it, you'll be prompted to enter a server IP address. The default server is kpem.aetherdestroyer.net, but you're welcome to connect to a different server or host your own. kpem-flux is compatible with any KPEM server, not just kpem-aberrate.

Once you're connected, you'll be presented with the main menu. From there, you can create an account or sign in to an existing account. Accounts are per server, so if you're on a new server you'll have to create a new account. When you're signed in, you can begin messaging any user on the same server, as long as you know their name.

### Using the kpem-aberrate server:

The server doesn't require much configuration. Make sure that there is a users.db file in the same directory as the server executable. The server uses port 9853: ensure the port is available, and if you intend for users outside of your local network to connect, that it is forwarded.

### Implementing the standard:

[unfinished documentation]

##### NETWORKING:

Network communications between a server and a client are sent in discrete packets. One of these discrete packets is henceforth referred to as a *network message item*. The network message item acts as a wrapper arond the *message data*, which is in the JSON format.

Each network message item comprises three components: a single byte describing the type of encryption used in the message data, two bytes describing the length of the remainder of the network message item (N), and N bytes representing the message data and any other encryption data.

The encryption byte may be 0, indicating no encryption used; 1, indicating AES encryption used; or 2, indicating RSA encryption used.
The length bytes may represent any integer between 0 and 65535. Consider that the actual maximum length of the message data is somewhat shorter than this, especially if it is encrypted. The length bytes are little endian.

A network message item should be read discretely, reading 3 + N bytes and then attempting to parse the message data. If the message data is unencrypted, it can be simply parsed to a JSON, using UTF-8 text encoding. If the message is AES encrypted, the first 16 bytes should be interpreted as the initialization vector, and used to decrypt the remainder of the network message item. It can then be parsed to a JSON using UTF-8 text encoding. In the case of RSA encryption, no special data needs to be retrieved from the message.

AES message data is encrypted with the CBC mode and padded with PKCS7, while RSA message data is padded with Pkcs1.

Once the JSON *message* is retrieved, it can be handled. The message contains a property "command" which is a simple string representing the name of some command, and a property "content" which is a dictionary containing some set of key value pairs.

The following is a list of supported messages, with their command name, key-values, and intended use:

[COMMAND] [ (KEY, VALUETYPE -- {options} ) ] [INFO]

##### SHARED MESSAGES:

[ping] [N/A] [requests the recepient return a (pong) message]

[pong] [N/A] [response to a (ping) message]

##### CLIENT MESSAGES:

[authenticate] [ (user, string), (password, string) ] [attempts to sign in with a given username and password]

[sendchatmessage] [ (target, string), (content, string) ] [sends a chat message to the targeted username. this message is forwarded directly to the receiving client]

##### SERVER MESSAGES:

[authenticationresult] [ (result, string -- {"success", "failure"} ) ] [response to an authenticate command; tells the client whether they are now signed in]
