using System.Buffers.Binary;
using System.ComponentModel;
using System.Numerics;

namespace MintCompiler
{
    public class Linker
    {
        private readonly Dictionary<string, byte[]> linkedLabels = new()
        {
            {"NULL", IntUtility.GetUInt16Bytes(0)},
            {"PAGE", IntUtility.GetUInt16Bytes(1)},
            {"PAGE.src", IntUtility.GetUInt16Bytes(2)},
            {"PAGE.rwsize", IntUtility.GetUInt16Bytes(3)},
            {"TERMINAL", IntUtility.GetUInt16Bytes(4)},
            {"TERMINAL.args", IntUtility.GetUInt16Bytes(5)},
            {"TERMINAL.readc", IntUtility.GetUInt16Bytes(6)},
            {"TERMINAL.writec", IntUtility.GetUInt16Bytes(7)},
            {"FS", IntUtility.GetUInt16Bytes(8)},
            {"FS.path", IntUtility.GetUInt16Bytes(9)},
            {"FS.source", IntUtility.GetUInt16Bytes(10)},
            {"FS.dest", IntUtility.GetUInt16Bytes(11)},
        };
        private const int reservedPointers = 12;

        private ushort numRefs = 0;
        private ushort nextFreeRef = 0;

        /// <summary>
        /// Links multiple compiled objects together.
        /// </summary>
        /// <param name="objects">List of compiled objects</param>
        /// <returns>Linked bytecode</returns>
        public List<byte> Link(List<CompiledObject> objects)
        {
            numRefs = 0;
            nextFreeRef = reservedPointers;

            List<byte> output = [];

            foreach (CompiledObject obj in objects)
            {
                foreach (KeyValuePair<string, byte[]> label in obj.Labels)
                {
                    linkedLabels.TryAdd(label.Key, IntUtility.GetUInt16Bytes(nextFreeRef++));
                }

                numRefs += obj.NumRegions;
            }

            foreach (CompiledObject obj in objects)
            {
                if (obj.Valid)
                {
                    Dictionary<ushort, byte[]> replaceIds = CheckReferences(obj.Labels);
                    LinkReferences(obj, replaceIds);
                    
                    foreach (MemoryRegion region in obj.Regions)
                    {
                        output.AddRange(region.Serialize());
                    }
                }
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
            numRefs = 0;
            nextFreeRef = 0;

            List<CompiledObject> compiledObjects = [];
            foreach (string file in files)
            {
                compiledObjects.Add(new CompiledObject(file));
            }

            return Link(compiledObjects);
        }

        /// <summary>
        /// Assembles the header, containing in order: signature, number of pointers (2 bytes),
        /// allocated memory size (4 bytes), and pointer to the main word (2 bytes).
        /// </summary>
        /// <returns>Header in list of bytes</returns>
        private List<byte> CreateHeader()
        {
            List<byte> header = [];
            if (!linkedLabels.TryGetValue("main", out byte[]? mainPointer))
            {
                throw new NoMainWordException();
            }

            header.AddRange("MINT"u8.ToArray());
            header.AddRange(IntUtility.GetUInt16Bytes(numRefs));

#pragma warning disable CS8604 // Possible null reference argument.
            header.AddRange(mainPointer);
#pragma warning restore CS8604 // Possible null reference argument.

            return header;
        }

        /// <summary>
        /// Creates label metadata, associating each label with its corresponding ID.
        /// </summary>
        /// <returns>Metadata in a list of bytes</returns>
        private List<byte> CreateMetadata()
        {
            List<byte> metadata = [];
            metadata.AddRange(IntUtility.GetUInt16Bytes((ushort)(linkedLabels.Count - reservedPointers)));

            foreach (KeyValuePair<string, byte[]> label in linkedLabels)
            {
                if (BinaryPrimitives.ReadUInt16BigEndian(label.Value) > reservedPointers - 1)
                {
                    metadata.AddRange(label.Value);
                    metadata.Add((byte)label.Key.Length);
                    foreach (char c in label.Key)
                    {
                        metadata.Add((byte)c);
                    }
                }
            }

            return metadata;
        }

        /// <summary>
        /// Prepares raw data for linking by replacing internal IDs with their global counterparts.
        /// </summary>
        /// <param name="obj">Deserialized object</param>
        /// <param name="replaceIds">Dictionary containing internal IDs and their replacements</param>
        private static void LinkReferences(CompiledObject obj, Dictionary<ushort, byte[]> replaceIds)
        {
            foreach (MemoryRegion region in obj.Regions)
            {
                region.Id = GetReplacementID(region.Id, replaceIds);
                foreach (ushort idx in region.PointerIndices)
                {
                    byte[] localId = [.. region.Data.GetRange(idx, 2)];
                    byte[] globalId = GetReplacementID(localId, replaceIds);

                    region.Data[idx] = globalId[0];
                    region.Data[idx+1] = globalId[1];
                }
            }
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