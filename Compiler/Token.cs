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
        OPEN_BRACKET = 2,
        LITERAL_STR = 3,
        CLOSE_BRACKET = 4,
        IDENTIFIER = 5,
        IDENTIFIER_PREFIX = 6,
        REGION_DEF = 7,
        PAREN_OPEN = 8,
        PAREN_CLOSE = 9,
        SEPARATOR = 10,
        EOF = 11,
    }
}