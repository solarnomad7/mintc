using System.Buffers.Binary;

namespace MintCompiler
{
    public class MemoryRegion
    {
        public List<byte> Data { get; set; } = [];
        public ushort Length { get; }
        public List<ushort> PointerIndices { get; }= [];
        
        private readonly RegionType type;
        private readonly byte[] id;
        private ushort numPointers = 0;

        public MemoryRegion(RegionType type, byte[] id, ushort initLen=0)
        {
            this.type = type;
            this.id = id;
            Length = initLen;
        }

        /// <summary>
        /// Deserializes a memory region.
        /// </summary>
        /// <param name="bytes">Object file bytecode</param>
        /// <param name="startIdx">Region start index in the bytecode</param>
        public MemoryRegion(byte[] bytes, int startIdx)
        {
            byte[] regBytes = bytes[startIdx ..];

            type = (RegionType)regBytes[0x01];
            Length = BinaryPrimitives.ReadUInt16BigEndian([regBytes[0x02], regBytes[0x03]]);
            id = [regBytes[0x04], regBytes[0x05]];
            numPointers = BinaryPrimitives.ReadUInt16BigEndian([regBytes[0x06], regBytes[0x07]]);

            int byteI = 0x08;
            for (int i = 0; i < numPointers; i++)
            {
                PointerIndices.Add(BinaryPrimitives.ReadUInt16BigEndian([regBytes[byteI++], regBytes[byteI++]]));
            }

            for (int i = 0; i < Length + 1; i++)
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
            List<byte> serData = [(byte)Op.DEF, (byte)type];
            ushort serLength = Length;

            if (serLength == 0)
            {
                serLength = (ushort)Data.Count;
            }
            serData.AddRange(IntUtility.GetUInt16Bytes(serLength));
            serData.AddRange(id);
            serData.AddRange(IntUtility.GetUInt16Bytes(numPointers));
            foreach (ushort pointerIdx in PointerIndices)
            {
                serData.AddRange(IntUtility.GetUInt16Bytes(pointerIdx));
            }
            serData.AddRange(Data);
            serData.Add((byte)Op.END);

            return serData;
        }
    }

    public enum RegionType { INT8 = 1, INT16 = 2, INT32 = 4 }
}