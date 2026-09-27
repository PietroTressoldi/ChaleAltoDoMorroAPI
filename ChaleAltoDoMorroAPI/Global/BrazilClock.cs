using ChaleAltoDoMorroAPI.Interfaces;

namespace ChaleAltoDoMorroAPI.Global
{
    public class BrazilClock(TimeProvider TimeProvider) : IClock
    {

        public static readonly TimeZoneInfo brasiliaTimezone = GetBrasiliaTime();

        public DateTime Now()
        {
            var utcNow = TimeProvider.GetUtcNow();
            return TimeZoneInfo.ConvertTime(utcNow, brasiliaTimezone).DateTime;

        }

        public DateOnly Today()
        {
            return DateOnly.FromDateTime(Now());
        }

        public static TimeZoneInfo GetBrasiliaTime()
        {
            TimeZoneInfo brasiliaTimeZone;
            try
            {
                brasiliaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                brasiliaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
            }

            return brasiliaTimeZone;
        }
    }
}
