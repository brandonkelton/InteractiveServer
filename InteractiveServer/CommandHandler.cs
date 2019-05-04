using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace InteractiveServer
{
    internal static class CommandHandler
    {
        public static string GetResult(Client client)
        {
            var splitCommand = client.Command.ToString().Split(" ", StringSplitOptions.RemoveEmptyEntries);
            string resultMessage;

            switch (splitCommand[0].ToLower().Trim())
            {
                case CommandTypes.HELLO:
                    resultMessage = "HELLO " + (client.Socket.RemoteEndPoint as IPEndPoint).Address + ":" + (client.Socket.RemoteEndPoint as IPEndPoint).Port;
                    break;
                case CommandTypes.SET_BUFFER:
                    resultMessage = SetProducerBuffer(client, splitCommand.Skip(1).ToArray());
                    break;
                case CommandTypes.START_PRODUCERS:
                    resultMessage = StartClientProducers(client, splitCommand.Skip(1).ToArray());
                    break;
                case CommandTypes.CLIENT_ID:
                    resultMessage = client.Id.ToString();
                    break;
                case CommandTypes.LINK_CLIENT:
                    resultMessage = LinkClient(client, splitCommand.Skip(1).ToArray());
                    break;
                case CommandTypes.LINK_TO_CLIENT:
                    resultMessage = LinkToClient(client, splitCommand.Skip(1).ToArray());
                    break;
                case CommandTypes.UNLINK_FROM_CLIENT:
                    resultMessage = UnLinkFromClient(client, splitCommand.Skip(1).ToArray());
                    break;
                case CommandTypes.BUFFER:
                    resultMessage = GetProducerBuffer(client);
                    break;
                case CommandTypes.TRANSFER_STATUS:
                    resultMessage = GetTransferStatus(client);
                    break;
                case CommandTypes.STOP_PRODUCERS:
                    resultMessage = StopClientProducers(client, splitCommand.Skip(1).ToArray());
                    break;
                case CommandTypes.STOP_ALL_PRODUCERS:
                    resultMessage = StopAllClientProducers(client);
                    break;
                case CommandTypes.PEEK_BUFFER:
                    resultMessage = PeekBuffer(client);
                    break;
                case CommandTypes.GET_WORD:
                    resultMessage = GetClientProducerWord(client);
                    break;
                case CommandTypes.STATUS:
                    resultMessage = GetStatus(client);
                    break;
                case CommandTypes.DISCONNECT:
                    client.Deactivate();
                    Server.DestroyClient(client.Id);
                    resultMessage = "DISCONNECTED";
                    break;
                default:
                    resultMessage = GetResult(client.Command.ToString());
                    break;
            }

            return resultMessage;
        }

        public static string GetResult(string command)
        {
            var splitCommand = command.Split(" ", StringSplitOptions.RemoveEmptyEntries);
            string resultMessage;

            switch (splitCommand[0].ToLower().Trim())
            {
                case CommandTypes.ECHO:
                    resultMessage = String.Join(" ", splitCommand.Skip(1));
                    break;
                case CommandTypes.STATUS_OF:
                case CommandTypes.STATUS_FOR:
                    resultMessage = GetStatusOf(splitCommand.Skip(1).ToArray());
                    break;
                case CommandTypes.CLIENTS:
                    resultMessage = ShowClients();
                    break;
                default:
                    resultMessage = null;
                    break;
            }

            return resultMessage;
        }

        private static string ShowClients()
        {
            var clients = Server.Clients.Values.Where(c => c.Socket.Connected);

            if (clients.Count() == 0)
            {
                return "THERE ARE NO CLIENTS CONNECTED";
            }

            var message = new StringBuilder();

            message.Append("<FORMAT><COLUMNS>\n");
            message.Append("{0,30}{1,15}{2,7}{3,8}{4,8}{5,12}{6,12}\n");
            message.Append("HOST NAME,IP ADDRESS,PORT,ID,LINKED,PRODUCERS,CONSUMERS\n");

            foreach (var client in clients)
            {
                var id = client.Id.ToString().Substring(0, 6);
                var ipAddress = (client.Socket.RemoteEndPoint as IPEndPoint).Address.ToString();
                var port = (client.Socket.RemoteEndPoint as IPEndPoint).Port;
                var name = Dns.GetHostEntry(ipAddress).HostName;
                var linkToClient = client.LinkToClientId != Guid.Empty ? client.LinkToClientId.ToString().Substring(0, 6) : Server.Clients.Values.FirstOrDefault(c => c.Id == client.Id)?.LinkedClientIds.Count() > 0 ? "MASTER" : "";
                var producerCount = client.LinkToClientId == Guid.Empty && client.ProducerController != null ? client.ProducerController.ProducerCount.ToString() : "";
                var consumerCount = client.LinkToClientId == Guid.Empty && Server.Clients.Values.FirstOrDefault(c => c.Id == client.Id)?.LinkedClientIds.Count() > 0 ? Server.Clients.Values.FirstOrDefault(c => c.Id == client.Id)?.LinkedClientIds.Count().ToString() ?? "" : "";

                message.Append($"{name},{ipAddress},{port},{id},{linkToClient},{producerCount},{consumerCount}\n");
            }

            return message.ToString();
        }

        private static string GetStatusOf(string[] arguments)
        {
            if (arguments.Length == 0)
            {
                return "INVALID STATUS ARGUMENTS";
            }

            Guid id;
            Client client;

            if (!Guid.TryParse(arguments[0], out id) ||
                (client = Server.Clients.Values.FirstOrDefault(c => c.Id == id)) == null)
            {
                return "INVALID CLIENT ID";
            }

            return GetStatus(client);
        }

        private static string GetStatus(Client client)
        {
            var linkedClients = Server.Clients.Values.Where(c => c.LinkToClientId == client.Id);
            var producers = client.ProducerController == null ? 0 : client.ProducerController.ProducerCount;
            var consumers = Server.Clients.Values.FirstOrDefault(c => c.Id == client.Id)?.LinkedClientIds.Count() ?? 0;
            var buffer = client.ProducerController == null ? 0 : client.ProducerController.BufferLevelRunningAverage;
            var transferStatus = client.ProducerController == null ? "" : client.ProducerController.TransferStatus;
            var complete = client.ProducerController == null ? 0 : client.ProducerController.PercentComplete;

            var message = new StringBuilder();

            message.Append("<FORMAT><COLUMNS>\n");
            message.Append("{0,20}{1,12}{2,12}{3,12}{4,12}\n");
            message.Append("TRANSFER STATUS,COMPLETE,BUFFER,PRODUCERS,CONSUMERS\n");

            message.Append($"{transferStatus},{complete},{buffer},{producers},{consumers}\n");

            return message.ToString();
        }

        private static string SetProducerBuffer(Client client, string[] arguments)
        {
            int bufferSize;
            if (arguments.Length == 0 || !int.TryParse(arguments[0], out bufferSize))
            {
                return "INVALID PRODUCER BUFFER ARGUMENTS";
            }

            if (client.ProducerController != null)
            {
                return "CAN NOT SET BUFFER ONCE PRODUCERS ARE STARTED";
            }

            if (client.ProducerController == null)
            {
                client.ProducerController = new ProducerController(bufferSize);
            }

            return $"PRODUCER BUFFER = {bufferSize}";
        }

        private static string StartClientProducers(Client client, string[] arguments)
        {
            int producerCount = 0;
            if (arguments.Length > 0 && !int.TryParse(arguments[0], out producerCount))
            {
                return "INVALID PRODUCER ARGUMENTS";
            }

            int bufferSize;
            if (arguments.Length == 2 && !int.TryParse(arguments[1], out bufferSize))
            {
                return "INVALID PRODUCER BUFFER SIZE";
            }
            else
            {
                bufferSize = 20;
            }

            if (client.ProducerController == null)
            {
                client.ProducerController = new ProducerController(bufferSize);
            }

            if (producerCount == 0)
            {
                client.ProducerController.StartSelfAdjustingProducers();
            }
            else
            {
                client.ProducerController.StartProducers(producerCount);
            }

            if (producerCount == 0)
            {
                return "SELF-ADJUSTING PRODUCERS STARTED";
            }

            return $"{producerCount} PRODUCERS STARTED | {client.ProducerController.ProducerCount} PRODUCERS RUNNING";
        }

        private static string StopClientProducers(Client client, string[] arguments)
        {
            if (client.ProducerController == null)
            {
                return "THERE ARE NO ACTIVE PRODUCERS FOR THIS CLIENT";
            }

            int producerCount;
            if (arguments.Length == 0 || !int.TryParse(arguments[0], out producerCount))
            {
                return "INVALID PRODUCER ARGUMENTS";
            }

            client.ProducerController.StopProducers(producerCount);

            return $"{producerCount} PRODUCERS STOPPED | {client.ProducerController.ProducerCount} PRODUCERS RUNNING";
        }

        private static string StopAllClientProducers(Client client)
        {
            if (client.ProducerController == null)
            {
                return "THERE ARE NO ACTIVE PRODUCERS FOR THIS CLIENT";
            }

            client.ProducerController.StopAllProducers();
            client.ProducerController = null;

            return "PRODUCERS STOPPED";
        }

        private static string PeekBuffer(Client client)
        {
            if (client.ProducerController == null)
            {
                return "PRODUCTION HAS NOT BEEN INITIALIZED";
            }

            var message = String.Join("\n", client.ProducerController.PeekBuffer());
            return message;
        }

        private static string GetClientProducerWord(Client client)
        {
            if (client.ProducerController == null)
            {
                return "THERE ARE NO ACTIVE PRODUCERS FOR THIS CLIENT";
            }

            var word = client.ProducerController.TakeFromBuffer();
            var message = JsonConvert.SerializeObject(word);
            return message;
        }

        // Set another client's ProducerController to the passed client's ProducerController
        // (Make another client a "slave")
        private static string LinkClient(Client client, string[] arguments)
        {
            Guid linkClientId;
            Client linkClient;

            if (arguments.Length == 0 
                || !Guid.TryParse(arguments[0], out linkClientId)
                || !Server.Clients.TryGetValue(linkClientId, out linkClient))
            {
                return "INVALID CLIENT ID OR CLIENT NOT FOUND";
            }

            linkClient.ProducerController = client.ProducerController;
            client.LinkedClientIds.Add(linkClientId);

            return "CLIENT LINKED";
        }

        // Set passed client's ProducerController to another client's ProducerController
        // (Make this client a "slave")
        private static string LinkToClient(Client client, string[] arguments)
        {
            Guid linkToClientId;
            Client linkToClient;

            if (arguments.Length == 0 
                || !Guid.TryParse(arguments[0], out linkToClientId)
                || (linkToClient = Server.Clients.Values.FirstOrDefault(c => c.Id == linkToClientId)) == null)
            {
                return "INVALID CLIENT ID OR CLIENT NOT FOUND";
            }

            client.LinkToClientId = linkToClient.Id;
            client.ProducerController = linkToClient.ProducerController;
            linkToClient.LinkedClientIds.Add(client.Id);

            return "LINKED TO CLIENT";
        }

        private static string UnLinkFromClient(Client client, string[] arguments)
        {
            Guid linkToClientId;
            Client linkToClient;

            if (arguments.Length == 0
                || !Guid.TryParse(arguments[0], out linkToClientId)
                || (linkToClient = Server.Clients.Values.FirstOrDefault(c => c.Id == linkToClientId)) == null)
            {
                return "INVALID CLIENT ID OR CLIENT NOT FOUND";
            }

            client.LinkToClientId = Guid.Empty;
            client.ProducerController = null;
            linkToClient.LinkedClientIds.Remove(client.Id);

            return "UNLINKED FROM CLIENT";
        }

        private static string GetProducerBuffer(Client client)
        {
            if (client.ProducerController == null)
            {
                return "THERE ARE NO ACTIVE PRODUCERS FOR THIS CLIENT";
            }

            return $"{(int)client.ProducerController.BufferLevelRunningAverage}%";
        }

        private static string GetTransferStatus(Client client)
        {
            if (client.ProducerController == null)
            {
                return "THERE ARE NO ACTIVE PRODUCERS FOR THIS CLIENT";
            }

            return client.ProducerController.TransferStatus;
        }
    }
}
