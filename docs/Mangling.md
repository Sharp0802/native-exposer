# Mangling

Managed-side stub uses mangled name of method,
Because overloaded `UnmanagedCallersOnly` methods cannot be distinguished.

## Format

```
_N<name>E[t]<mangle-code>
```

- `<name>` is method name
- `t` is for instance functions
- `<mangle-code>` is for parameters

## Examples

```
public int Foo(int a, float b); -> _NFooEtif
```

- `_N` : Standard prefix
- `Foo` : Method name
- `E` : Standard delimiter
- `t` : Mark for instance functions
- `i` : Mark for integer parameter
- `f` : Mark for float parameter

```
public static void Bar(int a, float b); -> _NBarEif
```

- `_N` : Standard prefix
- `Bar` : Method name
- `E` : Standard delimiter
- `i` : Mark for integer parameter
- `f` : Mark for float parameter

## Mangle Code

|              Type | Mangle Code                  | Full Name |
|------------------:|:-----------------------------|:----------|
|   Reference Types | `p`                          | pointer   |
|    `System.SByte` | `c`                          | char      |
|    `System.Int16` | `s`                          | short     |
|    `System.Int32` | `i`                          | int       |
|    `System.Int64` | `l`                          | long      |
|     `System.Byte` | `b`                          | byte      |
|   `System.UInt16` | `w`                          | word      |
|   `System.UInt32` | `u`                          | uint      |
|   `System.UInt64` | `q`                          | qword     |
|     `System.Half` | `h`                          | half      |
|   `System.Single` | `f`                          | float     |
|   `System.Double` | `d`                          | double    |
| Other Value Types | First character of type name |           |
