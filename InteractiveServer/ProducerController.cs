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
        public static int MaxProducers = 50;
        public int ProducerCount => _producers.Count();

        public int TransferredWords
        {
            get
            {
                int count = 0;
                lock (_transferWordCountLock)
                {
                    count = _transferredWords;
                }
                return count;
            }
        }

        public int PercentComplete => (int)(((decimal)_dataService.GetCurrentIndex() / DataService.WordCount) * 100);
        public string TransferStatus => $"{TransferredWords} / {DataService.WordCount}";
        public int BufferLevel => (int)(((decimal)Buffer.Count() / Buffer.BoundedCapacity) * 100);
        public double BufferLevelRunningAverage => BufferLevels.IsEmpty ? 0 : Math.Round(BufferLevels.Average(), 2);

        private ConcurrentDictionary<Guid, Producer> _producers = new ConcurrentDictionary<Guid, Producer>();
        private ConcurrentDictionary<Guid, Thread> _producerThreads = new ConcurrentDictionary<Guid, Thread>();
        private BlockingCollection<Word> Buffer;
        private ConcurrentQueue<int> BufferLevels;
        private int _transferredWords = 0;

        private int _bufferSize = 0;
        private DataService _dataService;
        private long _producerWatcherStopRequested = 0;
        private Thread _producerWatcherThread = null;
        private object _transferWordCountLock = new object();

        public ProducerController(int bufferSize)
        {
            _bufferSize = bufferSize;
            Buffer = new BlockingCollection<Word>(_bufferSize);
            BufferLevels = new ConcurrentQueue<int>();
            _dataService = new DataService();
        }

        public void StartSelfAdjustingProducers()
        {
            if (_producerWatcherThread == null)
            {
                _producerWatcherThread = new Thread(new ThreadStart(() => StartProducers()));
                _producerWatcherThread.Start();
            }
        }

        // This is the governor of the self-adjusting producers.
        // In order to adjust, this runs on it's own thread and
        // monitors the running average buffer level.
        private void StartProducers()
        {
            StartProducers(1);

            while (true)
            {
                // if (_producers.All(p => !p.IsActive)) StopAllProducers();

                if (Interlocked.Read(ref _producerWatcherStopRequested) == 1)
                {
                    break;
                }

                if (BufferLevelRunningAverage >= 80 && ProducerCount > 1)
                {
                    StopProducers(1);
                }
                else if (BufferLevelRunningAverage <= 20 && ProducerCount < MaxProducers)
                {
                    StartProducers(1);
                }

                Thread.Sleep(500);
            }
        }

        public void StartProducers(int count)
        {
            const int MAX_ATTEMPTS = 100;

            for (int i = 0; i < count; i++)
            {
                var producer = new Producer(this, _dataService);

                int attempts = 0;
                bool success = false;
                while (attempts++ < MAX_ATTEMPTS && !(success = _producers.TryAdd(producer.Id, producer)))
                {
                    Thread.Sleep(10);
                }

                if (!success) continue;

                var thread = new Thread(new ThreadStart(() => producer.Produce()));

                int threadAttempts = 0;
                var threadSuccess = false;
                while (threadAttempts++ < MAX_ATTEMPTS && !(threadSuccess = !_producerThreads.TryAdd(producer.Id, thread)))
                {
                    Thread.Sleep(10);
                }
                if (threadSuccess) thread.Start();
            }
        }

        public bool AddToBuffer(Word word)
        {
            var successful = Buffer.TryAdd(word);
            UpdateTrackedBufferLevel();
            return successful;
        }

        public Word TakeFromBuffer()
        {
            var word = Buffer.Take();
            UpdateTrackedBufferLevel();
            word.BufferLevel = BufferLevelRunningAverage;

            lock (_transferWordCountLock)
            {
                _transferredWords++;
            }

            return word;
        }

        private void UpdateTrackedBufferLevel()
        {
            BufferLevels.Enqueue(BufferLevel);
            if (BufferLevels.Count() > 100)
            {
                int oldestBufferLevel;
                BufferLevels.TryDequeue(out oldestBufferLevel);
            }
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
                var producerKey = _producers.Keys.FirstOrDefault();

                var attempts = 0;
                const int MAX_ATTEMPTS = 100;
                Producer removeProducer = null;
                bool success = false;
                while (attempts++ < MAX_ATTEMPTS && !(success = _producers.TryRemove(producerKey, out removeProducer)))
                {
                    Thread.Sleep(10);
                }
                StopProducer(removeProducer);
            }
        }

        private void StopProducer(Producer producer)
        {
            if (producer != null)
            {
                producer.Stop();

                int attempts = 0;
                const int MAX_ATTEMPTS = 100;
                bool success = false;
                Thread thread = null;
                while (attempts++ < MAX_ATTEMPTS && !(success = _producerThreads.TryRemove(producer.Id, out thread)))
                {
                    Thread.Sleep(10);
                }

                if (thread != null) thread.Join();
            }
        }

        public void StopAllProducers()
        {
            Interlocked.Exchange(ref _producerWatcherStopRequested, 1);

            if (_producerWatcherThread != null)
            {
                _producerWatcherThread.Join();
            }

            while (_producers.Count() > 0)
            {
                StopProducers(_producers.Count());
            }
        }
    }
}
