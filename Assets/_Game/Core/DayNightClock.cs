using System;
namespace RoadsideHaven
{
    /// <summary>Maps a fixed real cycle to a configurable daylight/night split while preserving seasonal sunrise/sunset.</summary>
    public static class DayNightClock
    {
        public static float Advance(float hour, float seconds, float cycleSeconds, float daylightHours, float dayFraction, out int days)
        {
            double length=Math.Max(30,cycleSeconds), daylight=Math.Max(1,Math.Min(23,daylightHours));
            double fraction=Math.Max(.1,Math.Min(.95,dayFraction)), night=24-daylight;
            double sunrise=12-daylight/2, sunset=12+daylight/2;
            double normalized=Repeat(hour,24);
            double raw=normalized>=sunrise && normalized<sunset ? (normalized-sunrise)/daylight*fraction :
                fraction+Repeat(normalized-sunset,24)/night*(1-fraction);
            double midnight=fraction+(24-sunset)/night*(1-fraction);
            double phase=Repeat(raw-midnight,1)+Math.Max(0,seconds)/length;
            days=(int)Math.Floor(phase+1e-10);
            raw=Repeat(phase+midnight,1);
            return (float)Repeat(raw<fraction ? sunrise+raw/fraction*daylight : sunset+(raw-fraction)/(1-fraction)*night,24);
        }
        private static double Repeat(double value,double length)=>value-Math.Floor(value/length)*length;
    }
}
