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
        progress.ProgressChanged += (_, args) =>
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

    private static IEnumerable<INamedTypeSymbol> GetExportTargets(Compilation compilation)
    {
        return compilation
            .Assembly
            .GlobalNamespace
            .GetAllType()
            .Where(t => t.IsExported());
    }

    private static void WriteMethod(IMethodSymbol method, string? prefix, CodeWriter writer)
    {
        writer.Write(method.IsCtor()
            ? $"{prefix}{method.ContainingType.Name}("
            : $"{method.ReturnType.ToNativeType()} {prefix}{method.Name}(");

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (i > 0)
                writer.Write(", ");
            writer.Write($"{method.Parameters[i].Type.ToNativeType()} {method.Parameters[i].Name}");
        }

        writer.Write(")");
    }


    private static async Task GenHeader(Compilation compilation, CodeWriter writer)
    {
        var boilerplate = await Resources.Embedded("lib.h");
        writer.WriteLine(boilerplate);

        foreach (var type in GetExportTargets(compilation))
        {
            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                writer.WriteLine($"namespace {type.ContainingNamespace.GetFullName("::")} {{");
                writer.Indent++;
            }

            writer.WriteLine(
                $$"""
                  class {{type.Name}} {
                    ::std::intptr_t _handle;

                  public:
                    {{type.Name}}(const {{type.Name}}&) = delete;
                    {{type.Name}} &operator =(const {{type.Name}}&) = delete;
                    ~{{type.Name}}();
                  """);
            writer.Indent++;

            foreach (var method in type
                         .GetMembers()
                         .Where(m => m.IsExported())
                         .OfType<IMethodSymbol>())
            {
                writer.Write("CLR_CALL ");
                WriteMethod(method, null, writer);
                writer.WriteLine(";");
            }

            writer.Indent--;
            writer.WriteLine("};");

            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                writer.Indent--;
                writer.WriteLine("}");
            }
        }
    }

    private static void WriteMethodBody(IMethodSymbol method, CodeWriter writer, bool mangle = true)
    {
        writer.Write("thread_local ");
        writer.Write(method.IsCtor()
            ? method.ContainingType.ToNativeBridgeType()
            : method.ReturnType.ToNativeBridgeType());

        writer.Write(" (MANAGED_CALL *_fp)(");

        if (!method.IsStatic && !method.IsCtor())
        {
            writer.Write("::std::intptr_t");
            if (method.Parameters.Any())
                writer.Write(", ");
        }

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (i > 0)
                writer.Write(", ");
            writer.Write(method.Parameters[i].Type.ToNativeBridgeType());
        }

        writer.WriteLine(");");

        var typeName   = method.ContainingType.GetFullyQualifiedName();
        var methodName = mangle ? method.Mangle() : method.Name;

        writer.WriteLine(
            // synchronization is required with no thread-local
            $$"""
              if (!_fp) {
                int r = ::clr::get_function_pointer("{{typeName}}", "{{methodName}}", UNMANAGEDCALLERSONLY_METHOD, nullptr, nullptr, reinterpret_cast<void**>(&_fp));
                ::clr::assert(static_cast<clr::StatusCode>(r));
              }
              """);

        if (!method.ReturnsVoid)
            writer.Write("return ");
        else if (method.IsCtor())
            writer.Write("_handle = ");

        writer.Write("_fp(");

        if (!method.IsStatic && !method.IsCtor())
        {
            writer.Write("_handle");
            if (method.Parameters.Any())
                writer.Write(", ");
        }

        for (var i = 0; i < method.Parameters.Length; i++)
        {
            if (i > 0)
                writer.Write(", ");
            writer.Write(method.Parameters[i].Name);
        }

        writer.WriteLine(");");
    }

    private static async Task GenSource(Compilation compilation, CodeWriter writer)
    {
        var boilerplate = await Resources.Embedded("lib.cxx");
        writer.WriteLine(boilerplate);

        var internals = compilation
            .Assembly
            .GlobalNamespace
            .GetAllType()
            .Single(t => t.GetFullName(".") == "NativeExposer.Internal");
        var free = internals
            .GetMembers()
            .OfType<IMethodSymbol>()
            .Single(m => m.Name == "Free");

        foreach (var type in GetExportTargets(compilation))
        {
            writer.WriteLine($"{type.GetFullName("::")}::~{type.Name}() {{");
            writer.Indent++;
            WriteMethodBody(free, writer, false);
            writer.Indent--;
            writer.WriteLine("}");

            foreach (var method in type
                         .GetMembers()
                         .Where(m => m.IsExported())
                         .OfType<IMethodSymbol>())
            {
                writer.Write("\n");
                WriteMethod(method, method.ContainingSymbol.GetFullName("::") + "::", writer);
                writer.WriteLine("{");
                writer.Indent++;
                WriteMethodBody(method, writer);
                writer.Indent--;
                writer.WriteLine("}");
            }
        }
    }

    private static string Encode(string str)
    {
        return new string(str.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
    }

    private static async Task GenBuild(Compilation compilation, CodeWriter writer)
    {
        var assembly = compilation.ObjectType.ContainingAssembly;
        var version  = assembly.Identity.Version;

        var name = Encode(compilation.AssemblyName ?? "");

        var boilerplate = await Resources.Embedded("CMakeLists.txt");

        boilerplate = boilerplate
            .Replace("@LIBRARY@", name)
            .Replace("@DOTNET_RUNTIME_VERSION@", version.ToString(3));

        foreach (var line in boilerplate.Split('\n'))
        {
            if (line.StartsWith("##"))
                continue;

            writer.WriteLine(line);
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
        Progress.Instance.Barrier();
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
        await using var build  = new StreamWriter(Path.Combine(args.OutputPath, "CMakeLists.txt"));

        var headerCode = new CodeWriter(header);
        var sourceCode = new CodeWriter(source);
        var buildCode  = new CodeWriter(build);

        await GenHeader(compilation, headerCode).Watch("generate header");
        await GenSource(compilation, sourceCode).Watch("generate source");
        await GenBuild(compilation, buildCode).Watch("generate CMakeLists.txt");

        return 0;
    }
}