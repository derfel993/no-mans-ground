using System;

namespace NoMansGround.Map
{
    public sealed class GameClock
    {
        public const double SecondsPerDay = 40d * 60d;
        private const int MinutesPerDay = 24 * 60;
        private double elapsedSeconds;

        public double ElapsedSeconds => elapsedSeconds;
        private double TotalMinutes => 9 * 60 + elapsedSeconds * MinutesPerDay / SecondsPerDay;
        public int Day => 1 + (int)(TotalMinutes / MinutesPerDay);
        public int Hour => (int)(TotalMinutes % MinutesPerDay) / 60;
        public int Minute => (int)(TotalMinutes % 60);
        public string Display => $"Giorno {Day} - {Hour:00}:{Minute:00}";
        public event Action Changed;

        public void AdvanceTime(double deltaTime)
        {
            if (!double.IsFinite(deltaTime) || deltaTime < 0d
                || 9 * 60 + (elapsedSeconds + deltaTime) * MinutesPerDay / SecondsPerDay
                    >= (double)int.MaxValue * MinutesPerDay)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            double previousMinute = Math.Floor(TotalMinutes);
            elapsedSeconds += deltaTime;
            if (Math.Floor(TotalMinutes) != previousMinute)
                Changed?.Invoke();
        }

        public void Reset()
        {
            elapsedSeconds = 0d;
            Changed?.Invoke();
        }
    }
}
