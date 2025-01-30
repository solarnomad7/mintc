writepage:
    _rwpage_ready
    @PAGE 1 WRITEPAGE! store
end

readpage:
    _rwpage_ready
    @PAGE 1 READPAGE! store
end

_rwpage_ready:
    dup len
    @PAGE.rwsize 1 rot store
    @PAGE.src 0 rot store
    @PAGE.rwsize 0 rot store
    @PAGE 0 rot store
end
