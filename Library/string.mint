int>str:
    dup 0 < if
        abs _int>str
        '-' swap 1 +
    else
        _int>str
    endif
end

_int>str:
    0 swap
    loop
        dup 10 %
        '0' + swap
        10 /
        dup 0 = if break endif
        rot 1 + swap
    repeat
    pop swap 1 +
end

str>int:
    swap dup
    '-' = if
        pop 1 -
        _str>int
        -1 *
    else
        swap
        _str>int
    endif
end

_str>int:
    0 swap 0 for
        swap
        '0' -
        10 i 1 - ** * +
    -1 next
end

strlen:
    dup len 0 swap
    for
        dup len
        1 - i = if
            i 1 +
            break 
        endif
        dup i load
        0 = if
            i
            break
        endif
    1 next
end

loadstr:
    strlen dup rot swap
    1 - -1 for
        dup i load swap rot swap
    -1 next
    pop
end

storestr:
    swap 0 swap for
        dup rot i swap store
    1 next
    pop
end

compstr:
    strlen rot strlen rot
    dup rot
    = if
        0 swap for
            dup i load rot
            dup i load rot
            = ! if
                pop pop 0
                break
            endif
        1 next
        dup 0 = ! if
            pop pop 1 
        endif
    else
        pop pop pop 0
    endif
end
