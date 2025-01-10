using System.Buffers.Binary;
using System.Text;

namespace MintCompiler
{
    public class Compiler(Lexer lexer)
    {
        private readonly Lexer lexer = lexer;

        private readonly List<byte> bytes = [];
        private readonly Dictionary<string, byte[]> labels = [];

        private ushort numPointers = 0;
        private uint undefinedRefs = 0;
        private int wordDefStartIdx = -1;
        private string wordDefName = "";

        /// <summary>
        /// Assembles Mint code into bytecode given a Lexer object.
        /// </summary>
        /// <returns>CompiledObject</returns>
        public CompiledObject Assemble()
        {
            lexer.Tokenize();

            GetLabels();
            AssembleInstructions();
            bytes.Add((byte)Op.END_FILE);

            return new CompiledObject([.. bytes], labels, numPointers);
        }

        /// <summary>
        /// Loops through tokens produced by the Lexer and assembles them.
        /// </summary>
        private void AssembleInstructions()
        {
            Token token = lexer.NextToken();
            while (token.Type != TokenType.EOF)
            {
                switch (token.Type)
                {
                    case TokenType.KEYWORD:
                        HandleArrayDef(token.Content);
                        break;
                    case TokenType.WORD_DEF:
                        wordDefStartIdx = bytes.Count;
                        wordDefName = token.Content;
                        break;
                    case TokenType.LITERAL_NUM:
                        AssembleLiteralNum(Convert.ToInt32(token.Content));
                        break;
                    case TokenType.LITERAL_ARR_BEGIN:
                        (List<int> array, IntSize? intSize) = CollectArrayValues();
                        if (intSize != null) AssembleLiteralArray(array, intSize.Value);
                        break;
                    case TokenType.LITERAL_STR:
                        AssembleLiteralArray([.. token.Content], IntSize.INT8, true, true);
                        break;
                    case TokenType.LITERAL_CHAR:
                        AssembleLiteralNum(token.Content[0]);
                        break;
                    case TokenType.IDENTIFIER_PREFIX:
                        AssembleIdentifierPrefixed(token.Content);
                        break;
                    case TokenType.IDENTIFIER:
                        AssembleIdentifier(token.Content);
                        break;
                }

                token = lexer.NextToken();
            }
        }

        /// <summary>
        /// Handles array definitions (array, var, and string).
        /// </summary>
        /// <param name="keyword">Array definition keyword</param>
        private void HandleArrayDef(string keyword)
        {
            RegionType type = RegionType.INT8;
            string label = "";
            ushort size = 1;

            if (keyword == "array")
            {
                string typeStr = lexer.NextToken().Content;
                if (typeStr == "*")
                {
                    type = RegionType.POINTER;
                }
                else
                {
                    type = (RegionType)(Convert.ToUInt32(typeStr) / 8);
                }
                label = lexer.NextToken().Content;
                size = Convert.ToUInt16(lexer.NextToken().Content);
            }
            else if (keyword == "var")
            {
                string typeStr = lexer.NextToken().Content;
                if (typeStr == "*")
                {
                    type = RegionType.POINTER;
                }
                else
                {
                    type = (RegionType)(Convert.ToUInt32(typeStr) / 8);
                }
                label = lexer.NextToken().Content;
            }
            else if (keyword == "string")
            {
                label = lexer.NextToken().Content;
                size = (ushort)lexer.NextToken().Content.Length; // Get string literal length
                lexer.PreviousToken();
            }

            AssembleRegionDef(type, label, size);

            Token content = lexer.NextToken();
            if ((int)content.Type >= 4) // Next token isn't a literal
            {
                bytes.Add((byte)Op.END);
                lexer.PreviousToken();
            }
            else if (content.Type == TokenType.LITERAL_STR)
            {
                char[] contentChars = content.Content.ToCharArray();
                bytes.AddRange(Encoding.UTF8.GetBytes(contentChars));
                bytes.Add((byte)Op.END);
            }
            else if (content.Type == TokenType.LITERAL_ARR_BEGIN)
            {
                (List<int> array, IntSize? intSize) = CollectArrayValues();
                if (intSize != null)
                {
                    AssembleRawArrayData(array, intSize.GetValueOrDefault());
                }
                bytes.Add((byte)Op.END);
            }
            else if (content.Type == TokenType.LITERAL_NUM || content.Type == TokenType.LITERAL_CHAR)
            {
                AssembleLiteralNum(Convert.ToInt32(content.Content), false);
                bytes.Add((byte)Op.END);
            }
        }

