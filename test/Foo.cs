namespace NativeExposer.Test;


public partial class Foo
{
    private int _i;

    [method: Export]
    public Foo(int i)
    {
        _i = i;
    }

    [Export]
    private int Bar(int a, int b)
    {
        Console.WriteLine("Hello from C#");
        return _i += a * b;
    }
}
