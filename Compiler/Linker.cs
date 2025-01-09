using System.Buffers.Binary;
using System.ComponentModel;
using System.Numerics;

namespace MintCompiler
{
    public class Linker
    {
        private readonly Dictionary<string, byte[]> linkedLabels = [];

        private ushort numRefs = 0;
        private ushort nextFreeRef = 0;

        /// <summary>
        /// Links multiple compiled objects together.
        /// </summary>
        /// <param name="objects">List of compiled objects</param>
        /// <returns>Linked bytecode</returns>
        public List<byte> Link(List<CompiledObject> objects)
        {
            linkedLabels.Clear();
            numRefs = 0;
            nextFreeRef = 0;

            List<byte> output = [];

            foreach (CompiledObject obj in objects)
            {
                foreach (KeyValuePair<string, byte[]> label in obj.Labels)
                {
                    linkedLabels.TryAdd(label.Key, IntUtility.GetUInt16Bytes(nextFreeRef++));
                }

                numRefs += obj.NumPointers;
            }

            foreach (CompiledObject obj in objects)
            {
                Dictionary<ushort, byte[]> replaceIds = CheckReferences(obj.Labels);
                List<byte> bytes = LinkReferences(obj.Instructions, replaceIds);

                output.AddRange(bytes);
            }

            output.InsertRange(0, CreateMetadata());
            output.InsertRange(0, CreateHeader());
            output.Add((byte)Op.END_FILE);

            return output;
        }

        /// <summary>
        /// Deserializes and links multiple compiled objects together.
        /// </summary>
        /// <param name="files">List of object files</param>
        /// <returns>Linked bytecode</returns>
        public List<byte> Link(List<string> files)
        {
            linkedLabels.Clear();
            numRefs = 0;
            nextFreeRef = 0;

            List<CompiledObject> compiledObjects = [];
            foreach (string file in files)
            {
                compiledObjects.Add(Deserialize(File.ReadAllBytes(file)));
            }

            return Link(compiledObjects);
        }

        /// <summary>
        /// Deserializes bytecode.
        /// </summary>
        /// <param name="bytes">Raw bytecode</param>
        /// <returns>CompiledObject</returns>
        private static CompiledObject Deserialize(byte[] bytes)
        {
            ushort objNumRefs = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4, 2));