        /// <summary>
        /// Assembles an integer literal.
        /// </summary>
        /// <param name="num">Integer</param>
        /// <param name="pushInstruction">Include an immediate push instruction</param>
        private void AssembleLiteralNum(int num, bool pushInstruction=true)
        {
            switch (IntUtility.GetIntSize(num))
            {
                case IntSize.INT8:
                    if (pushInstruction) bytes.Add((byte)Op.PUSH8);
                    bytes.Add((byte)num);
                    break;
                case IntSize.INT16:
                    if (pushInstruction) bytes.Add((byte)Op.PUSH16);
                    bytes.AddRange(IntUtility.GetInt16Bytes((short)num));
                    break;
                case IntSize.INT32:
                    if (pushInstruction) bytes.Add((byte)Op.PUSH32);
                    bytes.AddRange(IntUtility.GetInt32Bytes(num));
                    break;
            }
        }

        /// <summary>
        /// Assembles an array literal.
        /// </summary>
        /// <param name="array">Array data</param>
        /// <param name="bits">Array item size</param>
        /// <param name="pushLength">Add a push instruction for the array length</param>
        /// <param name="reverse">Reverse the array</param>
        private void AssembleLiteralArray(List<int> array, IntSize bits, bool pushLength=false, bool reverse=false)
        {
            if (reverse) array.Reverse();

            if (bits == IntSize.INT8)
            {
                bytes.Add((byte)Op.ARR8);
            }
            else if (bits == IntSize.INT16)
            {
                bytes.Add((byte)Op.ARR16);
            }
            else if (bits == IntSize.INT32)
            {
                bytes.Add((byte)Op.ARR32);
            }

            AssembleRawArrayData(array, bits);
            bytes.Add((byte)Op.ENDLIT);

            if (pushLength)
            {
                bytes.Add((byte)Op.PUSH16);
                bytes.AddRange(IntUtility.GetUInt16Bytes((ushort)array.Count));
            }
        }

        /// <summary>
        /// Assembles raw array data without push instructions.
        /// </summary>
        /// <param name="array">Array data</param>
        /// <param name="bits">Array item size</param>
        private void AssembleRawArrayData(List<int> array, IntSize bits)
        {
            if (bits == IntSize.INT8)
            {
                bytes.AddRange(array.Select(v => (byte)v));
            }
            else if (bits == IntSize.INT16)
            {
                foreach (int v in array)
                {
                    bytes.AddRange(IntUtility.GetInt16Bytes((short)v));
                }
            }
            else if (bits == IntSize.INT32)
            {
                foreach (int v in array)
                {
                    bytes.AddRange(IntUtility.GetInt32Bytes(v));
                }
            }
        }

        /// <summary>
        /// Creates a memory region definition.
        /// </summary>
        /// <param name="bits">Bytes per value</param>
        /// <param name="label">Label</param>
        /// <param name="size">Number of values</param>
        /// <param name="insertIdx">Index to insert the definition</param>
        private void AssembleRegionDef(RegionType type, string label, ushort size, int insertIdx=-1)
        {
            byte bBits = Convert.ToByte(type);
            byte[] bLabel = labels[label];

            uint bits = type == RegionType.POINTER ? 2 : (uint)type;
            byte[] bSize = IntUtility.GetUInt16Bytes((ushort)(size * bits));

            if (insertIdx == -1)
            {
                bytes.AddRange([(byte)Op.DEF,
                            bBits,
                            bLabel[0], bLabel[1],
                            bSize[0], bSize[1]]);
            }
            else
            {
                bytes.InsertRange(insertIdx, [(byte)Op.DEF,
                                            bBits,
                                            bLabel[0], bLabel[1],
                                            bSize[0], bSize[1]]);
            }
            
        }

