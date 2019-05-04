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
        public const string CLIENT_ID = "id";
        public const string LINK_CLIENT = "link";
        public const string LINK_TO_CLIENT = "linkto";
        public const string UNLINK_FROM_CLIENT = "unlinkfrom";
        public const string BUFFER = "buffer";
        public const string TRANSFER_STATUS = "transferstatus";
        public const string SET_BUFFER = "setbuffer";
        public const string START_PRODUCERS = "startproducers";
        public const string STOP_PRODUCERS = "stopproducers";
        public const string STOP_ALL_PRODUCERS = "stopallproducers";
        public const string PEEK_BUFFER = "peekbuffer";
        public const string STATUS = "status";
        public const string STATUS_OF = "statusof";
        public const string STATUS_FOR = "statusfor";
        public const string GET_WORD = "getword";
        public const string REPEAT_WAIT = "repeatwait";
        public const string DISCONNECT = "disconnect";
        public const string EXIT = "exit";
    }
}
