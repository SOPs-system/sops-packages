// .NET Framework 4.8 lacks a few compiler-support types that C# 12 syntax
// (init-only setters, records) expects. Declaring them here is the
// documented way to use that syntax on net48.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
