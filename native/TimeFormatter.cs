namespace NativeExposer.Build;

public static class TimeFormatter
{
    public static string Format(this TimeSpan time)
    {
        return time switch
        {
            { TotalDays        : >= 1 } => $"{time.TotalDays:F1}d",
            { TotalHours       : >= 1 } => $"{time.TotalHours:F1}h",
            { TotalMinutes     : >= 1 } => $"{time.TotalMinutes:F1}m",
            { TotalSeconds     : >= 1 } => $"{time.TotalSeconds:F1}s",
            { TotalMilliseconds: >= 1 } => $"{time.TotalMilliseconds:F1}ms",
            { TotalMicroseconds: >= 1 } => $"{time.TotalMicroseconds:F1}us",
            _                           => $"{time.TotalNanoseconds:F1}ns"
        };
    }
}