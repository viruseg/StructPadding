using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace StructPadding.Tests;

[TestFixture]
public class ZeroerTests
{
    [StructLayout(LayoutKind.Sequential)]
    private struct SimplePadding
    {
        public byte A;
        // 3 bytes of padding
        public int B;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct TailPadding
    {
        public long A;
        public byte B;
        // 7 bytes of padding
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NestedPadding
    {
        public SimplePadding Inner; // There are 3 padding bytes inside
        public byte C;
        // There are 3 more padding bytes (alignment to 4 bytes)
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NoPadding
    {
        public int A;
        public int B;
    }


    [Test]
    public unsafe void Zero_SimpleStruct_ClearsInternalPaddingOnly()
    {
        var size = Unsafe.SizeOf<SimplePadding>();
        var buffer = new byte[size];

        Array.Fill(buffer, (byte) 0xFF);

        var structSpan = MemoryMarshal.Cast<byte, SimplePadding>(buffer.AsSpan());
        ref var simplePaddingStructFromBuffer = ref structSpan[0];

        simplePaddingStructFromBuffer.A = 0x11;
        simplePaddingStructFromBuffer.B = 0x22222222;

        Assert.That(buffer[1], Is.EqualTo(0xFF), "Pre-condition failed");

        Zeroer.Zero(ref simplePaddingStructFromBuffer);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(buffer[0], Is.EqualTo(0x11), "Field A touched"); // A
            Assert.That(buffer[1], Is.EqualTo(0), "Padding[1]"); // Pad
            Assert.That(buffer[2], Is.EqualTo(0), "Padding[2]"); // Pad
            Assert.That(buffer[3], Is.EqualTo(0), "Padding[3]"); // Pad
            Assert.That(buffer[4], Is.EqualTo(0x22), "Field B touched"); // B
        }

        var simplePaddingStruct = new SimplePadding
        {
            A = 0x11,
            B = 0x22222222
        };

        var simplePaddingStructAsSpan = new ReadOnlySpan<byte>(&simplePaddingStruct, sizeof(SimplePadding));

        Assert.That(simplePaddingStructAsSpan.SequenceEqual(MemoryMarshal.AsBytes(structSpan)), Is.True);
    }

    [Test]
    public unsafe void Zero_TailPadding_ClearsEndPadding()
    {
        var size = Unsafe.SizeOf<TailPadding>();
        var buffer = new byte[size];

        Array.Fill(buffer, (byte) 0xFF);

        var structSpan = MemoryMarshal.Cast<byte, TailPadding>(buffer.AsSpan());
        ref var tailPaddingStructFromBuffer = ref structSpan[0];

        tailPaddingStructFromBuffer.A = -1;
        tailPaddingStructFromBuffer.B = 0xAA;

        Zeroer.Zero(ref tailPaddingStructFromBuffer);

        Assert.That(buffer[8], Is.EqualTo(0xAA)); // B

        for (var i = 9; i < 16; i++)
        {
            Assert.That(buffer[i], Is.EqualTo(0), $"Padding at index {i}");
        }

        var tailPaddingStruct = new TailPadding
        {
            A = -1,
            B = 0xAA
        };

        var simplePaddingStructAsSpan = new ReadOnlySpan<byte>(&tailPaddingStruct, sizeof(TailPadding));

        Assert.That(simplePaddingStructAsSpan.SequenceEqual(MemoryMarshal.AsBytes(structSpan)), Is.True);
    }

    [Test]
    public unsafe void Zero_NestedStruct_ClearsAllPaddings()
    {
        var size = Unsafe.SizeOf<NestedPadding>();
        var buffer = new byte[size];

        Array.Fill(buffer, (byte) 0xFF);

        var structSpan = MemoryMarshal.Cast<byte, NestedPadding>(buffer.AsSpan());
        ref var str = ref structSpan[0];

        str.Inner.A = 1;
        str.Inner.B = 2;
        str.C = 3;

        Zeroer.Zero(ref str);

        using (Assert.EnterMultipleScope())
        {
            // Internal padding
            Assert.That(buffer[1], Is.EqualTo(0));
            Assert.That(buffer[2], Is.EqualTo(0));
            Assert.That(buffer[3], Is.EqualTo(0));

            // External padding (after C, offset 8)
            Assert.That(buffer[8], Is.EqualTo(3)); // Self
            Assert.That(buffer[9], Is.EqualTo(0)); // Pad
            Assert.That(buffer[10], Is.EqualTo(0)); // Pad
            Assert.That(buffer[11], Is.EqualTo(0)); // Pad
        }

        var nestedPaddingStruct = new NestedPadding
        {
            Inner = new SimplePadding
            {
                A = 1,
                B = 2
            },
            C = 3
        };

        var simplePaddingStructAsSpan = new ReadOnlySpan<byte>(&nestedPaddingStruct, sizeof(NestedPadding));

        Assert.That(simplePaddingStructAsSpan.SequenceEqual(MemoryMarshal.AsBytes(structSpan)), Is.True);
    }

    [Test]
    public void Zero_StructWithNoPadding_DoesNothing()
    {
        var val = new NoPadding { A = 123, B = 456 };
        var originalBytes = GetBytes(val);

        Zeroer.Zero(ref val);

        var newBytes = GetBytes(val);
        Assert.That(newBytes, Is.EqualTo(originalBytes));
    }

    [Test]
    public void ZeroArray_SpanOfStructs_ClearsPaddingInAllElements()
    {
        const int COUNT = 5;
        var array = new SimplePadding[COUNT];
        var arraySpan = array.AsSpan();

        var bytes = MemoryMarshal.AsBytes(arraySpan);
        bytes.Fill(0xFF);

        for (var i = 0; i < COUNT; i++)
        {
            ref var item = ref arraySpan[i];
            item.A = (byte) (i + 1);
            item.B = i + 1000;
        }

        Zeroer.ZeroArray(arraySpan);

        var structSize = Unsafe.SizeOf<SimplePadding>();

        for (var i = 0; i < COUNT; i++)
        {
            var baseOffset = i * structSize;

            using (Assert.EnterMultipleScope())
            {
                // Data check
                Assert.That(bytes[baseOffset], Is.EqualTo((byte)(i + 1)), $"Elem {i} Field A");

                // Padding check
                Assert.That(bytes[baseOffset + 1], Is.EqualTo(0), $"Elem {i} Pad 1");
                Assert.That(bytes[baseOffset + 2], Is.EqualTo(0), $"Elem {i} Pad 2");
                Assert.That(bytes[baseOffset + 3], Is.EqualTo(0), $"Elem {i} Pad 3");
            }
        }
    }

    [Test]
    public void ZeroArray_EmptySpan_DoesNotThrow()
    {
        Assert.DoesNotThrow(() => Zeroer.ZeroArray<SimplePadding>(Span<SimplePadding>.Empty));
    }

    private static byte[] GetBytes<T>(T value) where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        var arr = new byte[size];
        Unsafe.WriteUnaligned(ref arr[0], value);
        return arr;
    }
}