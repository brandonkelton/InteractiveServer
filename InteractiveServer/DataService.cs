using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace InteractiveServer
{
    public class DataService
    {
        private static Word[] Words = null;
        public static bool IsDataLoaded { get; private set; } = false;
        public static long LargestIndex { get; private set; } = 0;
        public static int WordCount => Words.Count();

        // Maybe I was too efficient about loading the data here. I thought about making it load
        // each time a producer requested an indexed word, so as to minic the generation of a word,
        // but then I thought that was ridiculous, so I kept this.
        public static async Task LoadData(string fileName = null)
        {
            if (String.IsNullOrEmpty(fileName))
            {
                var directory = new DirectoryInfo("Assets");
                var file = directory.GetFiles("RawBook.txt")[0];
                fileName = file.FullName;
            }

            string text;

            try
            {
                using (StreamReader sr = new StreamReader(fileName))
                {
                    text = await sr.ReadToEndAsync();
                }
            }
            catch (FileNotFoundException ex)
            {
                text = ex.Message;
            }

            text = text + " <EOF>";

            Words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select((word, index) => new Word { Index = index, Text = word }).ToArray();

            LargestIndex = Words.Length - 1;

            IsDataLoaded = true;
        }

        private long _currentIndex = 0;
        private object _objLock = new object();

        public long GetCurrentIndex()
        {
            long currentIndex;

            lock (_objLock)
            {
                currentIndex = _currentIndex;
            }

            return currentIndex;
        }

        public bool HasWords()
        {
            bool hasWords = false;

            lock (_objLock)
            {
                hasWords = _currentIndex <= LargestIndex;
            }

            return hasWords;
        }

        public Word GetNextWord()
        {
            long currentIndex;

            lock (_objLock)
            {
                currentIndex = _currentIndex;
                _currentIndex++;
            }

            var word = Words.FirstOrDefault(w => w.Index == currentIndex);
            return word;
        }
    }
}
