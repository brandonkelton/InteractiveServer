using System;
using System.Collections.Generic;
using System.Text;

namespace InteractiveServer
{
    public class CommandTypes
    {
        public const string HELLO = "hello";
        public const string ECHO = "echo";
        public const string ENDPOINT = "endpoint";
        public const string CLIENTS = "clients";
        public const string STATE = "state";
        public const string START_PRODUCERS = "startproducers";
        public const string STOP_PRODUCERS = "stopproducers";
        public const string PEEK_PRODUCERS = "peekproducers";
        public const string GET_WORD = "getword";
        public const string DISCONNECT = "disconnect";
        public const string EXIT = "exit";
    }
}