        /// <summary>
        /// Assembles a prefixed identifier.
        /// </summary>
        /// <param name="prefix">Prefix</param>
        private void AssembleIdentifierPrefixed(string prefix)
        {
            string idLabel = lexer.NextToken().Content;

            byte[] id = TryCreateLabel(idLabel);
            if (prefix == "@")
            {
                bytes.Add((byte)Op.PUSHPTR);
                bytes.AddRange(id);
            }
            else if (prefix == "%")
            {
                bytes.Add((byte)Op.PUSHPTR);
                bytes.AddRange(id);
                bytes.Add((byte)Op.ADDR);
            }
        }

        /// <summary>
        /// Assembles an identifier, either built-in or user-defined.
        /// </summary>
        /// <param name="identifier">Identifier</param>
        private void AssembleIdentifier(string identifier)
        {
            List<byte> asmIdentifier = [];

            switch (identifier)
            {
                case "pop":     asmIdentifier.Add((byte)Op.POP); break;
                case "dup":     asmIdentifier.Add((byte)Op.DUP); break;
                case "swap":    asmIdentifier.Add((byte)Op.SWAP); break;
                case "over":    asmIdentifier.Add((byte)Op.OVER); break;
                case "rot":     asmIdentifier.Add((byte)Op.ROT); break;
                case "+":       asmIdentifier.Add((byte)Op.ADD); break;
                case "-":       asmIdentifier.Add((byte)Op.SUB); break;
                case "*":       asmIdentifier.Add((byte)Op.MUL); break;
                case "/":       asmIdentifier.Add((byte)Op.DIV); break;
                case "load":    asmIdentifier.Add((byte)Op.LOAD); break;
                case "store":   asmIdentifier.Add((byte)Op.STORE); break;
                case "len":     asmIdentifier.Add((byte)Op.SIZE); break;
                case "emit":    asmIdentifier.Add((byte)Op.OUTCHAR); break;
                case "display": asmIdentifier.Add((byte)Op.OUTINT); break;
                case "addr":    asmIdentifier.Add((byte)Op.ADDR); break;
                case "=":       asmIdentifier.Add((byte)Op.EQU); break;
                case ">":       asmIdentifier.Add((byte)Op.GREATER); break;
                case "<":       asmIdentifier.Add((byte)Op.LESS); break;
                case ">=":      asmIdentifier.Add((byte)Op.GEQ); break;
                case "<=":      asmIdentifier.Add((byte)Op.LEQ); break;
                case "~":       asmIdentifier.Add((byte)Op.INVERT); break;
                case "&":       asmIdentifier.Add((byte)Op.AND); break;
                case "|":       asmIdentifier.Add((byte)Op.OR); break;
                case "^":       asmIdentifier.Add((byte)Op.XOR); break;
                case "<<":      asmIdentifier.Add((byte)Op.SLEFT); break;
                case ">>":      asmIdentifier.Add((byte)Op.SRIGHT); break;
                case "if":      asmIdentifier.Add((byte)Op.IF); break;
                case "endif":   asmIdentifier.Add((byte)Op.ENDIF); break;
                case "else":    asmIdentifier.Add((byte)Op.ELSE); break;
                case "loop":    asmIdentifier.Add((byte)Op.LOOP); break;
                case "repeat":  asmIdentifier.Add((byte)Op.REPEAT); break;
                case "for":     asmIdentifier.Add((byte)Op.FOR); break;
                case "next":    asmIdentifier.AddRange([(byte)Op.ADDI, (byte)Op.NEXT]); break;
                case "break":   asmIdentifier.Add((byte)Op.BREAK); break;
                case "i":       asmIdentifier.AddRange([(byte)Op.PUSH8, 0, (byte)Op.PUSHI]); break;
                case "j":       asmIdentifier.AddRange([(byte)Op.PUSH8, 1, (byte)Op.PUSHI]); break;
                case "k":       asmIdentifier.AddRange([(byte)Op.PUSH8, 2, (byte)Op.PUSHI]); break;
                case "l":       asmIdentifier.AddRange([(byte)Op.PUSH8, 3, (byte)Op.PUSHI]); break;
                case "halt":    asmIdentifier.Add((byte)Op.HALT); break;
                case "end":
                    asmIdentifier.Add((byte)Op.END);
                    AssembleRegionDef(RegionType.INT8, wordDefName, (ushort)(bytes.Count - wordDefStartIdx + 1), wordDefStartIdx);
                    wordDefStartIdx = -1;
                    break;
                default:
                    byte[] id = TryCreateLabel(identifier);
                    asmIdentifier.AddRange([
                        (byte)Op.PUSHPTR,
                        id[0],
                        id[1],
                        (byte)Op.CALL
                    ]);
                    break;
            }

            bytes.AddRange(asmIdentifier);
        }

