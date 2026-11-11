using NUnit.Framework;
namespace RoadsideHaven.Tests
{
    public sealed class DayNightClockTests
    {
        [TestCase(10f)] [TestCase(12f)] [TestCase(14f)]
        public void EightyPercentDayAndTwentyPercentNightPreserveCalendar(float daylight)
        {
            float sunrise=12-daylight/2, sunset=12+daylight/2;
            Assert.That(DayNightClock.Advance(sunrise,384,480,daylight,.8f,out _),Is.EqualTo(sunset).Within(.001f));
            Assert.That(DayNightClock.Advance(sunset,96,480,daylight,.8f,out int days),Is.EqualTo(sunrise).Within(.001f));
            Assert.AreEqual(1,days);
            Assert.That(DayNightClock.Advance(15,480,480,daylight,.8f,out days),Is.EqualTo(15).Within(.001f));
            Assert.AreEqual(1,days);
        }
    }
}
