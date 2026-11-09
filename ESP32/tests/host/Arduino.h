#pragma once
#include <stdint.h>
#include <stddef.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include <string>
class String {
    std::string text_;
public:
    String(const char* text="") : text_(text) {}
    String(const std::string& text) : text_(text) {}
    explicit String(uint32_t number) : text_(std::to_string(number)) {}
    char operator[](size_t index) const { return text_[index]; }
    size_t write(uint8_t byte) { text_+=char(byte);return 1; }
    size_t write(const uint8_t* bytes,size_t count) {text_.append(reinterpret_cast<const char*>(bytes),count);return count;}
    const char* c_str() const { return text_.c_str(); }
    String operator+(const char* text) const { return text_+text; }
    String operator+(const String& text) const { return text_+text.text_; }
    bool operator==(const char* text) const { return text_==text; }
    bool operator==(const String& text) const {return text_==text.text_;}
    bool operator!=(const String& text) const {return text_!=text.text_;}
    bool isEmpty() const {return text_.empty();}
    size_t length() const {return text_.size();}
    bool startsWith(const char* prefix) const {return text_.compare(0,strlen(prefix),prefix)==0;}
    bool reserve(size_t size) {text_.reserve(size);return true;}
    bool concat(const char* data,size_t size) {text_.append(data,size);return true;}
    int lastIndexOf(char ch) const {auto p=text_.find_last_of(ch);return p==std::string::npos ? -1 : int(p);}
    int indexOf(char ch) const {auto p=text_.find(ch);return p==std::string::npos ? -1 : int(p);}
    String substring(size_t start,size_t end) const {return text_.substr(start,end-start);}
    void trim() {auto start=text_.find_first_not_of(" \r\n\t");auto end=text_.find_last_not_of(" \r\n\t");text_=start==std::string::npos ? "" : text_.substr(start,end-start+1);}
    bool equalsIgnoreCase(const char* value) const {if(text_.size()!=strlen(value))return false;for(size_t i=0;i<text_.size();++i)if(tolower(text_[i])!=tolower(value[i]))return false;return true;}
};
extern unsigned ntpStarts;
inline void configTime(long,int,const char*) { ++ntpStarts; }
extern uint32_t testMillis;
inline uint32_t millis() { return testMillis; }
inline void delay(uint32_t value) {testMillis+=value;}
struct TestSerial { template<class... Args> void printf(const char*, Args...) {} };
extern TestSerial Serial;
