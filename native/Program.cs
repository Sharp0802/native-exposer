using System.Diagnostics;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace NativeExposer.Build;

public static class Program
{
    private static async Task<Project?> LoadProject(MSBuildWorkspace msbuild, string path)
    {
        object? eval = null, build = null, resolve = null;
        
        var progress = new Progress<ProjectLoadProgress>();
        progress.ProgressChanged += (sender, args) =>
        {
            var tr = args.Operation switch
            {
                ProjectLoadOperation.Evaluate => __makeref(eval),
                ProjectLoadOperation.Build    => __makeref(build),
                ProjectLoadOperation.Resolve  => __makeref(resolve),
                _                             => throw new ArgumentOutOfRangeException()
            };
            ref var target = ref __refvalue(tr, object?);

            if (target is null)
            {
                var name = Path.GetFileNameWithoutExtension(args.FilePath);
                
                var verb = args.Operation switch
                {
                    ProjectLoadOperation.Evaluate => "evaluate ",
                    ProjectLoadOperation.Build    => "build ",
                    ProjectLoadOperation.Resolve  => "resolve ",
                    _                             => throw new ArgumentOutOfRangeException()
                };
                
                target = Progress.Instance.Begin(verb + name);
            }
            else
            {
                Progress.Instance.End(target);
            }
        };

        try
        {
            return await msbuild.OpenProjectAsync(path, progress);
        }
        catch (IOException e)
        {
            Console.WriteLine($"error: {e.Message}");
            return null;
        }
    }

    private static async Task GenHeader(Compilation compilation, StreamWriter writer)
    {
        var boilerplate = await Resources.Embedded("lib.h");
        await writer.WriteAsync(boilerplate);
    }
    
    private static async Task GenSource(Compilation compilation, StreamWriter writer)
    {
        var boilerplate = await Resources.Embedded("lib.h");
        await writer.WriteAsync(boilerplate);
    }
    
    private static async Task GenBuild(Compilation compilation, StreamWriter writer)
    {
        var assembly = compilation.ObjectType.ContainingAssembly;
        var version = assembly.Identity.Version;
        
        var boilerplate = await Resources.Embedded("CMakeLists.txt");

        boilerplate = boilerplate
            .Replace("@LIBRARY@", compilation.AssemblyName)
            .Replace("@DOTNET_RUNTIME_VERSION@", version.ToString(3));

        foreach (var line in boilerplate.Split('\n'))
        {
            if (line.StartsWith("##"))
                continue;
            
            await writer.WriteLineAsync(line);
        }
    }


    private static async Task<int> Main(string[] argv)
    {
        var r = await Start(argv);
        Progress.Instance.Dispose();
        return r;
    }
    
    private static async Task<int> Start(string[] argv)
    {
        var args = Args.Parse(argv);
        if (args is null)
            return -1;

        using var msbuild = MSBuildWorkspace.Create();

        var project = await LoadProject(msbuild, args.ProjectPath);
        if (project is null)
            return -1;

        if (!project.SupportsCompilation)
        {
            Console.WriteLine("error: project doesn't support compilation");
            return -1;
        }
        
        var compilation = await project.GetCompilationAsync().Watch("compile project");

        // compilation should fail only when project isn't compilable
        Debug.Assert(compilation is not null);

        var error = false;
        foreach (var diagnostic in compilation.GetDiagnostics())
        {
            if (diagnostic.Severity == DiagnosticSeverity.Hidden)
                continue;
            if (diagnostic.Severity == DiagnosticSeverity.Error)
                error = true;
            
            var diag = new DiagnosticFormatter().Format(diagnostic, CultureInfo.InvariantCulture);
            Console.WriteLine(diag);
        }

        if (error)
        {
            Console.WriteLine("error: failed to compile project");
            return -1;
        }

        Directory.CreateDirectory(args.OutputPath);

        await using var header = new StreamWriter(Path.Combine(args.OutputPath, "lib.h"));
        await using var source = new StreamWriter(Path.Combine(args.OutputPath, "lib.cxx"));
        await using var build = new StreamWriter(Path.Combine(args.OutputPath, "CMakeLists.txt"));
        
        await GenHeader(compilation, header).Watch("generate header");
        await GenSource(compilation, source).Watch("generate source");
        await GenBuild(compilation, build).Watch("generate CMakeLists.txt");

        return 0;
    }
}