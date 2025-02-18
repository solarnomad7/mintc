using System.Buffers.Binary;

namespace MintCompiler
{
    public class MemoryRegion
    {
        public const int HeaderLength = 10;
        
        public List<byte> Data { get; set; } = [];
        public byte[] Id { get; set; }
        public ushort Size { get; }
        public ushort DataLength { get; }
        public List<ushort> PointerIndices { get; } = [];
        public RegionType Type { get; }

        private ushort numPointers = 0;

        public MemoryRegion(RegionType type, byte[] id, ushort initLen=0)
        {
            Type = type;
            Id = id;
            Size = (ushort)(initLen * (int)type);
        }

        /// <summary>
        /// Deserializes a memory region.
        /// </summary>
        /// <param name="bytes">Object file bytecode</param>
        /// <param name="startIdx">Region start index in the bytecode</param>
        public MemoryRegion(byte[] bytes, int startIdx)
        {
            byte[] regBytes = bytes[startIdx ..];

            Type = (RegionType)regBytes[0x01];
            Size = BinaryPrimitives.ReadUInt16BigEndian([regBytes[0x02], regBytes[0x03]]);
            DataLength = BinaryPrimitives.ReadUInt16BigEndian([regBytes[0x04], regBytes[0x05]]);
            Id = [regBytes[0x06], regBytes[0x07]];
            numPointers = BinaryPrimitives.ReadUInt16BigEndian([regBytes[0x08], regBytes[0x09]]);

            int byteI = 0x0A;
            for (int i = 0; i < numPointers; i++)
            {
                PointerIndices.Add(BinaryPrimitives.ReadUInt16BigEndian([regBytes[byteI++], regBytes[byteI++]]));
            }

            for (int i = 0; i < DataLength; i++)
            {
                Data.Add(regBytes[byteI++]);
            }
        }

        /// <summary>
        /// Adds a pointer at the current index.
        /// </summary>
        public void AddPointer()
        {
            PointerIndices.Add((ushort)Data.Count);
            numPointers++;
        }

        /// <summary>
        /// Serializes the memory region.
        /// </summary>
        /// <returns>Serialized bytes</returns>
        public List<byte> Serialize()
        {
            List<byte> serData = [(byte)Op.DEF, (byte)Type];
            ushort serSize = Size;

            if (serSize == 0)
            {
                serSize = (ushort)Data.Count;
            }
            serData.AddRange(IntUtility.GetUInt16Bytes(serSize));
            serData.AddRange(IntUtility.GetUInt16Bytes((ushort)Data.Count));
            serData.AddRange(Id);
            serData.AddRange(IntUtility.GetUInt16Bytes(numPointers));
            foreach (ushort pointerIdx in PointerIndices)
            {
                serData.AddRange(IntUtility.GetUInt16Bytes(pointerIdx));
            }
            serData.AddRange(Data);

            return serData;
        }
    }

    public enum RegionType { INT8 = 1, INT16 = 2, INT32 = 4 }
}