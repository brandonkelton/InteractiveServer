using System;

namespace InteractiveServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var console = new InteractiveConsole();
            console.Start().Wait();
        }
    }
}
