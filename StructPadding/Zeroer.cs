using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using ZLinq;

namespace StructPadding;

/// <summary>
/// Provides high-performance methods for zeroing memory padding in unmanaged structures.
/// </summary>
public static class Zeroer
{
    private unsafe delegate void ZeroAction(byte* ptr);

    private static readonly ConcurrentDictionary<Type, ZeroAction?> cache = new();

    /// <summary>
    /// Resets all padding bytes in the specified unmanaged structure reference to zero.
    /// </summary>
    /// <typeparam name="T">The type of the unmanaged structure. Must not be a primitive type.</typeparam>
    /// <param name="value">A reference to the structure instance to be sanitized.</param>
    public static unsafe void Zero<T>(ref T value) where T : unmanaged
    {
        var action = cache.GetOrAdd(typeof(T), CreateZeroer);

        if (action == null) return;

        fixed (T* ptr = &value)
        {
            action((byte*) ptr);
        }
    }

    /// <summary>
    /// Resets all padding bytes in a span of unmanaged structures to zero.
    /// </summary>
    /// <typeparam name="T">The type of unmanaged structures in the span.</typeparam>
    /// <param name="array">The span of structures to process.</param>
    public static unsafe void ZeroArray<T>(Span<T> array) where T : unmanaged
    {
        if (array.IsEmpty) return;

        var action = cache.GetOrAdd(typeof(T), CreateZeroer);

        if (action == null) return;

        fixed (T* ptr = array)
        {
            var bytePtr = (byte*) ptr;
            var stride = sizeof(T);
            var count = array.Length;

            for (var i = 0; i < count; i++)
            {
                action(bytePtr);
                bytePtr += stride;
            }
        }
    }

    private static ZeroAction? CreateZeroer(Type type)
    {
        var regions = AnalyzePadding(type);

        if (regions.Count == 0) return null;

        var method = new DynamicMethod($"ZeroPadding_{type.Name}",
                                       null,
                                       [ typeof(byte*) ],
                                       typeof(Zeroer).Module,
                                       true);

        var il = method.GetILGenerator();

        foreach (var region in regions)
        {
            switch (region.Length)
            {
                case 1:
                    il.Emit(OpCodes.Ldarg_0); // push ptr
                    il.Emit(OpCodes.Ldc_I4, region.Offset); // push offset
                    il.Emit(OpCodes.Add); // ptr + offset
                    il.Emit(OpCodes.Ldc_I4_0); // 0
                    il.Emit(OpCodes.Stind_I1); // *(ptr+offset) = (byte) 0
                    break;

                case 2:
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4, region.Offset);
                    il.Emit(OpCodes.Add);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Stind_I2); // *(short*) = 0
                    break;

                case 4:
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4, region.Offset);
                    il.Emit(OpCodes.Add);
                    il.Emit(OpCodes.Ldc_I4_0);
                    il.Emit(OpCodes.Stind_I4); // *(int*) = 0
                    break;

                case 8:
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4, region.Offset);
                    il.Emit(OpCodes.Add);
                    il.Emit(OpCodes.Ldc_I8, 0L);
                    il.Emit(OpCodes.Stind_I8); // *(long*) = 0
                    break;

                default:
                    il.Emit(OpCodes.Ldarg_0);
                    il.Emit(OpCodes.Ldc_I4, region.Offset);
                    il.Emit(OpCodes.Add); // Destination address
                    il.Emit(OpCodes.Ldc_I4_0); // Value (0)
                    il.Emit(OpCodes.Ldc_I4, region.Length); // Size
                    il.Emit(OpCodes.Initblk); // memset
                    break;
            }
        }

        il.Emit(OpCodes.Ret);

        return (ZeroAction) method.CreateDelegate(typeof(ZeroAction));
    }

    private struct PaddingRegion
    {
        public int Offset;
        public int Length;
    }

    private static List<PaddingRegion> AnalyzePadding(Type type)
    {
        var regions = new List<PaddingRegion>(8);
        var structSize = Marshal.SizeOf(type);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                         .AsValueEnumerable()
                         .Select(f => new
                         {
                             Field = f,
                             Offset = (int) Marshal.OffsetOf(type, f.Name),
                             Size = Marshal.SizeOf(f.FieldType)
                         })
                         .OrderBy(f => f.Offset)
                         .ToList();

        if (fields.Count == 0) return regions;

        for (var i = 0; i < fields.Count - 1; i++)
        {
            var end = fields[i].Offset + fields[i].Size;
            var next = fields[i + 1].Offset;
            if (next > end)
                regions.Add(new PaddingRegion
                {
                    Offset = end,
                    Length = next - end
                });
        }

        var last = fields[^1];
        var lastEnd = last.Offset + last.Size;

        if (lastEnd < structSize)
            regions.Add(new PaddingRegion
            {
                Offset = lastEnd,
                Length = structSize - lastEnd
            });

        foreach (var f in fields)
        {
            if (f.Field.FieldType is not { IsValueType: true, IsPrimitive: false, IsEnum: false }) continue;

            var nested = AnalyzePadding(f.Field.FieldType);

            foreach (var n in nested)
                regions.Add(n with { Offset = f.Offset + n.Offset });
        }

        return regions;
    }
}