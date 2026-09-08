// AppDeck additions, 2026. Original scrcpy code is Apache-2.0.
#ifndef APPDECK_INPUT_H
#define APPDECK_INPUT_H
#include <stdbool.h>
#include <stdint.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
struct appdeck_wheel_gate { uint64_t last; bool started; };
// Leading edge, then require a quiet interval. A continuous wheel burst cannot
// build a queue or trigger another page, regardless of its duration/delta.
static inline bool appdeck_wheel_accept(struct appdeck_wheel_gate *g,uint64_t now,unsigned quiet) {
    bool accept=!g->started || now-g->last>=quiet;
    g->last=now;g->started=true;return accept;
}
struct appdeck_input { bool enabled,wheel_volume; unsigned quiet_ms; };
static inline struct appdeck_input appdeck_read_input(void) {
    struct appdeck_input cfg={false,false,350};
#ifdef _WIN32
    const wchar_t *path=_wgetenv(L"APPDECK_INPUT_CONFIG");
    FILE *f=path?_wfopen(path,L"r"):NULL;
    if(f) {
        char line[128];
        while(fgets(line,sizeof(line),f)) {
            if(!strncmp(line,"Enabled=",8))cfg.enabled=atoi(line+8)!=0;
            else if(!strncmp(line,"WheelVolume=",12))cfg.wheel_volume=atoi(line+12)!=0;
            else if(!strncmp(line,"WheelQuietMs=",13))cfg.quiet_ms=atoi(line+13);
        }
        fclose(f);
    }
#endif
    if(cfg.quiet_ms<200)cfg.quiet_ms=200;
    if(cfg.quiet_ms>1000)cfg.quiet_ms=1000;
    return cfg;
}
#endif
