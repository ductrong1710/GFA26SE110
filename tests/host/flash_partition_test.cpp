#include <cassert>
#include <cstring>
#include <vector>
#include "FlashPartitionCheck.h"
int main() {
    std::vector<unsigned char> flash(12288, 255);
    unsigned reads = 0;
    bool fail = false;
    auto read = [&](uint32_t address, uint32_t* data, size_t length) {
        ++reads;
        if (fail) return false;
        std::memcpy(data, flash.data() + address, length);
        return true;
    };
    auto idle = []() {};
    assert(checkFlashPartition(4096,8192,12288,read,idle) == FlashPartitionState::Blank);
    for (size_t offset : {size_t(4096),size_t(8192),size_t(12287)}) {
        flash[offset] = 0;
        assert(checkFlashPartition(4096,8192,12288,read,idle) == FlashPartitionState::NonBlank);
        flash[offset] = 255;
    }
    fail = true;
    assert(checkFlashPartition(4096,8192,12288,read,idle) == FlashPartitionState::ReadError);
    reads = 0;
    assert(checkFlashPartition(4096,0,12288,read,idle) == FlashPartitionState::InvalidRange);
    assert(checkFlashPartition(4097,4096,12288,read,idle) == FlashPartitionState::InvalidRange);
    assert(checkFlashPartition(8192,8192,12288,read,idle) == FlashPartitionState::InvalidRange);
    assert(checkFlashPartition(0xfffff000,8192,12288,read,idle) == FlashPartitionState::InvalidRange);
    assert(reads == 0);
}
