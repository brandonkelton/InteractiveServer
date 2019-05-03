using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace InteractiveServer
{
    public class ProducerController
    {
        public static int MaxProducers = 100;
        public int ProducerCount;

        private List<Producer> _producers = new List<Producer>();
        private Dictionary<Guid, Thread> _producerThreads = new Dictionary<Guid, Thread>();
        private BlockingCollection<Word> Buffer;
        private int _bufferSize = 0;
        private DataService _dataService;
        private long _producerWatcherStopRequested = 0;
        private Thread _producerWatcherThread = null;

        public ProducerController(int bufferSize)
        {
            _bufferSize = bufferSize;
            Buffer = new BlockingCollection<Word>(_bufferSize);
            _dataService = new DataService();
        }

        public int PercentComplete => (int)(_dataService.GetCurrentIndex() / DataService.WordCount) * 100;
        public string TransferStatus => $"Words Transferred: {_dataService.GetCurrentIndex()}/{DataService.WordCount} > {PercentComplete}%";
        public decimal BufferLevel => ((decimal)Buffer.Count() / Buffer.BoundedCapacity) * 100;

        public void StartSelfAdjustingProducers()
        {
            if (_producerWatcherThread == null)
            {
                var thread = new Thread(new ThreadStart(() => StartProducers()));
                thread.Start();
            }
        }

        private void StartProducers()
        {
            while (true)
            {
                if (Interlocked.Read(ref _producerWatcherStopRequested) == 1)
                {
                    break;
                }

                if (BufferLevel > 90 && ProducerCount > 1)
                {
                    StopProducers(1);
                }
                else if (BufferLevel < 60 && ProducerCount < MaxProducers)
                {
                    StartProducers(1);
                }

                Thread.Sleep(250);
            }
        }

        public void StartProducers(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var producer = new Producer(this, _dataService);
                _producers.Add(producer);

                var thread = new Thread(new ThreadStart(() => producer.Produce()));
                _producerThreads.Add(producer.Id, thread);
                thread.Start();
            }

            ProducerCount = _producers.Count();
        }

        public bool AddToBuffer(Word word)
        {
            return Buffer.TryAdd(word);
        }

        public Word TakeFromBuffer()
        {
            var word = Buffer.Take();
            word.BufferLevel = Buffer.Count();
            return word;
        }

        public string[] PeekBuffer()
        {
            var contents = Buffer.ToArray()
                .Select((word, bufferIndex) => $"Buffer {bufferIndex}: {{ Index: {word.Index}, Word: {word.Text.Replace(@"\", @"\\")} }}").ToArray();

            return contents;
        }

        public void StopProducers(int count)
        {
            var stopProducerCount = count > _producers.Count() ? _producers.Count() : count;
            for (int i=0; i<stopProducerCount; i++)
            {
                var producer = _producers[i];
                producer.Stop();

                var thread = _producerThreads.GetValueOrDefault(producer.Id);
                if (thread != null)
                {
                    thread.Join();
                    _producerThreads.Remove(producer.Id);
                }
            }

            ProducerCount = _producers.Count();
        }

        public void StopAllProducers()
        {
            if (_producerWatcherThread != null && Interlocked.Read(ref _producerWatcherStopRequested) == 1)
            {
                Interlocked.Exchange(ref _producerWatcherStopRequested, 0);
                _producerWatcherThread.Join();
            }

            foreach (var producer in _producers)
            {
                producer.Stop();
            }

            foreach (var thread in _producerThreads.Values)
            {
                thread.Join();
            }
        }
    }
}
