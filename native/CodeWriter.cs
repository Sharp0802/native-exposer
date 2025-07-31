namespace NativeExposer.Build;

public class CodeWriter(TextWriter writer)
{
    private bool _indented;

    public int Indent { get; set; }

    private void EnsureIndent()
    {
        if (_indented)
            return;

        writer.Write(new string(' ', Indent * 2));
        _indented = true;
    }

    private void Write(char value)
    {
        if (value == '\n')
            _indented = false;
        else
            EnsureIndent();

        writer.Write(value);
    }

    public void Write(string value)
    {
        foreach (var ch in value)
            Write(ch);
    }

    public void WriteLine(string value)
    {
        foreach (var ch in value)
            Write(ch);
        Write('\n');
    }
}