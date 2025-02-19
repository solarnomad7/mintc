print:
    0 swap
    for
        emit
    1 next
end

printstr:
    dup _len 0 swap
    for
        dup i load
        dup 0 = if break endif
        emit
    1 next
    pop
end

emit:
    @TERMINAL.writec 0 rot store
    @TERMINAL 0 WRITEC! store
end

getchar:
    @TERMINAL 0 READC! store
    @TERMINAL.readc 0 load
end

getline:
    dup _len 0 swap
    for
        dup i
        getchar dup
        _is_eol if
            pop 0 store
            break
        endif
        store
    1 next
end

_is_eol:
    dup '\n' =
    swap 0 =
    |
end

getarg:
    swap
    @TERMINAL.args 0 rot store
    @TERMINAL 0 rot 2 + store
end

_len:
    dup size swap bytes /
end
