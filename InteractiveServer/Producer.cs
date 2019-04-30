using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace InteractiveServer
{
    public class Producer
    {
        private ProducerController _producerController;
        private DataService _dataService;
        private long _stopRequested = 0;

        public Producer(ProducerController producerController, DataService dataService)
        {
            _producerController = producerController;
            _dataService = dataService;
        }

        public void Produce()
        {
            while (_dataService.HasWords())
            {
                if (Interlocked.Read(ref _stopRequested) == 1)
                    break;

                var word = _dataService.GetNextWord();

                while (!_producerController.AddToBuffer(word))
                {
                    if (Interlocked.Read(ref _stopRequested) == 1)
                        break;

                    Thread.Sleep(100);
                }
            }
        }

        public void Stop()
        {
            Interlocked.Exchange(ref _stopRequested, 1);
        }
    }
}
