using System.Globalization;

namespace MintCompiler
{
    public class Lexer(string code)
    {
        private static readonly char[] prefixes = ['@'];

        private readonly string code = code + "\n"; // Ensure the last token is read
        private readonly List<Token> tokens = [];
        private int tokenI = 0;

        /// <summary>
        /// Tokenizes the given code, storing all tokens in the tokens list. Assumes there is no leading or
        /// trailing whitespace on any line.
        /// </summary>
        public void Tokenize()
        {
            List<char> tokenChars = [];

            bool inStringOrChar = false;
            bool inComment = false;
            foreach (char c in code)
            {
                if (inComment)
                {
                    if (c == '\n') inComment = false;
                    continue;
                }
                else if (inStringOrChar)
                {
                    if (c == '"' || c == '\'')
                    {
                        inStringOrChar = false;
                    }
                    tokenChars.Add(c);
                    continue;
                }
                else if (c == ' ' || c == '\n')
                {
                    HandleToken(tokenChars);
                    tokenChars = [];
                    continue;
                }
                else if (c == '[')
                {
                    tokens.Add(new Token(TokenType.LITERAL_ARR_BEGIN, "["));
                    continue;
                }
                else if (c == ']')
                {
                    if (tokenChars.Count > 1)
                    {
                        HandleToken(tokenChars);
                        tokenChars = [];
                    }
                    tokens.Add(new Token(TokenType.LITERAL_ARR_END, "]"));
                    continue;
                }
                else if (c == '(')
                {
                    tokens.Add(new Token(TokenType.PAREN_OPEN, "("));
                    continue;
                }
                else if (c == ')')
                {
                    if (tokenChars.Count > 1)
                    {
                        HandleToken(tokenChars);
                        tokenChars = [];
                    }
                    tokens.Add(new Token(TokenType.PAREN_CLOSE, ")"));
                    continue;
                }
                else if (prefixes.Contains(c))
                {
                    tokens.Add(new Token(TokenType.IDENTIFIER_PREFIX, c.ToString()));
                    continue;
                }
                else if (c == '"' || c == '\'')
                {
                    inStringOrChar = true;
                }
                else if (c == '#')
                {
                    inComment = true;
                    continue;
                }
                
                if (c != '\t')
                {
                    tokenChars.Add(c);
                }
            }
            tokens.Add(new Token(TokenType.EOF, string.Empty));
        }

        /// <summary>
        /// Returns the next Token in the list.
        /// </summary>
        public Token NextToken()
        {
            if (tokenI == tokens.Count) return new Token(TokenType.EOF, "");
            return tokens[tokenI++];
        }

        /// <summary>
        /// Moves back to the previous token.
        /// </summary>
        public void PreviousToken()
        {
            tokenI--;
        }

        /// <summary>
        /// Jumps to the start of the token list.
        /// </summary>
        public void ResetLexer()
        {
            tokenI = 0;
        }

        /// <summary>
        /// Converts a multi-character string to a Token and adds it to the token list.
        /// </summary>
        /// <param name="tokenChars">Characters comprising the token</param>
        private void HandleToken(List<char> tokenChars)
        {
            string tokenStr = String.Concat(tokenChars);
            if (tokenStr.Length == 0) return;

            (bool, int) parseNum = ParseInt(tokenStr);
            if (parseNum.Item1)
            {
                tokens.Add(new Token(TokenType.LITERAL_NUM, parseNum.Item2.ToString()));
                return;
            }

            if (tokenStr.StartsWith('\'') && tokenStr.EndsWith('\''))
            {
                tokenStr = tokenStr.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
                tokens.Add(new Token(TokenType.LITERAL_CHAR, tokenStr[1..(tokenStr.Length-1)].ToString()));
            }
            else if (tokenStr.StartsWith('"') && tokenStr.EndsWith('"'))
            {
                tokenStr = tokenStr.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\\\", "\\");
                tokens.Add(new Token(TokenType.LITERAL_STR, tokenStr[1..(tokenStr.Length-1)]));
            }
            else if (tokenStr.EndsWith(':'))
            {
                tokens.Add(new Token(TokenType.REGION_DEF, tokenStr[..(tokenStr.Length-1)]));
            }
            else
            {
                tokens.Add(new Token(TokenType.IDENTIFIER, tokenStr));
            }
        }

        /// <summary>
        /// Try to convert a string to an integer.
        /// </summary>
        /// <param name="integerRaw">String value</param>
        /// <returns>Tuple containing parse success state and if applicable the parsed integer.</returns>
        private static (bool, int) ParseInt(string integerRaw)
        {
            int integer;
            bool result;
            if (integerRaw.StartsWith("0x"))
            {
                integerRaw = integerRaw[2..];
                result = int.TryParse(integerRaw, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out integer);
            }
            else if (integerRaw.StartsWith("0b"))
            {
                integerRaw = integerRaw[2..];
                result = int.TryParse(integerRaw, NumberStyles.BinaryNumber, CultureInfo.CurrentCulture, out integer);
            }
            else
            {
                result = int.TryParse(integerRaw, out integer);
            }

            return (result, integer);
        }
    }
}