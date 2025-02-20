using System;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using NetCasperSDK.Converters;
using Org.BouncyCastle.Utilities.Encoders;

namespace NetCasperSDK.Types
{
    public class CLValue
    {
        // Type of the value. Can be simple or constructed 
        [JsonPropertyName("cl_type")]
        [JsonConverter(typeof(CLTypeInfoConverter))]
        public CLTypeInfo TypeInfo { get; }

        // Byte array representation of underlying data
        [JsonPropertyName("bytes")]
        [JsonConverter(typeof(HexBytesConverter))]
        public byte[] Bytes { get; }

        // The optional parsed value of the bytes used when testing
        [JsonPropertyName("parsed")] public object Parsed { get; }

        public CLValue(byte[] bytes, CLType clType) :
            this(bytes, new CLTypeInfo(clType))
        {
        }

        public CLValue(byte[] bytes, CLType clType, object parsed)
            : this(bytes, new CLTypeInfo(clType), parsed)
        {
        }

        public CLValue(byte[] bytes, CLTypeInfo clType) :
            this(bytes, clType, null)
        {
        }
        
        public CLValue(string hexBytes, CLTypeInfo clType, object parsed)
            : this(Hex.Decode(hexBytes), clType, parsed)
        {
        }

        [JsonConstructor]
        public CLValue(byte[] bytes, CLTypeInfo typeInfo, object parsed)
        {
            TypeInfo = typeInfo;
            Bytes = bytes;

            // json deserializer may send a JsonElement
            // we can convert to string, number or null
            //
            if (parsed is JsonElement je)
            {
                Parsed = je.ValueKind switch
                {
                    JsonValueKind.String => je.GetString(),
                    JsonValueKind.Number => je.GetInt32(),
                    JsonValueKind.Null => null,
                    _ => je
                };
            }
            else
                Parsed = parsed;
        }

        public static CLValue Bool(bool value)
        {
            var bytes = new byte[] {value ? (byte) 0x01 : (byte) 0x00};
            return new CLValue(bytes, new CLTypeInfo(CLType.Bool), value.ToString());
        }

        public static CLValue I32(int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return new CLValue(bytes, CLType.I32, value);
        }

        public static CLValue I64(long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return new CLValue(bytes, CLType.I64, value);
        }

        public static CLValue U8(byte value)
        {
            byte[] bytes = new byte[1];
            bytes[0] = value;
            return new CLValue(bytes, CLType.U8, value);
        }

        public static CLValue U32(UInt32 value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return new CLValue(bytes, CLType.U32, value);
        }

        public static CLValue U64(UInt64 value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);

            return new CLValue(bytes, CLType.U64, value);
        }

        public static CLValue U128(BigInteger value)
        {
            byte[] bytes = value.ToByteArray();
            var len = bytes.Length;

            byte[] b = new byte[1 + len];
            b[0] = (byte) len;
            Array.Copy(bytes, 0, b, 1, len);
            return new CLValue(b, CLType.U128, value.ToString());
        }

        public static CLValue U256(BigInteger value)
        {
            byte[] bytes = value.ToByteArray();
            var len = bytes.Length;

            byte[] b = new byte[1 + len];
            b[0] = (byte) len;
            Array.Copy(bytes, 0, b, 1, len);
            return new CLValue(b, CLType.U256, value.ToString());
        }

        public static CLValue U512(BigInteger value)
        {
            byte[] bytes = value.ToByteArray();
            var len = bytes.Length;

            byte[] b = new byte[1 + len];
            b[0] = (byte) len;
            Array.Copy(bytes, 0, b, 1, len);
            return new CLValue(b, CLType.U512, value.ToString());
        }

        public static CLValue U512(UInt64 value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            int nonZeros;
            for (nonZeros = bytes.Length; nonZeros > 0; nonZeros--)
                if (bytes[nonZeros - 1] != 0x00)
                    break;

            byte[] b = new byte[1 + nonZeros];
            b[0] = (byte) nonZeros;
            Array.Copy(bytes, 0, b, 1, nonZeros);
            return new CLValue(b, CLType.U512, value.ToString());
        }

        public static CLValue Unit()
        {
            return new CLValue(Array.Empty<byte>(), CLType.Unit, null);
        }

        public static CLValue String(string value)
        {
            var bValue = System.Text.Encoding.UTF8.GetBytes(value);
            var bLength = BitConverter.GetBytes(bValue.Length);
            Debug.Assert(bLength.Length == 4, "It's expected that string length is encoded in 4 bytes");
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bLength);

            var bytes = new byte[4 + bValue.Length];
            Array.Copy(bLength, 0, bytes, 0, 4);
            Array.Copy(bValue, 0, bytes, 4, bValue.Length);

