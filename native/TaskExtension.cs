namespace NativeExposer.Build;

public static class TaskExtension
{
    public static Task<T> Watch<T>(this Task<T> t, string title)
    {
        var handle = Progress.Instance.Begin(title);
        return t.ContinueWith(task =>
        {
            Progress.Instance.End(handle);
            return task.Result;
        });
    }
    
    public static Task Watch(this Task t, string title)
    {
        var handle = Progress.Instance.Begin(title);
        return t.ContinueWith(task =>
        {
            task.GetAwaiter().GetResult();
            Progress.Instance.End(handle);
        });
    }
}
