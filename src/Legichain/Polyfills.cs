// Polyfills for C# language features that the runtime doesn't ship in
// netstandard2.0. Compiled away on net8+.
#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices
{
    /// <summary>Required for records / <c>init</c>-only setters on netstandard2.0.</summary>
    internal static class IsExternalInit { }
}
#endif
