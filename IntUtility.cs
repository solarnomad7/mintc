using System.Buffers.Binary;

namespace MintCompiler
{
    public static class IntUtility
    {
        /// <summary>
        /// Converts a 16-bit integer to a byte array.
        /// </summary>
        /// <param name="num">Short integer</param>
        /// <returns>Byte array</returns>
        public static byte[] GetInt16Bytes(short num)
        {
            byte[] iBytes = new byte[2];
            BinaryPrimitives.WriteInt16BigEndian(iBytes, num);
            return iBytes;
        }

        /// <summary>
        /// Converts a 32-bit integer to a byte array.
        /// </summary>
        /// <param name="num">Integer</param>
        /// <returns>Byte array</returns>
        public static byte[] GetInt32Bytes(int num)
        {
            byte[] iBytes = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(iBytes, num);
            return iBytes;
        }

        /// <summary>
        /// Converts a 16-bit unsigned integer to a byte array.
        /// </summary>
        /// <param name="num">Short integer</param>
        /// <returns>Byte array</returns>
        public static byte[] GetUInt16Bytes(ushort num)
        {
            byte[] iBytes = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(iBytes, num);
            return iBytes;
        }

        /// <summary>
        /// Converts a 32-bit unsigned integer to a byte array.
        /// </summary>
        /// <param name="num">Integer</param>
        /// <returns>Byte array</returns>
        public static byte[] GetUInt32Bytes(uint num)
        {
            byte[] iBytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(iBytes, num);
            return iBytes;
        }

        /// <summary>
        /// Gets the smallest possible bit size of an integer.
        /// </summary>
        /// <param name="num">Integer</param>
        /// <returns>Bit size (8, 16, or 32)</returns>
        public static IntSize? GetIntSize(int num)
        {
            if (num < Byte.MaxValue && num > Byte.MinValue)
            {
                return IntSize.INT8;
            }
            else if (num < Int16.MaxValue && num > Int16.MinValue)
            {
                return IntSize.INT16;
            }
            else if (num < Int32.MaxValue && num > Int32.MinValue)
            {
                return IntSize.INT32;
            }
            return null;
        }
    }

    public enum IntSize { INT8 = 8, INT16 = 16, INT32 = 32 };
}