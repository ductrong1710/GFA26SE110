#pragma once
#include <stdint.h>
#include <stddef.h>
// Deterministic stand-in for payload grouping tests; production uses mbedTLS SHA-256.
inline int mbedtls_sha256_ret(const unsigned char* input,size_t length,unsigned char output[32],int) {
    uint32_t hash=2166136261u;
    for(size_t i=0;i<length;++i)hash=(hash^input[i])*16777619u;
    for(size_t i=0;i<32;++i)output[i]=static_cast<unsigned char>(hash>>((i%4)*8));
    return 0;
}
