using System;

namespace BlipBoard
{
    public class ScaleSettings
    {
        public TimeSpan TimeBegin { get; set; }
        public TimeSpan TimeEnd { get; set; }

        public Double DisplayBegin { get; set; }
        public Double DisplayEnd { get; set; }

        public Double BlipSize { get; set; }
    }

    public interface IScale
    {
        Double Scale(Int64 t);

        Int64 ScaleInverted(Double p);

        ScaleSettings Settings { get; }
    }

    public class LogScale : IScale
    {
        ScaleSettings settings;

        private Double blipSize;

        private Int64 t0;
        private Int64 t1;
        private Double p0;
        private Double p1;

        private Double l0, l1, f, g;

        public LogScale(ScaleSettings settings)
        {
            this.settings = settings;

            this.blipSize = settings.BlipSize;
            this.t0 = settings.TimeBegin.Ticks / 10000;
            this.t1 = settings.TimeEnd.Ticks / 10000;
            this.p0 = settings.DisplayBegin;
            this.p1 = settings.DisplayEnd;

            l0 = Math.Log(t0);
            l1 = Math.Log(t1);

            f = (p1 - p0) / (l1 - l0);
            g = l0 - p0 / f;
        }

        public ScaleSettings Settings => settings;

        public Double Scale(Int64 t)
        {
            if (t < t0) return p0;

            return p0 + f * (Math.Log(t) - l0);
        }

        public Int64 ScaleInverted(Double p)
        {
            return (Int64)Math.Exp(l0 + (p - p0) / f);
        }
    }

    public static class ScaleExtensions
    {
        public static Double GetEndPosition(this IScale scale, Int64 now, Blip blip)
        {
            var e = scale.Scale(now - blip.TimeEnd);
            var b = scale.Scale(now - blip.TimeBegin);

            return Math.Min(e, b - scale.Settings.BlipSize);
        }

        public static IScale MakeLogScale(this ScaleSettings settings)
        {
            return new LogScale(settings);
        }
    }
}
