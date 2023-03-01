using System;
using System.Collections.Generic;

namespace RMQReceiver
{
    public class AppSettings
    {
        public Uri? RabbitMqUri { get; set; }
        public List<string> Topics { get; set; } = new();
    }
}
