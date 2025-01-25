namespace MintCompiler
{
    public enum Op : byte
    {
        END_FILE        = 0x00,
        HALT            = 0x01,
        NOP             = 0x02,
        DEF             = 0x10,
        END             = 0x11,
        PUSH8           = 0x12,
        PUSH16          = 0x13,
        PUSH32          = 0x14,
        CALL            = 0x20,
        IF              = 0x21,
        ELSE            = 0x22,
        ENDIF           = 0x23,
        LOOP            = 0x24,
        FOR             = 0x25,
        NEXT            = 0x26,
        REPEAT          = 0x27,
        BREAK           = 0x28,
        ADDI            = 0x29,
        PUSHI           = 0x2A,
        LOAD            = 0x30,
        STORE           = 0x31,
        ADDR            = 0x32,
        SIZE            = 0x33,
        BYTES           = 0x34,
        POP             = 0x40,
        DUP             = 0x41,
        SWAP            = 0x42,
        OVER            = 0x43,
        ROT             = 0x44,
        ADD             = 0x60,
        SUB             = 0x61,
        MUL             = 0x62,
        DIV             = 0x63,
        MOD             = 0x64,
        EQU             = 0x65,
        GREATER         = 0x66,
        LESS            = 0x67,
        GEQ             = 0x68,
        LEQ             = 0x69,
        INVERT          = 0x6A,
        AND             = 0x6B,
        OR              = 0x6C,
        XOR             = 0x6D,
        SLEFT           = 0x6E,
        SRIGHT          = 0x6F,
        POW             = 0x70,
        ABS             = 0x71,
    }
}