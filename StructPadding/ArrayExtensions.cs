using System.Runtime.CompilerServices;

namespace StructPadding;

/// <summary>
/// Extension methods for zeroing padding bytes in arrays of unmanaged structures.
/// </summary>
public static class ArrayExtensions
{
    /// <typeparam name="T">The type of unmanaged structures in the span.</typeparam>
    /// <param name="array">The span of structures to process.</param>
    extension<T>(Span<T> array) where T : unmanaged
    {
        /// <summary>
        /// Resets all padding bytes in a span of unmanaged structures to zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ZeroPadding()
        {
            Zeroer.ZeroArray(array);
        }
    }

    /// <typeparam name="T">The type of unmanaged structures in the span.</typeparam>
    /// <param name="array">The span of structures to process.</param>
    extension<T>(T[] array) where T : unmanaged
    {
        /// <summary>
        /// Resets all padding bytes in a span of unmanaged structures to zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ZeroPadding()
        {
            Zeroer.ZeroArray(array);
        }
    }
}