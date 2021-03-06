﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveServer
{
    internal class InteractiveConsole
    {
        public bool IsActive { get; private set; }

        private Server _server = null;
        private Thread _serverThread = null;
        private long _repeatWaitStopRequested = 0;

        public async Task Start()
        {
            IsActive = true;

            Console.Write("Loading Data...");
            await DataService.LoadData();
            Console.WriteLine(" Done!");

            _server = new Server();
            _serverThread = new Thread(new ThreadStart(async () => await _server.Listen()));
            _serverThread.Start();

            Console.WriteLine("\n\n--------------------------");
            Console.WriteLine("Server Interactive Console");
            Console.WriteLine("--------------------------\n\n");

            Console.WriteLine($"LISTENING ON: {_server.EndPoint.Address.ToString()}:{_server.EndPoint.Port}\n\n");

            while (IsActive)
            {
                Console.Write("SERVER :: ");

                var userInput = Console.ReadLine();
                if (userInput.Trim() == "") continue;

                Console.WriteLine();

                var result = CommandHandler.GetResult(userInput);
                if (result == null)
                {
                    ProcessLocalCommand(userInput);
                }
                else
                {
                    if (result.StartsWith("<FORMAT>"))
                    {
                        ShowFormattedMessage(result);
                    }
                    else
                    {
                        Console.WriteLine(result);
                    }
                }
            }
        }

        private void ProcessLocalCommand(string command)
        {
            if (command.ToLower().StartsWith(CommandTypes.REPEAT_WAIT))
            {
                var args = command.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                RepeatWait(args.Skip(1).ToArray());
            }
            else
            {
                switch (command.Trim().ToLower())
                {
                    case CommandTypes.EXIT:
                        Stop();
                        break;
                    case CommandTypes.ENDPOINT:
                        Console.WriteLine($"\nLISTENING ON: {_server.EndPoint.Address.ToString()}:{_server.EndPoint.Port}\n");
                        break;
                    default:
                        ShowInvalidCommand(command.ToString());
                        break;
                }
            }
        }

        private void RepeatWait(string[] arguments)
        {
            if (arguments.Length < 2)
            {
                Console.WriteLine("\nREPEAT-WAIT: INVALID NUMBER OF ARGUMENTS\n");
                return;
            }

            int waitMilli;
            if (!int.TryParse(arguments[0], out waitMilli))
            {
                Console.WriteLine("\nREPEAT-WAIT: INVALID MILLISECOND WAIT-TIME\n");
                return;
            }

            _repeatWaitStopRequested = 0;

            var thread = new Thread(new ThreadStart(() => WaitForEscape()));
            thread.Start();

            Console.WriteLine();

            while (true)
            {
                if (Interlocked.Read(ref _repeatWaitStopRequested) == 1)
                {
                    break;
                }

                Console.WriteLine();
                var result = CommandHandler.GetResult(String.Join(" ", arguments.Skip(1)));

                if (result.StartsWith("<FORMAT>"))
                {
                    ShowFormattedMessage(result);
                }
                else
                {
                    Console.WriteLine(result);
                }

                Thread.Sleep(waitMilli);
            }

            thread.Join();
        }

        private void WaitForEscape()
        {
            while (Console.ReadKey(true).Key != ConsoleKey.Escape) { }

            Interlocked.Exchange(ref _repeatWaitStopRequested, 1);
        }

        private void ShowFormattedMessage(string message)
        {
            var dataSplit = message.Split("\n", StringSplitOptions.RemoveEmptyEntries);

            if (dataSplit.Length < 4)
            {
                Console.WriteLine("INVALID FORMATTED MESSAGE");
                return;
            }

            var format = dataSplit[1];
            var totalWidth =
                format.Split(new char[] { '{', '}' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(f => int.Parse(f.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)[1]))
                .Sum();

            var rowStart = 2;

            if (dataSplit[0].Contains("<COLUMNS>"))
            {
                var columns = dataSplit[2].Split(",");
                rowStart++;

                Console.WriteLine(format, columns);
            }

            Console.WriteLine(new string('-', totalWidth));

            foreach (var row in dataSplit.Skip(rowStart))
            {
                Console.WriteLine(format, row.Split(','));
            }
        }

        private void ShowInvalidCommand(string attemptedCommand)
        {
            Console.WriteLine($"\nINVALID COMMAND: { attemptedCommand }\n");
        }

        public void Stop()
        {
            Console.WriteLine("Stopping Server...");

            if (_server != null) _server.Stop();

            if (!Server.Clients.IsEmpty)
            {
                foreach (var client in Server.Clients.Values)
                {
                    if (client.ProducerController != null)
                        client.ProducerController.StopAllProducers();
                }
            }
            
            if (_serverThread != null) _serverThread.Join();

            IsActive = false;
            Console.WriteLine("Server Stopped!");
        }
    }
}
