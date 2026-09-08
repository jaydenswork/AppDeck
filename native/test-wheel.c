#include "appdeck_input.h"
#include <assert.h>
#include <stdio.h>
int main(void) {
    struct appdeck_wheel_gate g={0};
    assert(appdeck_wheel_accept(&g,100,350));
    for(uint64_t now=110;now<=5000;now+=10)assert(!appdeck_wheel_accept(&g,now,350));
    assert(!appdeck_wheel_accept(&g,5349,350));
    assert(appdeck_wheel_accept(&g,5699,350));
    assert(!appdeck_wheel_accept(&g,5700,500));
    assert(appdeck_wheel_accept(&g,6200,500));
    struct appdeck_input cfg=appdeck_read_input();
    if(getenv("APPDECK_INPUT_CONFIG")){assert(cfg.enabled);assert(cfg.wheel_volume);assert(cfg.quiet_ms==500);}
    puts("PASS: first event; 490-event continuous burst suppressed; quiet interval reset and boundaries; native settings reader");
    return 0;
}
