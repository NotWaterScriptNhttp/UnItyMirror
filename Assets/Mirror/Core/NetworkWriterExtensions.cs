using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
    // Mirror's Weaver automatically detects all NetworkWriter function types,
    // but they do all need to be extensions.
    public static class NetworkWriterExtensions
    {
        public static void WriteByte(this NetworkWriter writer, byte value) => writer.WriteBlittable(value);
        public static void WriteByteNullable(this NetworkWriter writer, byte? value) => writer.WriteBlittableNullable(value);

        public static void WriteSByte(this NetworkWriter writer, sbyte value) => writer.WriteBlittable(value);
        public static void WriteSByteNullable(this NetworkWriter writer, sbyte? value) => writer.WriteBlittableNullable(value);

        // char is not blittable. convert to ushort.
        public static void WriteChar(this NetworkWriter writer, char value) => writer.WriteBlittable((ushort)value);
        public static void WriteCharNullable(this NetworkWriter writer, char? value) => writer.WriteBlittableNullable((ushort?)value);

        // bool is not blittable. convert to byte.
        public static void WriteBool(this NetworkWriter writer, bool value) => writer.WriteBlittable((byte)(value ? 1 : 0));
        public static void WriteBoolNullable(this NetworkWriter writer, bool? value) => writer.WriteBlittableNullable(value.HasValue ? ((byte)(value.Value ? 1 : 0)) : new byte?());

        public static void WriteShort(this NetworkWriter writer, short value) => writer.WriteBlittable(value);
        public static void WriteShortNullable(this NetworkWriter writer, short? value) => writer.WriteBlittableNullable(value);

        public static void WriteUShort(this NetworkWriter writer, ushort value) => writer.WriteBlittable(value);
        public static void WriteUShortNullable(this NetworkWriter writer, ushort? value) => writer.WriteBlittableNullable(value);

        public static void WriteInt(this NetworkWriter writer, int value) => writer.WriteBlittable(value);
        public static void WriteIntNullable(this NetworkWriter writer, int? value) => writer.WriteBlittableNullable(value);

        public static void WriteUInt(this NetworkWriter writer, uint value) => writer.WriteBlittable(value);
        public static void WriteUIntNullable(this NetworkWriter writer, uint? value) => writer.WriteBlittableNullable(value);

        public static void WriteLong(this NetworkWriter writer, long value)  => writer.WriteBlittable(value);
        public static void WriteLongNullable(this NetworkWriter writer, long? value) => writer.WriteBlittableNullable(value);

        public static void WriteULong(this NetworkWriter writer, ulong value) => writer.WriteBlittable(value);
        public static void WriteULongNullable(this NetworkWriter writer, ulong? value) => writer.WriteBlittableNullable(value);

        // WriteInt/UInt/Long/ULong writes full bytes by default.
        // define additional "VarInt" versions that Weaver will automatically prefer.
        // 99% of the time [SyncVar] ints are small values, which makes this very much worth it.
        [WeaverPriority] public static void WriteVarInt(this NetworkWriter writer, int value) => Compression.CompressVarInt(writer, value);
        [WeaverPriority] public static void WriteVarUInt(this NetworkWriter writer, uint value) => Compression.CompressVarUInt(writer, value);
        [WeaverPriority] public static void WriteVarLong(this NetworkWriter writer, long value) => Compression.CompressVarInt(writer, value);
        [WeaverPriority] public static void WriteVarULong(this NetworkWriter writer, ulong value) => Compression.CompressVarUInt(writer, value);

        public static void WriteFloat(this NetworkWriter writer, float value) => writer.WriteBlittable(value);
        public static void WriteFloatNullable(this NetworkWriter writer, float? value) => writer.WriteBlittableNullable(value);

        public static void WriteDouble(this NetworkWriter writer, double value) => writer.WriteBlittable(value);
        public static void WriteDoubleNullable(this NetworkWriter writer, double? value) => writer.WriteBlittableNullable(value);

        public static void WriteDecimal(this NetworkWriter writer, decimal value) => writer.WriteBlittable(value);
        public static void WriteDecimalNullable(this NetworkWriter writer, decimal? value) => writer.WriteBlittableNullable(value);

        public static void WriteHalf(this NetworkWriter writer, Half value) => writer.WriteUShort(value._value);

        public static void WriteString(this NetworkWriter writer, string value)
        {
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.
            if (value == null)
            {
                writer.WriteUShort(0);
                return;
            }

            // WriteString copies into the buffer manually.
            // need to ensure capacity here first, manually.
            int maxSize = writer.encoding.GetMaxByteCount(value.Length);
            writer.EnsureCapacity(writer.Position + 2 + maxSize); // 2 bytes position + N bytes encoding

            // encode it into the buffer first.
            // reserve 2 bytes for header after we know how much was written.
            int written = writer.encoding.GetBytes(value, 0, value.Length, writer.buffer, writer.Position + 2);

            // check if within max size, otherwise Reader can't read it.
            if (written > NetworkWriter.MaxStringLength)
                throw new IndexOutOfRangeException($"NetworkWriter.WriteString - Value too long: {written} bytes. Limit: {NetworkWriter.MaxStringLength} bytes");

            // .Position is unchanged, so fill in the size header now.
            // we already ensured that max size fits into ushort.max-1.
            writer.WriteUShort(checked((ushort)(written + 1))); // Position += 2

            // now update position by what was written above
            writer.Position += written;
        }

        // Weaver needs a write function with just one byte[] parameter
        // (we don't name it .Write(byte[]) because it's really a WriteBytesAndSize since we write size / null info too)
        public static void WriteBytesAndSize(this NetworkWriter writer, byte[] buffer)
        {
            // buffer might be null, so we can't use .Length in that case
            writer.WriteBytesAndSize(buffer, 0, buffer != null ? buffer.Length : 0);
        }

        // for byte arrays with dynamic size, where the reader doesn't know how many will come
        // (like an inventory with different items etc.)
        public static void WriteBytesAndSize(this NetworkWriter writer, byte[] buffer, int offset, int count)
        {
            // null is supported because [SyncVar]s might be structs with null byte[] arrays.
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.
            if (buffer == null)
            {
                // most sizes are small, write size as VarUInt!
                Compression.CompressVarUInt(writer, 0u);
                // writer.WriteUInt(0u);
                return;
            }
            // most sizes are small, write size as VarUInt!
            Compression.CompressVarUInt(writer, checked((uint)count) + 1u);
            // writer.WriteUInt(checked((uint)count) + 1u);
            writer.WriteBytes(buffer, offset, count);
        }

        // writes ArraySegment of byte (most common type) and size header
        public static void WriteArraySegmentAndSize(this NetworkWriter writer, ArraySegment<byte> segment)
        {
            writer.WriteBytesAndSize(segment.Array, segment.Offset, segment.Count);
        }

        // writes ArraySegment of any type, and size header
        public static void WriteArraySegment<T>(this NetworkWriter writer, ArraySegment<T> segment)
        {
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.
            //
            // ArraySegment technically can't be null, but users may call:
            // - WriteArraySegment
            // - ReadArray
            // in which case ReadArray needs null support. both need to be compatible.
            int count = segment.Count;
            // most sizes are small, write size as VarUInt!
            Compression.CompressVarUInt(writer, checked((uint)count) + 1u);
            // writer.WriteUInt(checked((uint)count) + 1u);
            for (int i = 0; i < count; i++)
            {
                writer.Write(segment.Array[segment.Offset + i]);
            }
        }

        public static void WriteGuid(this NetworkWriter writer, Guid value)
        {
#if !UNITY_2021_3_OR_NEWER
            // Unity 2019 doesn't have Span yet
            byte[] data = value.ToByteArray();
            writer.WriteBytes(data, 0, data.Length);
#else
            // WriteBlittable(Guid) isn't safe. see WriteBlittable comments.
            // Guid is Sequential, but we can't guarantee packing.
            // TryWriteBytes is safe and allocation free.
            writer.EnsureCapacity(writer.Position + 16);
            value.TryWriteBytes(new Span<byte>(writer.buffer, writer.Position, 16));
            writer.Position += 16;
#endif
        }
        public static void WriteGuidNullable(this NetworkWriter writer, Guid? value)
        {
            writer.WriteBool(value.HasValue);
            if (value.HasValue)
                writer.WriteGuid(value.Value);
        }

        // while SyncList<T> is recommended for NetworkBehaviours,
        // structs may have .List<T> members which weaver needs to be able to
        // fully serialize for NetworkMessages etc.
        // note that Weaver/Writers/GenerateWriter() handles this manually.
        public static void WriteList<T>(this NetworkWriter writer, List<T> list)
        {
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.
            if (list is null)
            {
                // most sizes are small, write size as VarUInt!
                Compression.CompressVarUInt(writer, 0u);
                // writer.WriteUInt(0);
                return;
            }

            // check if within max size, otherwise Reader can't read it.
            if (list.Count > NetworkReader.AllocationLimit)
                throw new IndexOutOfRangeException($"NetworkWriter.WriteList - List<{typeof(T)}> too big: {list.Count} elements. Limit: {NetworkReader.AllocationLimit}");

            // most sizes are small, write size as VarUInt!
            Compression.CompressVarUInt(writer, checked((uint)list.Count) + 1u);
            // writer.WriteUInt(checked((uint)list.Count) + 1u);
            for (int i = 0; i < list.Count; i++)
                writer.Write(list[i]);
        }

        // while SyncSet<T> is recommended for NetworkBehaviours,
        // structs may have .Set<T> members which weaver needs to be able to
        // fully serialize for NetworkMessages etc.
        // note that Weaver/Writers/GenerateWriter() handles this manually.
        public static void WriteHashSet<T>(this NetworkWriter writer, HashSet<T> hashSet)
        {
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.
            if (hashSet is null)
            {
                // most sizes are small, write size as VarUInt!
                Compression.CompressVarUInt(writer, 0u);
                //writer.WriteUInt(0);
                return;
            }

            // most sizes are small, write size as VarUInt!
            Compression.CompressVarUInt(writer, checked((uint)hashSet.Count) + 1u);
            //writer.WriteUInt(checked((uint)hashSet.Count) + 1u);
            foreach (T item in hashSet)
                writer.Write(item);
        }

        public static void WriteArray<T>(this NetworkWriter writer, T[] array)
        {
            // we offset count by '1' to easily support null without writing another byte.
            // encoding null as '0' instead of '-1' also allows for better compression
            // (ushort vs. short / varuint vs. varint) etc.
            if (array is null)
            {
                // most sizes are small, write size as VarUInt!
                Compression.CompressVarUInt(writer, 0u);
                // writer.WriteUInt(0);
                return;
            }

            // check if within max size, otherwise Reader can't read it.
            if (array.Length > NetworkReader.AllocationLimit)
                throw new IndexOutOfRangeException($"NetworkWriter.WriteArray - Array<{typeof(T)}> too big: {array.Length} elements. Limit: {NetworkReader.AllocationLimit}");

            // most sizes are small, write size as VarUInt!
            Compression.CompressVarUInt(writer, checked((uint)array.Length) + 1u);
            // writer.WriteUInt(checked((uint)array.Length) + 1u);
            for (int i = 0; i < array.Length; i++)
                writer.Write(array[i]);
        }

        public static void WriteUri(this NetworkWriter writer, Uri uri)
        {
            writer.WriteString(uri?.ToString());
        }

        public static void WriteDateTime(this NetworkWriter writer, DateTime dateTime)
        {
            writer.WriteDouble(dateTime.ToOADate());
        }

        public static void WriteDateTimeNullable(this NetworkWriter writer, DateTime? dateTime)
        {
            writer.WriteBool(dateTime.HasValue);
            if (dateTime.HasValue)
                writer.WriteDouble(dateTime.Value.ToOADate());
        }
    }
}
