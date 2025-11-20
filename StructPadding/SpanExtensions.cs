namespace StructPadding;

/// <summary>
/// Extension methods for zeroing padding bytes in unmanaged structures.
/// </summary>
public static class SpanExtensions
{
    /// <typeparam name="T">The type of unmanaged structures in the span.</typeparam>
    /// <param name="array">The span of structures to process.</param>
    extension<T>(Span<T> array) where T : unmanaged
    {
        /// <summary>
        /// Resets all padding bytes in a span of unmanaged structures to zero.
        /// This is significantly faster than iterating and zeroing elements individually.
        /// </summary>
        public void ZeroPadding()
        {
            Zeroer.ZeroArray(array);
        }
    }
}