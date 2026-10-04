#if !NET5_0_OR_GREATER
using System.ComponentModel;

namespace System.Runtime.CompilerServices;

/// <summary>
/// Polyfill that lets <c>init</c> accessors and records compile for target frameworks
/// whose base class library does not ship <c>IsExternalInit</c> (netstandard2.1).
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit
{
}
#endif
