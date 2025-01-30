is_alphabetic:
    dup is_upper swap is_lower
    |
end

is_upper:
    dup 64 >
    swap 91 <
    &
end

is_lower:
    dup 96 >
    swap 123 <
    &
end

is_numeric:
    dup 47 >
    swap 58 <
    &
end

to_upper:
    dup is_lower if
        32 -
    endif
end

to_lower:
    dup is_upper if
        32 +
    endif
end
