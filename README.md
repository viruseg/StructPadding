# StructPadding

**StructPadding** is a high-performance library for clearing (zeroing) padding bytes in unmanaged structures.

It ensures a deterministic memory state, which is critical for binary comparison (`memcmp`), hash calculation, and security sanitization.

## Features
- **Maximum Performance**: Uses System.Reflection.Emit (IL Generation) to create dynamic code specific to each structure.
- **Zero Allocation**: No memory allocations during method calls (after cache warmup).
- **Nested Support**: Correctly handles complex structures with nested unmanaged types.
- **Span-friendly**: Optimized processing for arrays and Span<T>.
- **Thread-Safe**: Thread-safe caching of memory layouts.

## Why is this needed?

In .NET (and other languages), structures are aligned in memory to optimize CPU access. This creates "holes" (padding) between fields containing random garbage.

Example structure:

```csharp
[StructLayout(LayoutKind.Sequential)]
struct Example
{
    public byte A; // 1 byte
    // --- 3 bytes of garbage (padding) ---
    public int B;  // 4 bytes
}
```

**Problems solved by StructPadding:**
1. Hashing: If you calculate a hash (CRC32, MD5, SHA) from the raw memory of a structure, garbage in the padding will result in different hashes for logically identical objects.
2. Comparison (memcmp): You cannot simply compare two memory blocks to determine if the structures are equal.
3. Security: Padding may contain residual data from RAM (passwords, keys) which could leak during serialization or memory dumps.

## Installation

```bash
dotnet add package StructPadding
```

## Usage

**Zeroing a single structure**

```csharp
using StructPadding;

[StructLayout(LayoutKind.Sequential)]
public struct MyData
{
    public byte Id;
    public long Value; // There will be 7 bytes of padding before this field
}

public void Example()
{
    MyData data = new MyData { Id = 1, Value = 100 };

    // Before zeroing: padding bytes contain garbage
    // After call: padding bytes are guaranteed to be 0
    Zeroer.Zero(ref data);
}
```

**Zeroing an Array or Span**

The `ZeroArray` method is optimized for processing arrays without unnecessary overhead. It generates code that iterates through memory linearly.

```csharp
public void ProcessBatch(Span<MyData> batch)
{
    // Fast clearing of the entire array/span
    Zeroer.ZeroArray(batch);
    
    // Now the batch can be safely passed to native code, 
    // saved to disk, or hashed.
}
```
or
```csharp
public void ProcessBatch(Span<MyData> batch)
{
    batch.ZeroPadding();
}
```

## Performance

The library analyzes the structure only once (upon first access). Based on this analysis, dynamic IL code (DynamicMethod) is generated to perform zero-writing directly at specific offsets.

This means:
- No reflection on the hot path.
- No foreach loops over field lists.
- Performance is comparable to manually written ptr[offset] = 0 code.

## Requirements
The type T must have the unmanaged constraint (structs containing only primitives or other unmanaged structs).

## License
MIT License. See LICENSE for details.