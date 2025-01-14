using System.Buffers.Binary;

namespace MintCompiler
{
    public class CompiledObject
    {
        public bool Valid { get; }
        public ushort NumRegions { get; }
        public Dictionary<string, byte[]> Labels { get; } = [];
        public List<MemoryRegion> Regions { get; } = [];

        /// <summary>
        /// Deserialize a compiler object file.
        /// </summary>
        /// <param name="filename">Path to the object file</param>
        public CompiledObject(string filename)
        {
            byte[] bytes = File.ReadAllBytes(filename);

            if (bytes[0x00] != (byte)'M' && bytes[0x01] != (byte)'O' && bytes[0x02] != (byte)'B' && bytes[0x03] != (byte)'J')
            {
                Valid = false;
                return;
            }

            NumRegions = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(0x04, 2));
            ushort numLabels = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(0x06, 2));

            int byteI = 0x08;
            for (int i = 0; i < numLabels; i++)
            {
                byte[] id = [bytes[byteI++], bytes[byteI++]];
                int len = bytes[byteI++];

                char[] label = new char[len];
                for (int j = 0; j < len; j++)
                {
                    label[j] = (char)bytes[byteI++];
                }

                Labels.Add(string.Join("", label), id);
            }

            for (int i = 0; i < NumRegions; i++)
            {
                MemoryRegion newRegion = new(bytes, byteI);
                Regions.Add(newRegion);
                byteI += newRegion.DataLength + MemoryRegion.HeaderLength + (newRegion.PointerIndices.Count * 2);
            }
            
            Valid = true;
        }
    }
}