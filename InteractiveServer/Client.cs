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

        private long _isActive = 0;
        public bool IsActive => Interlocked.Read(ref _isActive) == 1;

        public CancellationToken CancellationToken { get; set; } = new CancellationToken();

        public void Activate()
        {
            Interlocked.Exchange(ref _isActive, 1);
        }

        public void Deactivate()
        {
            Interlocked.Exchange(ref _isActive, 0);
        }

        public Guid Id { get; set; }
        public Socket Socket { get; set; }
        
        public byte[] Buffer { get; set; } = new byte[BufferSize];
        public StringBuilder Command { get; set; } = new StringBuilder();

        public string Message { get; set; }

        public ProducerController ProducerController = null;
        public Guid LinkToClientId = Guid.Empty;
        public ConcurrentBag<Guid> LinkedClientIds = new ConcurrentBag<Guid>();
    }
}
