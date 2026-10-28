#pragma once
#include <stddef.h>
#include <string.h>

inline bool pendingDeleteConfirmed(const char* line, size_t length, bool overflow) {
    const char command[] = "DELETE_PENDING CONFIRM";
    return line && !overflow && length == sizeof(command) - 1 &&
        !memcmp(line, command, sizeof(command) - 1);
}

class PendingFiles {
public:
    virtual ~PendingFiles() {}
    // 1 = next file, 0 = empty directory, -1 = read failure.
    virtual int next(char* path, size_t capacity) = 0;
    virtual bool remove(const char* path) = 0;
};

inline bool deletePendingFiles(PendingFiles& files, size_t& removed, size_t limit) {
    removed = 0;
    for (;;) {
        char path[160] = {};
        const int found = files.next(path, sizeof(path));
        if (found == 0) return true;
        if (found != 1 || removed >= limit) return false;
        // Only direct files inside /pending. No recursive deletion or caller-supplied paths.
        if (strncmp(path, "/pending/", 9) || !path[9] ||
            strchr(path + 9, '/') || strchr(path + 9, '\\') ||
            !strcmp(path + 9, ".") || !strcmp(path + 9, "..")) return false;
        if (!files.remove(path)) return false;
        ++removed;
    }
}
