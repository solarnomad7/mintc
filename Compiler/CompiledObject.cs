using System.Buffers.Binary;

namespace MintCompiler
{
    public class CompiledObject
    {
        public bool Valid { get; }
        public ushort NumPointers { get; }
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

            NumPointers = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(0x04, 2));

            int byteI = 0x06;
            for (int i = 0; i < NumPointers; i++)
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

            for (int i = 0; i < NumPointers; i++)
            {
                MemoryRegion newRegion = new(bytes, byteI);
                Regions.Add(newRegion);
                byteI += newRegion.Length - 1;
            }

            Valid = true;
        }
    }
}