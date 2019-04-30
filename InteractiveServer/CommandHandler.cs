using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

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
                    resultMessage = "HELLO " + client.Endpoint.Address + ":" + client.Endpoint.Port;
                    break;
                case CommandTypes.START_PRODUCERS:
                    resultMessage = StartClientProducers(client, splitCommand.Skip(1).ToArray());
                    break;
                case CommandTypes.STOP_PRODUCERS:
                    resultMessage = StopClientProducers(client);
                    break;
                case CommandTypes.PEEK_PRODUCERS:
                    resultMessage = PeekClientProducers(client);
                    break;
                case CommandTypes.GET_WORD:
                    resultMessage = GetClientProducerWord(client);
                    break;
                case CommandTypes.DISCONNECT:
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
            message.Append("{0,30}{1,15}{2,15}\n");
            message.Append("HOST NAME,IP ADDRESS,PORT\n");

            foreach (var client in clients)
            {
                var ipAddress = (client.Socket.RemoteEndPoint as IPEndPoint).Address.ToString();
                var port = (client.Socket.RemoteEndPoint as IPEndPoint).Port;
                var name = Dns.GetHostEntry(ipAddress).HostName;

                message.Append($"{name},{ipAddress},{port}\n");
            }

            return message.ToString();
        }

        private static string StartClientProducers(Client client, string[] arguments)
        {
            if (client.ProducerController != null)
            {
                return "PRODUCERS ALREADY STARTED!";
            }

            int producerCount;
            if (arguments.Length == 0 || !int.TryParse(arguments[0], out producerCount))
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
                bufferSize = 10;
            }

            client.ProducerController = new ProducerController(client, bufferSize);
            client.ProducerController.StartProducers(producerCount);

            return $"{producerCount} PRODUCERS STARTED";
        }

        private static string StopClientProducers(Client client)
        {
            if (client.ProducerController == null)
            {
                return "THERE ARE NO ACTIVE PRODUCERS FOR THIS CLIENT";
            }

            client.ProducerController.StopProducers();
            client.ProducerController = null;

            return "PRODUCERS STOPPED";
        }

        private static string PeekClientProducers(Client client)
        {
            if (client.ProducerController == null)
            {
                return "THERE ARE NO ACTIVE PRODUCERS FOR THIS CLIENT";
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
    }
}
