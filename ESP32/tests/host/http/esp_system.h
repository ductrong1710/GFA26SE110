#pragma once
#include <cstring>
inline void esp_fill_random(void* data,size_t length) {memset(data,1,length);}
