using Azma.Runs.Api.Domain;

namespace Azma.Runs.Api.Application.Runs;

internal static class RunScheduleCalculator
{
    private static readonly Dictionary<string, string> IanaToWindows = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Asia/Tehran"] = "Iran Standard Time",
        ["UTC"] = "UTC",
        ["Etc/UTC"] = "UTC",
        ["America/New_York"] = "Eastern Standard Time",
        ["America/Chicago"] = "Central Standard Time",
        ["America/Denver"] = "Mountain Standard Time",
        ["America/Los_Angeles"] = "Pacific Standard Time",
        ["Europe/London"] = "GMT Standard Time",
        ["Europe/Berlin"] = "W. Europe Standard Time",
        ["Asia/Dubai"] = "Arabian Standard Time",
    };

    private static readonly Dictionary<string, string> WindowsToIana = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Iran Standard Time"] = "Asia/Tehran",
        ["Eastern Standard Time"] = "America/New_York",
        ["Central Standard Time"] = "America/Chicago",
        ["Mountain Standard Time"] = "America/Denver",
        ["Pacific Standard Time"] = "America/Los_Angeles",
        ["GMT Standard Time"] = "Europe/London",
        ["W. Europe Standard Time"] = "Europe/Berlin",
        ["Arabian Standard Time"] = "Asia/Dubai",
    };

    public static DateTime? NextRunUtc(
        ScheduleRecurrenceType recurrenceType,
        int timeOfDayMinutes,
        int weekDaysMask,
        string timeZoneId,
        DateTime? startAtUtc,
        DateTime? endAtUtc,
        DateTime nowUtc)
    {
        if (TryGetIntervalMinutes(recurrenceType, timeOfDayMinutes, out var intervalMinutes))
            return NextIntervalRunUtc(intervalMinutes, startAtUtc, endAtUtc, nowUtc);

        if (recurrenceType == ScheduleRecurrenceType.Weekly && weekDaysMask == 0)
            return null;

        var zone = ResolveTimeZone(timeZoneId);
        var baseUtc = startAtUtc is not null && startAtUtc.Value > nowUtc
            ? startAtUtc.Value
            : nowUtc;
        var localBase = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(baseUtc, DateTimeKind.Utc), zone);
        var minutes = Math.Clamp(timeOfDayMinutes, 0, 1439);

        for (var offset = 0; offset < 370; offset++)
        {
            var localDate = localBase.Date.AddDays(offset);
            if (recurrenceType == ScheduleRecurrenceType.Weekly && !IncludesDay(weekDaysMask, localDate.DayOfWeek))
                continue;

            var candidateLocal = localDate.AddMinutes(minutes);
            if (candidateLocal <= localBase)
                continue;

            var candidateUtc = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(candidateLocal, DateTimeKind.Unspecified), zone);
            if (startAtUtc is not null && candidateUtc < startAtUtc.Value)
                continue;
            if (endAtUtc is not null && candidateUtc > endAtUtc.Value)
                return null;

            return candidateUtc;
        }

        return null;
    }

    public static bool TryGetIntervalMinutes(
        ScheduleRecurrenceType recurrenceType,
        int configuredMinutes,
        out int intervalMinutes)
    {
        intervalMinutes = recurrenceType switch
        {
            ScheduleRecurrenceType.Hourly => 60,
            ScheduleRecurrenceType.Every30Minutes => 30,
            ScheduleRecurrenceType.Every15Minutes => 15,
            ScheduleRecurrenceType.Every10Minutes => 10,
            ScheduleRecurrenceType.Every5Minutes => 5,
            _ => 0,
        };

        if (intervalMinutes > 0)
            return true;

        return false;
    }

    private static DateTime? NextIntervalRunUtc(
        int intervalMinutes,
        DateTime? startAtUtc,
        DateTime? endAtUtc,
        DateTime nowUtc)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, intervalMinutes));
        var candidate = startAtUtc switch
        {
            { } start when start > nowUtc => start,
            { } start => start.AddTicks(((nowUtc - start).Ticks / interval.Ticks + 1) * interval.Ticks),
            _ => nowUtc.Add(interval),
        };

        if (endAtUtc is not null && candidate > endAtUtc.Value)
            return null;

        return candidate;
    }

    private static bool IncludesDay(int mask, DayOfWeek day) =>
        (mask & (1 << (int)day)) != 0;

    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? "Asia/Tehran" : timeZoneId.Trim();
        return TryFind(id)
            ?? (IanaToWindows.TryGetValue(id, out var windowsId) ? TryFind(windowsId) : null)
            ?? (WindowsToIana.TryGetValue(id, out var ianaId) ? TryFind(ianaId) : null)
            ?? TryFind("Asia/Tehran")
            ?? TryFind("Iran Standard Time")
            ?? TimeZoneInfo.Utc;
    }

    private static TimeZoneInfo? TryFind(string id)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            return null;
        }
        catch (InvalidTimeZoneException)
        {
            return null;
        }
    }
}
