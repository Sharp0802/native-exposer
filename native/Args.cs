namespace NativeExposer.Build;

public class Args
{
    public string ProjectPath { get; }
    public string OutputPath  { get; }

    private Args(string project, string output)
    {
        ProjectPath = project;
        OutputPath  = output;
    }

    public static Args? Parse(string[] args)
    {
        if (args.Length < 2)
        {
            Console.WriteLine(
                """
                error: insufficient arguments
                usage: native-expose <csproj-file> <output-dir>
                """);
            return null;
        }

        return new Args(args[0], args[1]);
    }
}