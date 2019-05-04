using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveServer
{
    public class Server
    {
        public static ConcurrentDictionary<Guid, Client> Clients = new ConcurrentDictionary<Guid, Client>();
        public IPEndPoint EndPoint;
        public bool IsActive { get; private set; }
        public bool CanServerListen { get; private set; }

        private static ConcurrentDictionary<Guid, Thread> ClientThreads = new ConcurrentDictionary<Guid, Thread>();
        private Socket _socket;

        public Server()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var localIP = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            EndPoint = new IPEndPoint(localIP, IPEndPoint.MaxPort);
        }

        public async Task Listen()
        {
            IsActive = true;

            _socket = new Socket(EndPoint.AddressFamily, SocketType.Stream, ProtocolType.IP);

            _socket.Bind(EndPoint);
            _socket.Listen(100);

            CanServerListen = true;

            while (CanServerListen)
            {
                Socket socket = null;
                try
                {
                    socket = await _socket.AcceptAsync();
                }
                catch (Exception) { }

                if (socket != null)
                {
                    var client = CreateClient(socket);

                    if (client != null)
                    {
                        var thread = new Thread(new ThreadStart(async () => await ExecuteClient(client)));
                        while (!ClientThreads.TryAdd(client.Id, thread))
                            Thread.Sleep(10);
                        thread.Start();
                    }
                }
            }

            IsActive = false;
        }

        private async Task ExecuteClient(Client client)
        {
            client.Activate();

            while (client.IsActive)
            {
                if (client.CancellationToken.IsCancellationRequested)
                {
                    client.Deactivate();
                }

                try
                {
                    var bytesReceived = await client.Socket.ReceiveAsync(client.Buffer, SocketFlags.None, client.CancellationToken);
                    if (bytesReceived > 0) await ProcessClientMessage(client, bytesReceived);
                }
                catch (Exception)
                {
                    client.Deactivate(); // Swallow the exception for now
                }
            }
            
            DestroyClient(client.Id);
        }

        private async Task ProcessClientMessage(Client client, int bytesReceived)
        {
            var commandPart = Encoding.Unicode.GetString(client.Buffer, 0, bytesReceived);

            if (!String.IsNullOrEmpty(commandPart))
            {
                client.Command.Append(commandPart);
            }

            client.Buffer = new byte[Client.BufferSize];

            if (client.Command.ToString().EndsWith("<STOP>"))
            {
                int stopIndex = client.Command.ToString().IndexOf("<STOP>");
                if (stopIndex > -1 && client.Command.Length > 0)
                {
                    client.Command.Remove(stopIndex, 6);
                    await ProcessCommand(client);
                }
                client.Command.Clear();
            }
        }

        private async Task ProcessCommand(Client client)
        {
            if (String.IsNullOrEmpty(client.Command.ToString())) {
                return;
            }

            var result = CommandHandler.GetResult(client);
            client.Message = result == null ? "Invalid Command: " + client.Command.ToString() : result;

            client.Command.Clear();

            var returnBytes = Encoding.Unicode.GetBytes($"{client.Message}<STOP>");
            var bytesSent = await client.Socket.SendAsync(returnBytes, SocketFlags.None, client.CancellationToken);
        }

        public void Stop()
        {
            CanServerListen = false;
            _socket.Close(5);
            _socket.Dispose();

            foreach (var client in Clients.Values)
            {
                DestroyClient(client.Id);
            }

            IsActive = false;
        }

        private Client CreateClient(Socket socket)
        {
            var client = new Client
            {
                Id = Guid.NewGuid(),
                Socket = socket
            };

            var maxAttempts = 100;
            var attempts = 0;
            while (!Clients.TryAdd(client.Id, client) &&
                attempts++ < maxAttempts)
            {
                Thread.Sleep(10);
            }

            return attempts >= maxAttempts ? null : client;
        }

        public static void DestroyClient(Guid id)
        {
            int maxAttempts = 100;

            if (Clients.ContainsKey(id))
            {
                Client removedClient;
                int removeClientAttempts = 0;
                while (!Clients.TryRemove(id, out removedClient) &&
                    removeClientAttempts++ < maxAttempts)
                {
                    Thread.Sleep(10);
                }

                if (removedClient != null)
                {
                    if (removedClient.ProducerController != null)
                    {
                        removedClient.ProducerController.StopAllProducers();
                    }

                    if (removedClient.Socket != null)
                    {
                        removedClient.Socket.Close(5);
                        removedClient.Socket.Dispose();
                    }
                }
            }
            
            if (ClientThreads.ContainsKey(id))
            {
                Thread thread;
                int removeThreadAttempts = 0;
                while (!ClientThreads.TryRemove(id, out thread) &&
                    removeThreadAttempts < maxAttempts)
                {
                    Thread.Sleep(10);
                }

                if (thread != null) thread.Join();
            }
        }
    }
}
