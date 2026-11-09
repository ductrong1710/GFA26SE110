#include "PendingDeletion.h"
#include <cassert>
#include <cstdio>
#include <iostream>
#include <string>
#include <vector>

struct Files : PendingFiles {
    std::vector<std::string> pending;
    std::vector<std::string> untouched{"/synced/saved", "/receipts/saved", "/mission.json", "/flight-test.json"};
    int failAfter=-1;
    unsigned removed=0;
    bool readFails=false;
    int next(char* path,size_t capacity) override {
        if(readFails)return -1;
        if(pending.empty())return 0;
        snprintf(path,capacity,"%s",pending.front().c_str());return 1;
    }
    bool remove(const char* path) override {
        if(failAfter>=0 && removed==unsigned(failAfter))return false;
        assert(!pending.empty() && pending.front()==path);
        pending.erase(pending.begin());++removed;return true;
    }
};
int main() {
    const char command[]="DELETE_PENDING CONFIRM";
    assert(pendingDeleteConfirmed(command,sizeof(command)-1,false));
    assert(!pendingDeleteConfirmed(command,sizeof(command)-1,true));
    assert(!pendingDeleteConfirmed("DELETE_PENDING",14,false));
    assert(!pendingDeleteConfirmed("DELETE_PENDING CONFIRM extra",28,false));
    const char injected[]="DELETE_PENDING CONFIRM\0extra";
    assert(!pendingDeleteConfirmed(injected,sizeof(injected)-1,false));
    size_t removed=99;
    {Files f;f.pending={"/pending/a","/pending/b"};assert(deletePendingFiles(f,removed,1000));assert(removed==2 && f.pending.empty());assert(f.untouched.size()==4);}
    {Files f;assert(deletePendingFiles(f,removed,1000));assert(removed==0);}
    {Files f;f.pending={"/pending/a","/pending/b"};f.failAfter=1;assert(!deletePendingFiles(f,removed,1000));assert(removed==1 && f.pending.size()==1);}
    {Files f;f.readFails=true;assert(!deletePendingFiles(f,removed,1000));assert(removed==0);}
    for(auto path:{"/synced/a","/mission.json","/pending/../mission.json","/pending/..","/pending/","/pending/nested/a"}) {
        Files f;f.pending={path};assert(!deletePendingFiles(f,removed,1000));assert(removed==0 && f.pending.size()==1);
    }
    {Files f;f.pending={"/pending/a","/pending/b"};assert(!deletePendingFiles(f,removed,1));assert(removed==1 && f.pending.size()==1);}
    std::cout<<"Pending-only deletion, empty queue, partial failure and path bounds passed\n";
}
