using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace MintCompiler
{
    public class Compiler(Lexer lexer)
    {
        private readonly Lexer lexer = lexer;

        private readonly Dictionary<string, byte[]> labels = [];
        private readonly List<MemoryRegion> regions = [];

        private int currentRegion = -1;
        private ushort numPointers = 0;
        private ushort undefinedRefs = 0;

        /// <summary>
        /// Assembles Mint code into bytecode given a Lexer object.
        /// </summary>
        /// <returns>Raw bytecode</returns>
        public List<byte> Assemble()
        {
            lexer.Tokenize();

            GetLabels();
            AssembleInstructions([TokenType.EOF]);

            List<byte> output = [.. "MOBJ"u8.ToArray()];

            output.AddRange(IntUtility.GetUInt16Bytes((ushort)regions.Count));
            output.AddRange(IntUtility.GetUInt16Bytes((ushort)(numPointers + undefinedRefs)));
            foreach (KeyValuePair<string, byte[]> label in labels)
            {
                output.AddRange(label.Value);
                output.Add((byte)label.Key.Length);
                foreach (char c in label.Key)
                {
                    output.Add((byte)c);
                }
            }
            
            foreach (MemoryRegion region in regions)
            {
                output.AddRange(region.Serialize());
            }
            output.Add((byte)Op.END_FILE);

            return output;
        }

        /// <summary>
        /// Loops through tokens produced by the Lexer and assembles them.
        /// </summary>
        /// <param name="endToken">Halt assembly a Token of this type is reached</param>
        /// <returns>Last token</return>
        private Token AssembleInstructions(TokenType[] endTokens)
        {
            Token token = lexer.NextToken();
            while (!endTokens.Contains(token.Type))
            {
                switch (token.Type)
                {
                    case TokenType.REGION_DEF:
                        HandleRegionDef(token.Content);
                        break;
                    case TokenType.LITERAL_NUM:
                        AssembleLiteralNum(Convert.ToInt32(token.Content));
                        break;
                    case TokenType.OPEN_BRACKET:
                        (List<int> array, bool pointerArray) = CollectArrayValues();
                        AssembleRawArrayData(array, regions[currentRegion].Type, pointerArray);
                        break;
                    case TokenType.LITERAL_STR:
                        AssembleLiteralString(token.Content, true);
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

            return token;
        }

        /// <summary>
        /// Handles a memory region definition
        /// </summary>
        /// <param name="label">Region label</param>
        private void HandleRegionDef(string label)
        {
            Token token = lexer.NextToken();
            if (token.Type != TokenType.PAREN_OPEN)
            {
                lexer.PreviousToken();
                regions.Add(new MemoryRegion(RegionType.INT8, TryCreateLabel(label)));
                currentRegion = regions.Count - 1;
                return;
            }

            RegionType type = RegionType.INT8;
            ushort len = 0;

            for (int i = 0; i < 2; i++)
            {
                token = lexer.NextToken();
                if (token.Type != TokenType.PAREN_CLOSE)
                {
                    (RegionType? typeOrNull, ushort? sizeOrNull) = GetRegionSigData(token);
                    if (typeOrNull.HasValue) type = typeOrNull.GetValueOrDefault();
                    else if (sizeOrNull.HasValue) len = sizeOrNull.GetValueOrDefault();
                }
                else
                {
                    lexer.PreviousToken();
                    break;
                }
            }

            MemoryRegion newRegion = new(type, TryCreateLabel(label), len);
            regions.Add(newRegion);
            currentRegion++;
        }

        /// <summary>
        /// Gather data from a memory region signature
        /// </summary>
        /// <param name="token">Data token</param>
        /// <returns>Tuple containing either region type or size</returns>
        private static (RegionType?, ushort?) GetRegionSigData(Token token)
        {
            if (token.Type == TokenType.IDENTIFIER)
            {
                RegionType type = RegionType.INT8;
                switch (token.Content)
                {
                    case "i8":
                        type = RegionType.INT8;
                        break;
                    case "i16":
                    case "*":
                        type = RegionType.INT16;
                        break;
                    case "i32":
                        type = RegionType.INT32;
                        break;
                }

                return (type, null);
            }
            else if (token.Type == TokenType.LITERAL_NUM)
            {
                return (null, Convert.ToUInt16(token.Content));
            }
            else
            {
                return (null, null);
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
                    if (pushInstruction) AddByte((byte)Op.PUSH8);
                    AddByte((byte)num);
                    break;
                case IntSize.INT16:
                    if (pushInstruction) AddByte((byte)Op.PUSH16);
                    AddByteRange(IntUtility.GetInt16Bytes((short)num));
                    break;
                case IntSize.INT32:
                    if (pushInstruction) AddByte((byte)Op.PUSH32);
                    AddByteRange(IntUtility.GetInt32Bytes(num));
                    break;
            }
        }

        /// <summary>
        /// Assembles a string literal and adds a push length instruction.
        /// </summary>
        /// <param name="str"></param>
        /// <param name="reverse"></param>
        private void AssembleLiteralString(string str, bool reverse=false)
        {
            List<char> strValues = [.. str];
            if (reverse) strValues.Reverse();

            foreach (char i in strValues)
            {
                AddByte((byte)Op.PUSH8);
                AddByte((byte)i);
            }

            // Push string length
            AddByte((byte)Op.PUSH16);
            AddByteRange(IntUtility.GetUInt16Bytes((ushort)strValues.Count));
        }

        /// <summary>
        /// Assembles raw array data without push instructions.
        /// </summary>
        /// <param name="array">Array data</param>
        /// <param name="type">Array type</param>
        /// <param name="pointerArray">This is an array of pointers</param>
        private void AssembleRawArrayData(List<int> array, RegionType type, bool pointerArray=false)
        {
            if (type == RegionType.INT8)
            {
                AddByteRange(array.Select(v => (byte)v).ToArray());
            }
            else if (type == RegionType.INT16)
            {
                if (pointerArray) regions[currentRegion].AddPointer();
                foreach (int v in array)
                {
                    AddByteRange(IntUtility.GetInt16Bytes((short)v));
                }
            }
            else if (type == RegionType.INT32)
            {
                foreach (int v in array)
                {
                    AddByteRange(IntUtility.GetInt32Bytes(v));
                }
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
                AddByte((byte)Op.PUSH16);
                regions[currentRegion].AddPointer();
                AddByteRange(id);
            }

            if (lexer.NextToken().Type != TokenType.OPEN_BRACKET)
            {
                lexer.PreviousToken();
                return;
            }

            Token lastToken = AssembleInstructions([TokenType.CLOSE_BRACKET, TokenType.SEPARATOR]);
            if (lastToken.Type == TokenType.CLOSE_BRACKET)
            {
                AddByte((byte)Op.LOAD);
            }
            else if (lastToken.Type == TokenType.SEPARATOR && lastToken.Content == "=>")
            {
                AssembleInstructions([TokenType.CLOSE_BRACKET]);
                AddByte((byte)Op.STORE);
            }
        }

        /// <summary>
        /// Assembles a for loop, possibly with a start and end value.
        /// </summary>
        private void AssembleForLoop()
        {
            Token next = lexer.NextToken();
            if (next.Type != TokenType.PAREN_OPEN)
            {
                AddByte((byte)Op.FOR);
                lexer.PreviousToken();
                return;
            }

            Token lastToken = AssembleInstructions([TokenType.SEPARATOR]);
            if (lastToken.Content == ",")
            {
                AssembleInstructions([TokenType.PAREN_CLOSE]);
                AddByte((byte)Op.FOR);
            }
        }

        /// <summary>
        /// Assembles an identifier, either built-in or user-defined.
        /// </summary>
        /// <param name="identifier">Identifier</param>
        private void AssembleIdentifier(string identifier)
        {
            switch (identifier)
            {
                case "pop":     AddByte((byte)Op.POP); break;
                case "dup":     AddByte((byte)Op.DUP); break;
                case "swap":    AddByte((byte)Op.SWAP); break;
                case "over":    AddByte((byte)Op.OVER); break;
                case "rot":     AddByte((byte)Op.ROT); break;
                case "third":   AddByte((byte)Op.THIRD); break;
                case "fourth":  AddByte((byte)Op.FOURTH); break;
                case "+":       AddByte((byte)Op.ADD); break;
                case "-":       AddByte((byte)Op.SUB); break;
                case "*":       AddByte((byte)Op.MUL); break;
                case "/":       AddByte((byte)Op.DIV); break;
                case "%":       AddByte((byte)Op.MOD); break;
                case "load":    AddByte((byte)Op.LOAD); break;
                case "store":   AddByte((byte)Op.STORE); break;
                case "size":    AddByte((byte)Op.SIZE); break;
                case "bytes":   AddByte((byte)Op.BYTES); break;
                case "addr":    AddByte((byte)Op.ADDR); break;
                case "=":       AddByte((byte)Op.EQU); break;
                case ">":       AddByte((byte)Op.GREATER); break;
                case "<":       AddByte((byte)Op.LESS); break;
                case ">=":      AddByte((byte)Op.GEQ); break;
                case "<=":      AddByte((byte)Op.LEQ); break;
                case "~":       AddByte((byte)Op.INVERT); break;
                case "&":       AddByte((byte)Op.AND); break;
                case "|":       AddByte((byte)Op.OR); break;
                case "^":       AddByte((byte)Op.XOR); break;
                case "<<":      AddByte((byte)Op.SLEFT); break;
                case ">>":      AddByte((byte)Op.SRIGHT); break;
                case "**":      AddByte((byte)Op.POW); break;
                case "!":       AddByteRange([(byte)Op.PUSH8, 1, (byte)Op.XOR]); break;
                case "abs":     AddByte((byte)Op.ABS); break;
                case "if":      AddByte((byte)Op.IF); break;
                case "endif":   AddByte((byte)Op.ENDIF); break;
                case "else":    AddByte((byte)Op.ELSE); break;
                case "loop":    AddByte((byte)Op.LOOP); break;
                case "repeat":  AddByte((byte)Op.REPEAT); break;
                case "for":     AssembleForLoop(); break;
                case "next":    AddByteRange([(byte)Op.ADDI, (byte)Op.NEXT]); break;
                case "break":   AddByte((byte)Op.BREAK); break;
                case "i":       AddByteRange([(byte)Op.PUSH8, 0, (byte)Op.PUSHI]); break;
                case "j":       AddByteRange([(byte)Op.PUSH8, 1, (byte)Op.PUSHI]); break;
                case "k":       AddByteRange([(byte)Op.PUSH8, 2, (byte)Op.PUSHI]); break;
                case "l":       AddByteRange([(byte)Op.PUSH8, 3, (byte)Op.PUSHI]); break;
                case "halt":    AddByte((byte)Op.HALT); break;
                case "call":    AddByte((byte)Op.CALL); break;
                case ";":
                    currentRegion = -1;
                    break;
                case "end":
                    AddByte((byte)Op.END);
                    currentRegion = -1;
                    break;

                case "READC!":      AssembleLiteralNum(1); break;
                case "WRITEC!":     AssembleLiteralNum(2); break;
                case "GETARG1!":    AssembleLiteralNum(3); break;
                case "GETARG2!":    AssembleLiteralNum(4); break;
                case "GETARG3!":    AssembleLiteralNum(5); break;
                case "GETARG4!":    AssembleLiteralNum(6); break;
                case "READPAGE!":   AssembleLiteralNum(1); break;
                case "WRITEPAGE!":  AssembleLiteralNum(2); break;
                case "READF!":      AssembleLiteralNum(1); break;
                case "WRITEF!":     AssembleLiteralNum(2); break;
                case "APPENDF!":    AssembleLiteralNum(3); break;
                case "READFP!":     AssembleLiteralNum(4); break;
                case "WRITEFP!":    AssembleLiteralNum(5); break;
                case "APPENDFP!":   AssembleLiteralNum(6); break;
                case "FILELEN!":    AssembleLiteralNum(7); break;

                default:
                    byte[] id = TryCreateLabel(identifier);
                    AddByte((byte)Op.PUSH16);
                    regions[currentRegion].AddPointer();
                    AddByteRange([
                        id[0],
                        id[1],
                        (byte)Op.CALL
                    ]);
                    break;
            }
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
        /// <returns>Tuple containing a list with all array values and a boolean that's true if the array contains pointers.</returns>
        private (List<int>, bool) CollectArrayValues()
        {
            List<int> array = [];
            bool pointerArray = false;

            Token content = lexer.NextToken();
            while (content.Type != TokenType.CLOSE_BRACKET)
            {
                if (content.Type == TokenType.LITERAL_NUM || content.Type == TokenType.LITERAL_CHAR)
                {
                    int val = Convert.ToInt32(content.Content);
                    array.Add(val);
                }
                else if (content.Type == TokenType.IDENTIFIER_PREFIX && content.Content == "@")
                {
                    pointerArray = true;
                    content = lexer.NextToken();
                    array.Add(BinaryPrimitives.ReadUInt16BigEndian(TryCreateLabel(content.Content)));
                }
                else if (content.Type == TokenType.LITERAL_STR)
                {
                    foreach (char c in content.Content)
                    {
                        array.Add(c);
                    }
                }
                content = lexer.NextToken();
            }

            return (array, pointerArray);
        }

        /// <summary>
        /// Assigns each label a unique ID.
        /// </summary>
        private void GetLabels()
        {
            Token token = lexer.NextToken();
            while (token.Type != TokenType.EOF)
            {
                if (token.Type == TokenType.REGION_DEF)
                {
                    labels.TryAdd(token.Content.ToLower(), IntUtility.GetUInt16Bytes(numPointers++));
                }
                token = lexer.NextToken();
            }
            lexer.ResetLexer();
        }

        /// <summary>
        /// Adds a byte to the current region.
        /// </summary>
        /// <param name="b">Byte</param>
        private void AddByte(byte b)
        {
            regions[currentRegion].Data.Add(b);
        }

        /// <summary>
        /// Adds a range of bytes to the current region.
        /// </summary>
        /// <param name="b">Byte array</param>
        private void AddByteRange(byte[] b)
        {
            regions[currentRegion].Data.AddRange(b);
        }
    }
}