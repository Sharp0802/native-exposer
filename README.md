# NativeExposer

Do not write glue code yourself!

Preserving all API data,
NativeExposer generates glue codes automatically to host C# from C++,
without NativeAOT or IPC:

- C#

```csharp
class Program
{
    [Export]
    public Program() {}

    [Export]
    public void Hello() {
        Console.WriteLine("Hello, World");
    }
}
```

- C++

```cpp
Program program;
program.Hello();
```

## Getting Started

1. Install `NativeExposer.Build` to system

```
dotnet tool install -g NativeExposer.Build
```

It installs `native-exposer` build tool to your system.

2. Add `NativeExposer` to csproj as dependency

```
<PackageReference Include="NativeExposer" Version="1.0.0"/>
```

3. Use custom builder to generate stubs

```
native-exposer <csproj> <output-path>
```

It generates `CMakeLists.txt`, `lib.cxx` and `lib.h`.
You can use this `CMakeLists.txt` from your CMake project as `add_subdirectory` or etc...

4. Enjoy it!
