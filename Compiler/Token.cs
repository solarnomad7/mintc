namespace MintCompiler
{
    public readonly struct Token(TokenType type, string content)
    {
        public readonly TokenType Type = type;
        public readonly string Content = content;
    }
    
    public enum TokenType
    {
        LITERAL_NUM = 0,
        LITERAL_CHAR = 1,
        LITERAL_ARR_BEGIN = 2,
        LITERAL_STR = 3,
        LITERAL_ARR_END = 4,
        IDENTIFIER = 5,
        IDENTIFIER_PREFIX = 6,
        WORD_DEF = 7,
        KEYWORD = 8,
        EOF = 9,
    }
}