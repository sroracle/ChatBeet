﻿using ChatBeet;
using System;

namespace DtellaRules.Models
{
    public class DownloadCompleteMessage : IInboundMessage
    {
        public string Name { get; set; }

        public string Source { get; set; }

        public string Sender => Source;

        public string Content => Name;

        public DateTime DateRecieved { get; set; } = DateTime.Now;
    }
}
