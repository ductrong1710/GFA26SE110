#pragma once
#include <cstddef>
#include <cstdint>

enum class FlashPartitionState { Blank, NonBlank, ReadError, InvalidRange };

// Check every byte before allowing first-use formatting. Never infer that the
// whole partition is erased from an empty/corrupt filesystem superblock.
template<class Read, class Idle>
FlashPartitionState checkFlashPartition(uint32_t start, uint32_t length,
                                       uint32_t flashSize, Read read, Idle idle) {
    if (!length || start % 4096 || length % 4096 ||
        start >= flashSize || length > flashSize - start)
        return FlashPartitionState::InvalidRange;
    uint32_t block[64];
    for (uint32_t offset = 0; offset < length; offset += sizeof(block)) {
        if (!read(start + offset, block, sizeof(block)))
            return FlashPartitionState::ReadError;
        for (uint32_t word : block)
            if (word != UINT32_MAX) return FlashPartitionState::NonBlank;
        idle();
    }
    return FlashPartitionState::Blank;
}
