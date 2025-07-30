using System.Text;
using Microsoft.CodeAnalysis;

namespace NativeExposer;

public static class SymbolExtension
{
    public static string GetFullName(this ISymbol symbol, string delimiter)
    {
        var queue = new Stack<string>();
        while (symbol is INamespaceOrTypeSymbol or IMethodSymbol)
        {
            if (!string.IsNullOrWhiteSpace(symbol.Name))
                queue.Push(symbol.Name);
            symbol = symbol.ContainingSymbol;
        }

        var builder = new StringBuilder();
        for (var first = true; queue.Count > 0; first = false)
        {
            if (!first)
                builder.Append(delimiter);
            builder.Append(queue.Pop());
        }

        return builder.ToString();
    }

    private static char Mangle(this ITypeSymbol type)
    {
        if (type.IsReferenceType)
            return 'p';

        return type.GetFullName(".") switch
        {
            "System.SByte" => 'c', // char
            "System.Int16" => 's', // short
            "System.Int32" => 'i', // int
            "System.Int64" => 'l', // long

            "System.Byte"   => 'b', // byte
            "System.UInt16" => 'w', // word
            "System.UInt32" => 'u', // uint
            "System.UInt64" => 'q', // qword

            "System.Half"   => 'h', // half
            "System.Single" => 'f', // float
            "System.Double" => 'd', // double

            _ => type.Name[0]
        };
    }

    public static string Mangle(this IMethodSymbol symbol)
    {
        var builder = new StringBuilder();
        builder.Append("_N").Append(symbol.Name).Append("E");

        if (!symbol.IsStatic) 
            builder.Append('t');
        
        foreach (var param in symbol.Parameters) 
            builder.Append(param.Type.Mangle());
        
        return builder.ToString();
    }

    public static string ToNativeType(this ITypeSymbol type)
    {
        return type.GetFullName(".") switch
        {
            "System.SByte" => "::std::int8_t",
            "System.Int16" => "::std::int16_t",
            "System.Int32" => "::std::int32_t",
            "System.Int64" => "::std::int64_t",

            "System.Byte"   => "::std::uint8_t",
            "System.UInt16" => "::std::uint16_t",
            "System.UInt32" => "::std::uint32_t",
            "System.UInt64" => "::std::uint64_t",

            "System.Half"   => "::std::float16_t",
            "System.Single" => "::std::float32_t",
            "System.Double" => "::std::float64_t",

            _ => "::" + type.GetFullName("::")
        };
    }

    public static string ToBridgeType(this ITypeSymbol type)
    {
        if (type.IsReferenceType)
            return "global::System.IntPtr";
        
        return "global::" + type.GetFullName(".");
    }

    private static bool IsRootNamespace(ISymbol symbol) 
    {
        INamespaceSymbol? s;
        return (s = symbol as INamespaceSymbol) != null && s.IsGlobalNamespace;
    }

    public static string GetFullyQualifiedName(this ISymbol? s)
    {
        if (s == null || IsRootNamespace(s))
        {
            return string.Empty;
        }
        
        var asm = s.ContainingAssembly;

        var sb   = new StringBuilder(s.MetadataName);
        var last = s;

        s = s.ContainingSymbol;

        while (!IsRootNamespace(s))
        {
            if (s is ITypeSymbol && last is ITypeSymbol)
            {
                sb.Insert(0, '+');
            }
            else
            {
                sb.Insert(0, '.');
            }

            sb.Insert(0, s.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
            //sb.Insert(0, s.MetadataName);
            s = s.ContainingSymbol;
        }

        return $"{sb}, {asm}";
    }
}