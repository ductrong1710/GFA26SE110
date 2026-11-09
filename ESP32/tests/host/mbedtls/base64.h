#pragma once
#include <stddef.h>
inline int mbedtls_base64_encode(unsigned char* out,size_t capacity,size_t* written,const unsigned char* data,size_t size) {
    static const char alphabet[]="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/";
    const size_t length=((size+2)/3)*4;
    if(capacity<=length)return -1;
    size_t n=0;
    for(size_t i=0;i<size;i+=3) {
        unsigned value=unsigned(data[i])<<16;
        if(i+1<size)value|=unsigned(data[i+1])<<8;
        if(i+2<size)value|=data[i+2];
        out[n++]=alphabet[(value>>18)&63];out[n++]=alphabet[(value>>12)&63];
        out[n++]=i+1<size?alphabet[(value>>6)&63]:'=';out[n++]=i+2<size?alphabet[value&63]:'=';
    }
    out[n]=0;*written=n;return 0;
}
