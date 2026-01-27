using System;
using Kajsmentkeri.Application.Interfaces;

namespace Kajsmentkeri.Application.Services;

public class TimeService : ITimeService
{
    private static readonly TimeZoneInfo BratislavaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Central Europe Standard Time");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateTime NowBratislava => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BratislavaTimeZone);

    public DateTime ToBratislava(DateTime utcDateTime)
    {
        if (utcDateTime.Kind == DateTimeKind.Unspecified)
        {
            utcDateTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);
        }
        return TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, BratislavaTimeZone);
    }

    public DateTime ToUtc(DateTime bratislavaDateTime)
    {
        return TimeZoneInfo.ConvertTimeToUtc(bratislavaDateTime, BratislavaTimeZone);
    }
}