            return new CLValue(bytes, new CLTypeInfo(CLType.String), value);
        }

        public static CLValue URef(string value)
        {
            if (!value.StartsWith("uref-"))
                throw new ArgumentOutOfRangeException(nameof(value), "An URef object must start with 'uref-'.");

            var parts = value.Substring(5).Split(new char[] {'-'});
            if (parts.Length != 2)
                throw new ArgumentOutOfRangeException(nameof(value),
                    "An Uref object must end with an access rights suffix.");
            if (parts[0].Length != 64)
                throw new ArgumentOutOfRangeException(nameof(value), "An Uref object must contain a 32 byte value.");
            if (parts[1].Length != 3)
                throw new ArgumentOutOfRangeException(nameof(value),
                    "An Uref object must contain a 3 digits access rights suffix.");

            byte[] bytes = new byte[33];
            Array.Copy(Hex.Decode(parts[0]), 0, bytes, 0, 32);
            bytes[32] = (byte) uint.Parse(parts[1]);
            //return new CLValue(bytes, new CLURefTypeInfo(bytes[..31], (AccessRights)bytes[32]), value);
            return new CLValue(bytes, new CLTypeInfo(CLType.URef), value);
        }

        public static CLValue Option(CLValue innerValue)
        {
            byte[] bytes;
            if (innerValue == null)
            {
                bytes = new byte[1];
                bytes[0] = 0x00;
                
                return new CLValue(bytes, new CLOptionTypeInfo(null), "null");
            }
            else
            {
                bytes = new byte[1 + innerValue.Bytes.Length];
                bytes[0] = 0x01;
                Array.Copy(innerValue.Bytes, 0, bytes, 1, innerValue.Bytes.Length);  

                return new CLValue(bytes, new CLOptionTypeInfo(innerValue.TypeInfo), innerValue.Parsed);
            }
        }

        public static CLValue List(CLValue[] values)
        {
            if (values.Length == 0)
                throw new ArgumentOutOfRangeException(nameof(values), "Can't create instance for empty list");
            
            var ms = new MemoryStream();
            
            byte[] bytes = BitConverter.GetBytes(values.Length);
            if (!BitConverter.IsLittleEndian)
                Array.Reverse(bytes);
            ms.Write(bytes);

            var typeInfo = values[0].TypeInfo;
            foreach (var clValue in values)
            {
                ms.Write(clValue.Bytes);
                if(!clValue.TypeInfo.Equals(typeInfo))
                    throw new ArgumentOutOfRangeException(nameof(values), "A list cannot contain different types");
            }
                
            return new CLValue(ms.ToArray(), new CLListTypeInfo(typeInfo), "null");
        }

        public static CLValue ByteArray(byte[] bytes)
        {
            return new CLValue(bytes, new CLByteArrayTypeInfo(bytes.Length), Hex.ToHexString(bytes));
        }
        
        public static CLValue ByteArray(string hex)
        {
            var bytes = Hex.Decode(hex);
            return new CLValue(bytes, new CLByteArrayTypeInfo(bytes.Length), hex);
        }

        public static CLValue Tuple1(CLValue t0)
        {
            return new CLValue(t0.Bytes, new CLTuple1TypeInfo(t0.TypeInfo), t0.Parsed);
        }

        public static CLValue Tuple2(CLValue t0, CLValue t1)
        {
            var bytes = new byte[t0.Bytes.Length + t1.Bytes.Length];
            Array.Copy(t0.Bytes, 0, bytes, 0, t0.Bytes.Length);
            Array.Copy(t1.Bytes, 0, bytes, t0.Bytes.Length, t1.Bytes.Length);

            return new CLValue(bytes, new CLTuple2TypeInfo(t0.TypeInfo, t1.TypeInfo), Hex.ToHexString(bytes));
        }

        public static CLValue Tuple3(CLValue t0, CLValue t1, CLValue t2)
        {
            var bytes = new byte[t0.Bytes.Length + t1.Bytes.Length + t2.Bytes.Length];
            Array.Copy(t0.Bytes, 0, bytes, 0, t0.Bytes.Length);
            Array.Copy(t1.Bytes, 0, bytes, t0.Bytes.Length, t1.Bytes.Length);
            Array.Copy(t2.Bytes, 0, bytes, t0.Bytes.Length + t1.Bytes.Length, t2.Bytes.Length);

            return new CLValue(bytes, new CLTuple3TypeInfo(t0.TypeInfo, t1.TypeInfo, t2.TypeInfo),
                Hex.ToHexString(bytes));
        }
        
        public static CLValue PublicKey(byte[] value, KeyAlgo keyAlgorithm)
        {
            var bytes = new byte[1+value.Length];
            bytes[0] = (byte) keyAlgorithm;
            Array.Copy(value, 0, bytes, 1, value.Length);

            return new CLValue(bytes, new CLTypeInfo(CLType.PublicKey), Hex.ToHexString(bytes));
        }
        
        public static CLValue PublicKey(string value, KeyAlgo keyAlgorithm)
        {
            return PublicKey(Hex.Decode(value), keyAlgorithm);
        }
    }
}