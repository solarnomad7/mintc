print:
    0 swap
    for
        emit
    1 next
end

printstr:
    dup len 0 swap
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
    dup len 0 swap
    for
        dup i
        getchar dup
        '\n' = if break endif
        store
    1 next
end
