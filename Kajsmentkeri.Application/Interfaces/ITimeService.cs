using System;

namespace Kajsmentkeri.Application.Interfaces;

public interface ITimeService
{
    DateTime UtcNow { get; }
    DateTime NowBratislava { get; }
    DateTime ToBratislava(DateTime utcDateTime);
    DateTime ToUtc(DateTime bratislavaDateTime);
}
