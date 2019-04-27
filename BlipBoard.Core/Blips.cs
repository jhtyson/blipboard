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

    public class QueuedBlip
    {
        public Blip Blip { get; set; }
        public QueuedBlip Next { get; set; }
        public QueuedBlip Previous { get; set; }
        public Channel Channel { get; set; }
        public DateTimeOffset Priority { get; set; }

        public IEnumerable<QueuedBlip> SelfAndEarlier {
            get {
                for (var current = this; current != null; current = current.Previous)
                {
                    yield return current;
                }
            }
        }

        public IEnumerable<QueuedBlip> SelfAndLater {
            get {
                for (var current = this; current != null; current = current.Next)
                {
                    yield return current;
                }
            }
        }
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

    public class Channel
    {
        QueuedBlip latest = null, first = null;

        public ChannelGroup Group { get; set; }

        public Level Level { get; set; }

        public IEnumerable<Blip> Blips => first.SelfAndLater.Select(n => n.Blip);

        public Object Element { get; set; }

        public void Add(Blip blip)
        {
            if (latest != null && blip.TimeBegin < latest.Blip.TimeBegin)
            {
                return;
            }

            var node = new QueuedBlip { Channel = this, Blip = blip, Previous = latest };

            if (latest != null)
            {
                latest.Next = node;
            }

            latest = node;

            if (first == null)
            {
                first = node;
            }
        }

        public void Merge(IScale scale, Int64 now, QueuedBlip node)
        {
            var next = node.Next;
            var previous = node.Previous;

            if (previous != null)
            {
                previous.Blip.TimeEnd = now - scale.ScaleInverted(scale.GetEndPosition(now, node.Blip));
                previous.Blip.Count += node.Blip.Count;
                previous.Next = next;
            }

            if (next != null)
            {
                next.Previous = previous;
            }
            else
            {
                latest = previous;
            }

            node.Next = node.Previous = null;
        }

        public void MergeAllApplicable(IScale scale, Int64 now)
        {
            var current = latest;

            while (current != null && current.Previous != null)
            {
                var cbp = scale.Scale(now - current.Blip.TimeBegin);
                var pbp = scale.Scale(now - current.Previous.Blip.TimeBegin);
                var pep = scale.Scale(now - current.Previous.Blip.TimeEnd);
                var pp = Math.Min(pbp - scale.Settings.BlipSize, pep);

                var previous = current.Previous;

                if (pp < cbp)
                {
                    Merge(scale, now, current);
                }

                current = previous;
            }
        }
    }

    public class ChannelGroup
    {
        public String Logger { get; set; }

        public Object Element { get; set; }

        public Dictionary<Level, Channel> Channels { get; private set; } = new Dictionary<Level, Channel>();
    }

    public class BlipBoardSettings
    {
        public ScaleSettings Scale { get; set; }
    }

    public class ScaleSettings
    {
        public TimeSpan TimeBegin { get; set; }
        public TimeSpan TimeEnd { get; set; }

        public Double DisplayBegin { get; set; }
        public Double DisplayEnd { get; set; }

        public Double BlipSize { get; set; }
    }

    public class BlipRepo
    {
        public DateTimeOffset LastBlibReceivedAt = DateTimeOffset.UtcNow;

        Dictionary<String, ChannelGroup> channelGroups = new Dictionary<String, ChannelGroup>();

        Queue<Blip> latestBlips = new Queue<Blip>();

        //SimplePriorityQueue<QueuedBlip, DateTimeOffset> queue = new SimplePriorityQueue<QueuedBlip, DateTimeOffset>();

        public ScaleSettings Settings => scaleSettings;

        public static ScaleSettings scaleSettings = new ScaleSettings
        {
            TimeBegin = TimeSpan.FromSeconds(1),
            TimeEnd = TimeSpan.FromDays(30),
            DisplayBegin = 0,
            DisplayEnd = 1080,
            BlipSize = 20
        };

        IScale scale = scaleSettings.MakeLogScale();

        public IScale Scale => scale;

        public IEnumerable<ChannelGroup> ChannelGroups => channelGroups.Values.OrderBy(c => c.Logger);

        IEnumerable<Blip> GetBlips() {
            foreach (var channelGroup in ChannelGroups)
            {
                foreach (var channel in channelGroup.Channels.Values)
                {
                    foreach (var blip in channel.Blips)
                    {
                        yield return blip;
                    }
                }
            }
        }

        public Blip[] Blips { get { lock (this) return GetBlips().ToArray(); } }

        public void Add(Level level, String lane, String body)
        {
            var time = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            AddInternal(new Blip
            {
                Level = level,
                Lane = lane,
                Body = body,
                Count = 1,
                TimeBegin = time,
                TimeEnd = time
            });
        }

        public void Add(Blip blip)
        {
            AddInternal(blip);
        }

        void AddInternal(Blip blip)
        {
            lock (this)
            {
                latestBlips.Enqueue(blip);

                Insert(blip);

                LastBlibReceivedAt = DateTimeOffset.UtcNow;
            }
        }

        public Blip[] GetBlipsSince(Int64 since)
        {
            Blip[] blips;

            lock (this)
            {
                blips = latestBlips.ToArray();
            }

            Int32 i;
            for (i = 0; i < blips.Length; ++i)
            {
                if (blips[i].TimeBegin > since) break;
            }

            var result = new Blip[blips.Length - i];

            Array.Copy(blips, i, result, 0, blips.Length - i);

            return result;
        }

        public Blip[] GetAllBlips()
        {
            lock (this)
            {
                return Blips.ToArray();
            }
        }

        private void Insert(Blip blip)
        {
            ChannelGroup channelGroup;

            if (!channelGroups.TryGetValue(blip.Lane, out channelGroup))
            {
                channelGroups[blip.Lane] = channelGroup = new ChannelGroup { Logger = blip.Lane };
            }

            Channel channel;

            if (!channelGroup.Channels.TryGetValue(blip.Level, out channel))
            {
                channelGroup.Channels.Add(blip.Level, channel = new Channel { Group = channelGroup, Level = blip.Level });
            }
            channel.Add(blip);
        }

        public void PoorMansSpool(Int64 now)
        {
            lock (this)
            {
                foreach (var channelGroup in ChannelGroups)
                {
                    foreach (var channel in channelGroup.Channels.Values)
                    {
                        channel.MergeAllApplicable(scale, now);
                    }
                }
            }
        }
    }
}
