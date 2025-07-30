namespace NativeExposer.Build;

public class Progress : IDisposable
{
    private static readonly Lazy<Progress> Lazy = new(() => new Progress());
    public static           Progress       Instance => Lazy.Value;

    private class ProgressEntry(string title, DateTime begin, DateTime? end = null)
    {
        public string    Title { get; }      = title;
        public DateTime  Begin { get; }      = begin;
        public DateTime? End   { get; set; } = end;
    }

    private readonly Thread              _thread;
    private readonly List<ProgressEntry> _logs = new();

    private bool _stop;

    public Progress()
    {
        _thread = new Thread(Tick);
        _thread.Start();
    }

    ~Progress()
    {
        Dispose();
    }

    private void Tick()
    {
        while (!_stop || _logs.Count > 0)
        {
            var log = _logs.FirstOrDefault();
            if (log is null)
                continue;

            var elapsed = (log.End ?? DateTime.Now) - log.Begin;

            var msg = $"\r{elapsed.Format(),8}: {log.Title}";
            msg += new string(' ', Console.BufferWidth - msg.Length);

            Console.Write(msg);

            if (log.End is not null)
            {
                Console.Write('\n');
                _logs.Remove(log);
            }
            
            Thread.Sleep(100);
        }
    }

    public object Begin(string title)
    {
        var log = new ProgressEntry(title, DateTime.Now);
        _logs.Add(log);
        return log;
    }

    public void End(object obj)
    {
        ((ProgressEntry)obj).End = DateTime.Now;
    }

    public void Dispose()
    {
        _stop = true;
        _thread.Join();
    }
}