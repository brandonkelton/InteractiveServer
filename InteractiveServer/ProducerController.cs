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
        public int ProducerCount;

        private List<Producer> _producers = new List<Producer>();
        private Dictionary<Guid, Thread> _producerThreads = new Dictionary<Guid, Thread>();
        private BlockingCollection<Word> Buffer;
        private int _bufferSize = 0;
        private DataService _dataService;

        public ProducerController(int bufferSize)
        {
            _bufferSize = bufferSize;
            Buffer = new BlockingCollection<Word>(_bufferSize);
            _dataService = new DataService();
        }

        public int PercentComplete => (int)(_dataService.GetCurrentIndex() / DataService.WordCount) * 100;
        public string TransferStatus => $"Words Transferred: {_dataService.GetCurrentIndex()}/{DataService.WordCount} > {PercentComplete}%";
        public string BufferLevel => $"{(Buffer.Count() / Buffer.BoundedCapacity) * 100}%";

        public void Reset()
        {
            Buffer = new BlockingCollection<Word>(_bufferSize);
            _dataService = new DataService();
        }

        public void StartProducers(int count)
        {
            for (int i=0; i<count; i++)
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
        }

        public void StopAllProducers()
        {
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
