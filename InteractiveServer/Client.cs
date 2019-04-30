using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace InteractiveServer
{
    public class Client
    {
        public static int BufferSize { get; set; } = 1024;

        public Guid Id { get; set; }
        public IPEndPoint Endpoint { get; set; }
        public Socket Socket { get; set; }
        
        public byte[] Buffer { get; set; } = new byte[BufferSize];
        public StringBuilder Command { get; set; } = new StringBuilder();

        public bool IsActive { get; set; }
        public string Message { get; set; }

        public ProducerController ProducerController = null;
    }
}
