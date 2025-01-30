inc:
    rot rot
    over over load
    fourth + store
    pop
end

sum:
    0 swap dup
    len 0 swap for
        dup i load rot +
        swap
    1 next
    pop
end

map:
    swap dup
    len 0 swap for
        dup i load
        over swap
        fourth call
        i swap store
    1 next
    pop pop
end
