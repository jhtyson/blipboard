using Priority_Queue;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace BlipBoard
{
    public class Event
    {
        public String Logger { get; set; }

        public Level Level { get; set; }

        public String Details { get; set; }

        public DateTimeOffset Time { get; set; }

    }

    public class Blip
    {
        public Level Level { get; set; }

        public String Lane { get; set; }

        public Int64 TimeBegin { get; set; }

        public Int64 TimeEnd { get; set; }

        public Int32 Count { get; set; }

        public String Body { get; set; }
    }

    public enum Level
    {
        Trace = 0,
        Debug = 1,
        Info = 2,
        Warning = 3,
        Error = 4,
        Fatal = 5,

        Warn = Warning
    }
}
