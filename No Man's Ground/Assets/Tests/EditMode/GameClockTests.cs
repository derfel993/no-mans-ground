using System;
using NoMansGround.Map;
using NUnit.Framework;

namespace NoMansGround.Tests
{
    public sealed class GameClockTests
    {
        [Test]
        public void NewClockStartsOnDayOneAtNineAndPreservesFractionalTime()
        {
            var clock = new GameClock();
            Assert.AreEqual("Giorno 1 - 09:00", clock.Display);
            clock.AdvanceTime(1d);
            Assert.AreEqual("Giorno 1 - 09:00", clock.Display);
            clock.AdvanceTime(1d);
            Assert.AreEqual("Giorno 1 - 09:01", clock.Display);
            Assert.AreEqual(2d, clock.ElapsedSeconds);
        }

        [TestCase(100d, 1, 10, 0)]
        [TestCase(1499d, 1, 23, 59)]
        [TestCase(1500d, 2, 0, 0)]
        [TestCase(2400d, 2, 9, 0)]
        [TestCase(3900d, 3, 0, 0)]
        [TestCase(24000d, 11, 9, 0)]
        public void ClockUsesExactlyFortyMinutesPerDayAndRollsOverAtMidnight(
            double seconds, int day, int hour, int minute)
        {
            var clock = new GameClock();
            clock.AdvanceTime(seconds);
            Assert.AreEqual(day, clock.Day);
            Assert.AreEqual(hour, clock.Hour);
            Assert.AreEqual(minute, clock.Minute);
        }

        [Test]
        public void FrameSubdivisionPreservesClockAndResetRestoresInitialTime()
        {
            var single = new GameClock();
            var stepped = new GameClock();
            single.AdvanceTime(2399.5d);
            for (int i = 0; i < 143970; i++)
                stepped.AdvanceTime(1d / 60d);
            Assert.AreEqual(single.Display, stepped.Display);
            Assert.AreEqual(single.ElapsedSeconds, stepped.ElapsedSeconds, 0.000001d);
            stepped.Reset();
            Assert.AreEqual("Giorno 1 - 09:00", stepped.Display);
            Assert.Zero(stepped.ElapsedSeconds);
        }

        [Test]
        public void NotificationsOnlyOccurWhenDisplayedMinuteChangesOrClockResets()
        {
            var clock = new GameClock();
            int notifications = 0;
            clock.Changed += () => notifications++;
            clock.AdvanceTime(0d);
            clock.AdvanceTime(1d);
            Assert.Zero(notifications);
            clock.AdvanceTime(1d);
            Assert.AreEqual(1, notifications);
            clock.AdvanceTime(2400d);
            Assert.AreEqual(2, notifications);
            clock.Reset();
            Assert.AreEqual(3, notifications);
        }

        [TestCase(-1d)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(double.MaxValue)]
        public void InvalidTimeIsRejectedWithoutChangingClock(double seconds)
        {
            var clock = new GameClock();
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.AdvanceTime(seconds));
            Assert.AreEqual("Giorno 1 - 09:00", clock.Display);
            Assert.Zero(clock.ElapsedSeconds);
        }
    }
}