            ushort labelsCount = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(8, 2));
            Dictionary<string, byte[]> objLabels = [];

            int byteI = 10;

            for (int i = 0; i < labelsCount; i++)
            {
                byte[] labelId = bytes[byteI..(byteI+2)];
                byteI += 2;

                byte stringLen = bytes[byteI];
                byteI++;

                List<char> stringBytes = [];
                for (int j = 0; j < stringLen; j++)
                {
                    stringBytes.Add((char)bytes[byteI++]);
                }

                objLabels.Add(string.Join("", stringBytes), labelId);
            }

            List<byte> instructions = [];
            while (byteI < bytes.Length)
            {
                instructions.Add(bytes[byteI++]);
            }
            instructions.Add((byte)Op.END_FILE);

            return new([.. instructions], objLabels, objNumRefs);
        }

        /// <summary>
        /// Assembles the header, containing in order: signature (MINT or MINL), number of pointers (2 bytes),
        /// allocated memory size (4 bytes), and pointer to the main word (2 bytes).
        /// </summary>
        /// <returns>Header in list of bytes</returns>
        private List<byte> CreateHeader()
        {
            List<byte> header = [];
            // If the program doesn't contain a main word mark it as a library
            bool hasMainWord = linkedLabels.TryGetValue("main", out byte[]? mainPointer);
            if (hasMainWord)
            {
                header.AddRange("MINT"u8.ToArray());
            }
            else
            {
                header.AddRange("MINL"u8.ToArray());
            }

            header.AddRange(IntUtility.GetUInt16Bytes(numRefs));

            if (hasMainWord)
            {
#pragma warning disable CS8604 // Possible null reference argument.
                header.AddRange(mainPointer);
#pragma warning restore CS8604 // Possible null reference argument.
            }
            else
            {
                header.AddRange([0, 0]);
            }

            return header;
        }

        /// <summary>
        /// Creates label metadata, associating each label with its corresponding ID.
        /// </summary>
        /// <returns>Metadata in a list of bytes</returns>
        private List<byte> CreateMetadata()
        {
            List<byte> metadata = [];
            metadata.AddRange(IntUtility.GetUInt16Bytes((ushort)linkedLabels.Count));

            foreach (KeyValuePair<string, byte[]> label in linkedLabels)
            {
                metadata.AddRange(label.Value);
                metadata.Add((byte)label.Key.Length);
                foreach (char c in label.Key)
                {
                    metadata.Add((byte)c);
                }
            }

            return metadata;
        }

        /// <summary>
        /// Prepares raw data for linking by replacing internal IDs with their global counterparts.
        /// </summary>
        /// <param name="bytes">Raw object file data</param>
        /// <param name="replaceIds">Dictionary containing internal IDs and their replacements</param>
        /// <returns>Updated raw data</returns>
        private static List<byte> LinkReferences(byte[] bytes, Dictionary<ushort, byte[]> replaceIds)
        {
            List<byte> newBytes = [];

            int byteI = 0;
            int skipBytes = 0;

            // TODO: refactor this
            while (skipBytes > 0 || bytes[byteI] != (byte)Op.END_FILE)
            {
                // If we come across push instructions, skip the immediate values
                if (skipBytes == 0 && bytes[byteI] == (byte)Op.PUSH8) skipBytes = 2;
                else if (skipBytes == 0 && bytes[byteI] == (byte)Op.PUSH16) skipBytes = 3;
                else if (skipBytes == 0 && bytes[byteI] == (byte)Op.PUSH32) skipBytes = 5;
                else if (skipBytes == 0 && bytes[byteI] == (byte)Op.DEF)
                {
                    newBytes.Add(bytes[byteI++]); // instruction

                    byte type = bytes[byteI];
                    newBytes.Add(bytes[byteI++]); // type

                    byte[] idArr = [bytes[byteI++], bytes[byteI++]];
                    newBytes.AddRange(GetReplacementID(idArr, replaceIds));

                    byte[] sizeArr = [bytes[byteI], bytes[byteI+1]];
                    ushort size = BinaryPrimitives.ReadUInt16BigEndian(sizeArr);
                    newBytes.Add(bytes[byteI++]);
                    newBytes.Add(bytes[byteI++]);

                    if (type == 3)
                    {
                        for (int i = 0; i < size / 2; i++)
                        {
                            byte[] pointerId = [bytes[byteI++], bytes[byteI++]];
                            newBytes.AddRange(GetReplacementID(pointerId, replaceIds));
                        }
                    }

                    continue;
                }
                else if (skipBytes == 0 && bytes[byteI] == (byte)Op.PUSHPTR)
                {
                    newBytes.Add(bytes[byteI++]);

                    byte[] idArr = [bytes[byteI++], bytes[byteI++]];
                    newBytes.AddRange(GetReplacementID(idArr, replaceIds));

                    continue;
                }
                
                if (skipBytes > 0) skipBytes--;

                newBytes.Add(bytes[byteI++]);
            }

            return newBytes;
        }

        /// <summary>
        /// Gets a global replacement ID for a local one from a dictionary of replacements returned by CheckReferences.
        /// </summary>
        /// <param name="localId">Local ID</param>
        /// <param name="replaceIds">Replacement dictionary</param>
        /// <returns>Global ID</returns>
        private static byte[] GetReplacementID(byte[] localId, Dictionary<ushort, byte[]> replaceIds)
        {
            ushort id = BinaryPrimitives.ReadUInt16BigEndian(localId);
            if (replaceIds.TryGetValue(id, out byte[]? globalId))
            {
                return globalId;
            }
            else
            {
                return localId;
            }
        }

        /// <summary>
        /// Checks for discrepancies between a file's internal IDs and the chosen global IDs. If an internal ID refers
        /// to a definition that has already been linked, replace the internal ID with the global one.
        /// </summary>
        /// <param name="internalLabels">Internal IDs in a label-ID dictionary</param>
        /// <returns>Internal IDs to be replaced along with their global counterparts in byte array format</returns>
        private Dictionary<ushort, byte[]> CheckReferences(Dictionary<string, byte[]> internalLabels)
        {
            Dictionary<ushort, byte[]> replaceIds = [];

            foreach (KeyValuePair<string, byte[]> label in internalLabels)
            {
                if (linkedLabels.TryGetValue(label.Key, out byte[]? id)
                    && BinaryPrimitives.ReadUInt16BigEndian(id) != BinaryPrimitives.ReadUInt16BigEndian(label.Value))
                {
                    replaceIds.Add(BinaryPrimitives.ReadUInt16BigEndian(label.Value), id);
                }
            }

            return replaceIds;
        }
    }
}