using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace InteractiveServer
{
    public class Producer
    {
        public Guid Id;

        private ProducerController _producerController;
        private DataService _dataService;
        private long _stopped = 1;

        public Producer(ProducerController producerController, DataService dataService)
        {
            Id = Guid.NewGuid();
            _producerController = producerController;
            _dataService = dataService;
        }

        public void Start()
        {
            Interlocked.Exchange(ref _stopped, 0);
        }

        public void Stop()
        {
            Interlocked.Exchange(ref _stopped, 1);
        }

        public bool IsActive => Interlocked.Read(ref _stopped) == 0;

        public void Produce()
        {
            Start();

            while (IsActive && _dataService.HasWords())
            {
                var word = _dataService.GetNextWord();
                if (word == null) Stop();

                while (IsActive && !_producerController.AddToBuffer(word))
                {
                    Thread.Sleep(10);
                }
            }
        }
    }
}