        /// <summary>
        /// Tries to create a new label-ID pair.
        /// </summary>
        /// <param name="label">Label</param>
        /// <returns>ID</returns>
        private byte[] TryCreateLabel(string label)
        {
            bool hasLabel = labels.TryGetValue(label, out _);
            if (!hasLabel)
            {
                labels.Add(label, IntUtility.GetUInt16Bytes((ushort)(numPointers + undefinedRefs++)));
            }
            return labels[label];
        }

        /// <summary>
        /// Collects array values (integers or pointers) into a list.
        /// </summary>
        /// <returns>Tuple containing a list with all array values and the maximum integer size</returns>
        private (List<int>, IntSize?) CollectArrayValues()
        {
            List<int> array = [];
            int largest = 0;
            bool pointerArray = false;

            Token content = lexer.NextToken();
            while (content.Type != TokenType.LITERAL_ARR_END)
            {
                if (content.Type == TokenType.LITERAL_NUM || content.Type == TokenType.LITERAL_CHAR)
                {
                    int val = Convert.ToInt32(content.Content);
                    array.Add(val);

                    largest = (val > largest) ? val : largest;
                }
                else if (content.Type == TokenType.IDENTIFIER_PREFIX && content.Content == "@")
                {
                    pointerArray = true;
                    content = lexer.NextToken();
                    array.Add(BinaryPrimitives.ReadUInt16BigEndian(TryCreateLabel(content.Content)));
                }
                content = lexer.NextToken();
            }

            IntSize? intSize = pointerArray ? IntSize.INT16 : IntUtility.GetIntSize(largest);

            return (array, intSize);
        }

        /// <summary>
        /// Assigns each label a unique ID.
        /// </summary>
        private void GetLabels()
        {
            Token token = lexer.NextToken();
            while (token.Type != TokenType.EOF)
            {
                if (token.Type == TokenType.WORD_DEF)
                {
                    labels.TryAdd(token.Content.ToLower(), IntUtility.GetUInt16Bytes(numPointers++));
                }
                else if (token.Type == TokenType.KEYWORD && token.Content == "string")
                {
                    token = lexer.NextToken();
                    labels.TryAdd(token.Content.ToLower(), IntUtility.GetUInt16Bytes(numPointers++));
                }
                else if (token.Type == TokenType.KEYWORD && (token.Content == "var" || token.Content == "array"))
                {
                    lexer.NextToken();
                    token = lexer.NextToken();
                    labels.TryAdd(token.Content.ToLower(), IntUtility.GetUInt16Bytes(numPointers++));
                }
                token = lexer.NextToken();
            }
            lexer.ResetLexer();
        }

        private enum RegionType { INT8 = 1, INT16 = 2, POINTER = 3, INT32 = 4 }
    }
}