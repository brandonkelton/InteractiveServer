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
        private Client _client;
        private List<Producer> _producers = new List<Producer>();
        private List<Thread> _producerThreads = new List<Thread>();
        private BlockingCollection<Word> Buffer;
        private int _bufferSize = 0;
        private DataService _dataService;

        public ProducerController(Client client, int bufferSize)
        {
            _client = client;
            _bufferSize = bufferSize;
        }

        public void StartProducers(int count)
        {
            Buffer = new BlockingCollection<Word>(_bufferSize);
            _dataService = new DataService();

            for (int i=0; i<count; i++)
            {
                var producer = new Producer(this, _dataService);
                _producers.Add(producer);

                var thread = new Thread(new ThreadStart(() => producer.Produce()));
                _producerThreads.Add(thread);
                thread.Start();
            }
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

        public void StopProducers()
        {
            foreach (var producer in _producers)
            {
                producer.Stop();
            }

            foreach (var thread in _producerThreads)
            {
                thread.Join();
            }
        }
    }
}
