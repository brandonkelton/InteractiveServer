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
        public static ConcurrentDictionary<Guid, Client> Clients = new ConcurrentDictionary<Guid, Client>(10, 100);

        public IPEndPoint EndPoint;
        public bool IsActive { get; private set; }
        public bool CanServerListen { get; private set; }

        private ManualResetEvent _listenerResetEvent = new ManualResetEvent(false);
        private Socket _socket;

        public Server()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var localIP = host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
            EndPoint = new IPEndPoint(localIP, IPEndPoint.MaxPort);
        }

        public void Listen()
        {
            IsActive = true;

            _socket = new Socket(EndPoint.AddressFamily, SocketType.Stream, ProtocolType.IP);

            _socket.Bind(EndPoint);
            _socket.Listen(100);

            CanServerListen = true;

            while (CanServerListen)
            {
                _listenerResetEvent.Reset();

                _socket.BeginAccept(new AsyncCallback(AcceptConnection), _socket);

                _listenerResetEvent.WaitOne();
            }

            _listenerResetEvent.Set();

            IsActive = false;
        }

        private void AcceptConnection(IAsyncResult result)
        {
            _listenerResetEvent.Set();

            if (!CanServerListen || !IsActive)
            {
                return;
            }

            var socket = ((Socket)result.AsyncState).EndAccept(result);

            Client client = null;

            try
            {
                client = CreateClient();
            }
            catch (InvalidOperationException e)
            {
                var messageByteString = Encoding.Unicode.GetBytes("Could not accept client. Please try again later.");
                var messageBuffer = new ReadOnlyMemory<byte>(messageByteString);
                socket.Send(messageBuffer.ToArray(), SocketFlags.None);
                socket.Close();
                socket.Disconnect(true);
                socket.Dispose();
                return;
            }

            client.Endpoint = socket.RemoteEndPoint as IPEndPoint;
            client.Socket = socket;

            client.Socket.BeginReceive(client.Buffer, 0, Client.BufferSize, SocketFlags.None,
                new AsyncCallback(ReadClient), client);
        }

        private void ReadClient(IAsyncResult result)
        {
            var client = (Client)result.AsyncState;
            int bytesRead;

            try
            {
                bytesRead = client.Socket.EndReceive(result);
            }
            catch (Exception)
            {
                DestroyClient(client.Id);
                return;
            }

            if (bytesRead > 0)
            {
                var commandPart = Encoding.Unicode.GetString(client.Buffer, 0, bytesRead);

                if (!String.IsNullOrEmpty(commandPart))
                {
                    client.Command.Append(commandPart);
                }
                
                client.Buffer = new byte[Client.BufferSize];

                if (client.Command.ToString().EndsWith("<STOP>"))
                {
                    int stopIndex = client.Command.ToString().IndexOf("<STOP>");
                    if (stopIndex > -1)
                    {
                        client.Command.Remove(stopIndex, 6);
                        ProcessCommand(client);
                    }
                    client.Command.Clear();
                }
                else
                {
                    client.Socket.BeginReceive(client.Buffer, 0, Client.BufferSize, SocketFlags.None,
                        new AsyncCallback(ReadClient), client);
                }
            }
        }

        private void ProcessCommand(Client client)
        {
            if (String.IsNullOrEmpty(client.Command.ToString())) {
                return;
            }

            var result = CommandHandler.GetResult(client);
            client.Message = result == null ? "Invalid Command: " + client.Command.ToString() : result;

            client.Command.Clear();

            if (client.Message == "DISCONNECTED")
            {
                return;
            }

            var returnBytes = Encoding.Unicode.GetBytes($"{client.Message}<STOP>");
            var returnBuffer = new ReadOnlyMemory<byte>(returnBytes);
            var sendResult = client.Socket.BeginSend(returnBuffer.ToArray(), 0, returnBuffer.Length, SocketFlags.None,
                new AsyncCallback(SendClient), client);
        }

        private void SendClient(IAsyncResult result)
        {
            var client = (Client)result.AsyncState;

            int bytesSent = client.Socket.EndSend(result);

            try
            {
                client.Socket.BeginReceive(client.Buffer, 0, Client.BufferSize, SocketFlags.None,
                    new AsyncCallback(ReadClient), client);
            }
            catch (Exception e)
            {
                DestroyClient(client.Id);
            }
        }

        public void Stop()
        {
            CanServerListen = false;
            _socket.Close();

            foreach (var client in Clients.Values)
            {
                DestroyClient(client.Id);
            }

            IsActive = false;
        }

        private Client CreateClient()
        {
            var client = new Client
            {
                Id = Guid.NewGuid(),
                IsActive = true
            };

            var maxAttempts = 60;
            var attempts = 0;
            while (!Clients.TryAdd(client.Id, client))
            {
                if (attempts >= maxAttempts)
                {
                    throw new InvalidOperationException("Error attempting to add client to ConcurrentBag");
                }

                attempts++;
                Thread.Sleep(1000);
            }

            return client;
        }

        public static void DestroyClient(Guid id)
        {
            Client removedClient;
            if (!Clients.TryRemove(id, out removedClient))
            {
                Thread.Sleep(100);
            }

            if (removedClient.ProducerController != null)
            {
                removedClient.ProducerController.StopAllProducers();
            }

            removedClient.Socket.Close();
        }
    }
}
