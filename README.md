# kpem-flux

### Intro:

KPEM (Kai and Preston Encrypted Messenger) is a simple standard for encrypted messaging. It is designed for maximum interoperability while still maintaining protcol support for a large featureset.
KPEM uses an AES encrypted JSON message format, and does *not* support peer to peer connections.
kpem-flux is my open-source kpem client, and is bundled with kpem-aberrate, my KPEM server.

### Using the kpem-flux client:

kpem-flux is a terminal application. Upon first running it, you'll be prompted to enter a server IP address. The default server is kpem.aetherdestroyer.net, but you're welcome to connect to a different server or host your own. kpem-flux is compatible with any KPEM server, not just kpem-aberrate.

Once you're connected, you'll be presented with the main menu. From there, you can create an account or sign in to an existing account. Accounts are per server, so if you're on a new server you'll have to create a new account. When you're signed in, you can begin messaging any user on the same server, as long as you know their name.

### Using the kpem-aberrate server:

The server doesn't require much configuration. Make sure that there is a users.db file in the same directory as the server executable. The server uses port 9853: ensure the port is available, and if you intend for users outside of your local network to connect, that it is forwarded.

### Implementing the standard:

[unfinished documentation]
