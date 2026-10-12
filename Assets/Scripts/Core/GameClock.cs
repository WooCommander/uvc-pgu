using System;

namespace Uvc.Core
{
    [Serializable]
    public sealed class GameClock
    {
        public const float DayStart = 9 * 60;
        public const float WorkEnd = 18 * 60;
        public const float DayEnd = 20 * 60;
        public const float MinutesPerDay = 24 * 60;
        public const float GameMinutesPerRealSecond = 0.5f;

        public int dayIndex;
        public float minutes = DayStart;

        public bool IsEvening => minutes >= WorkEnd;
        public bool IsOver => minutes >= DayEnd;
        public float Absolute => dayIndex * MinutesPerDay + minutes;

        public void Tick(float realSeconds)
        {
            if (realSeconds > 0f)
            {
                Spend(realSeconds * GameMinutesPerRealSecond);
            }
        }

        public void Spend(float gameMinutes)
        {
            if (gameMinutes > 0f)
            {
                minutes = Math.Min(DayEnd, minutes + gameMinutes);
            }
        }

        public string Format() => Format(minutes);

        public static string Format(float gameMinutes)
        {
            int total = (int)gameMinutes;
            return $"{total / 60 % 24:00}:{total % 60:00}";
        }

        public static string FormatDuration(float gameMinutes)
        {
            int total = Math.Max(0, (int)Math.Ceiling(gameMinutes));
            return $"{total / 60} ч {total % 60:00} мин";
        }
    }
}
