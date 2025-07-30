using System.Text;

namespace NativeExposer.Build;

public static class Resources
{
    public static async Task<string> Embedded(string name)
    {
        await using var stream = typeof(Resources).Assembly.GetManifestResourceStream("NativeExposer.Build.Resources." + name)!;
        using var       reader = new StreamReader(stream, Encoding.UTF8);

        return await reader.ReadToEndAsync();
    }
}