namespace MintCompiler
{
    public class CompiledObject(byte[] instructions, Dictionary<string, byte[]> labels, ushort numPointers)
    {
        public readonly byte[] Instructions = instructions;
        public readonly Dictionary<string, byte[]> Labels = labels;
        public readonly ushort NumPointers = numPointers;
    }
}