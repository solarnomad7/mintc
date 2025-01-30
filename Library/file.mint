_getfilelen:
    @FS.path 0 rot store
    @FS 0 FILELEN! store
end

getfilelen:
    _getfilelen
    @FS.source 1 load
end

readfile:
    swap
    _getfilelen
    @FS.source 0 0 store
    @FS.dest 0 rot store
    @FS 0 READF! store
end

_awfile:
    dup
    @FS.source 0 rot store
    @FS.source 0 rot len store
    @FS.path 0 rot store
end

writefile:
    _awfile
    @FS 0 WRITEF! store
end

appendfile:
    _awfile
    @FS 0 APPENDF! store
end
