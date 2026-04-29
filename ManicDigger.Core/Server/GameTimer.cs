using System.Diagnostics;

public class GameTimer
{
    private TimeSpan _time = TimeSpan.Zero;
    private long _startTimestamp = 0;
    private long _lastIngameSecond = 0;
    private bool _running = false;

    public int SpeedOfTime { get; set; } = 60;
    public int DaysPerYear { get; set; } = 4;

    public TimeSpan Time => _time;
    public int Hour => _time.Hours;
    public double HourTotal => _time.TotalHours;
    public int Day => (int)(_time.TotalDays % DaysPerYear);
    public double DaysTotal => _time.TotalDays;
    public int Year => (int)(_time.TotalDays / DaysPerYear);
    public int Season => Year % 4;

    internal void Init(long ticks)
    {
        _time = TimeSpan.FromTicks(ticks);
        _lastIngameSecond = 0;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    internal void Start()
    {
        _running = true;
        _startTimestamp = Stopwatch.GetTimestamp();
    }

    internal void Stop() => _running = false;

    internal bool Tick()
    {
        if (!_running || SpeedOfTime == 0) return false;

        long elapsed = Stopwatch.GetTimestamp() - _startTimestamp;
        long elapsedSeconds = elapsed / Stopwatch.Frequency;

        if (elapsedSeconds <= _lastIngameSecond) return false;

        long delta = elapsedSeconds - _lastIngameSecond;
        _lastIngameSecond = elapsedSeconds;
        _time += TimeSpan.FromSeconds(delta * SpeedOfTime);
        return true;
    }

    public int GetQuarterHourPartOfDay() => (_time.Hours * 4) + (_time.Minutes / 15);

    internal void Set(TimeSpan time) => _time = time;
    internal void Add(TimeSpan time) => _time += time;
}
