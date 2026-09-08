from pathlib import Path
import re, subprocess, os, difflib
import argparse
a=argparse.ArgumentParser();a.add_argument('--source',required=True);a.add_argument('--deps',required=True);a.add_argument('--zig',required=True);args_in=a.parse_args()
root=Path(args_in.source).resolve()
stage=Path(__file__).resolve().parent.parent
native=stage/'native';native.mkdir(exist_ok=True)
p=root/'app/src/input_manager.c'
backup=p.with_suffix('.c.original')
if not backup.exists():backup.write_text(p.read_text())
original=backup.read_text()
s=original.replace('#include "shortcut_mod.h"','#include "shortcut_mod.h"\n#include "appdeck_input.h"')
s=s.replace('im->sdl_shortcut_mods = sc_shortcut_mods_to_sdl(params->shortcut_mods);','im->sdl_shortcut_mods = 0; // AppDeck: no built-in MOD shortcuts')
start=s.index('    // Shortcuts that do not involve the MOD key\n')
end=s.index('    if (is_shortcut)',start)
s=s[:start]+s[end:]
needle='    if (!im->mp->ops->process_mouse_scroll) {'
assert needle in s
s=s.replace(needle,'''    // Consume only this SDL window's wheel messages. No global input hook.
    struct appdeck_input cfg = appdeck_read_input();
    if (cfg.enabled && cfg.wheel_volume && event->y != 0) {
        static struct appdeck_wheel_gate gate = {0};
        if (appdeck_wheel_accept(&gate, SDL_GetTicks(), cfg.quiet_ms)) {
            enum android_keycode key = event->y > 0 ? AKEYCODE_VOLUME_UP : AKEYCODE_VOLUME_DOWN;
            send_keycode(im, key, SC_ACTION_DOWN, "AppDeck wheel");
            send_keycode(im, key, SC_ACTION_UP, "AppDeck wheel");
            LOGI("AppDeck wheel volume: %s", event->y > 0 ? "up" : "down");
        }
        return;
    }

'''+needle)
p.write_text(s)
header='''// AppDeck additions, 2026. Original scrcpy code is Apache-2.0.
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
'''
(root/'app/src/appdeck_input.h').write_text(header)
(native/'appdeck_input.h').write_text(header)
(native/'scrcpy-input.patch').write_text(''.join(difflib.unified_diff(original.splitlines(True),s.splitlines(True),fromfile='a/app/src/input_manager.c',tofile='b/app/src/input_manager.c')))
build=root/'appdeck-build';build.mkdir(exist_ok=True)
(build/'config.h').write_text('''#define SCRCPY_VERSION "4.1"
#define PREFIX "."
#define PORTABLE
#define DEFAULT_LOCAL_PORT_RANGE_FIRST 27183
#define DEFAULT_LOCAL_PORT_RANGE_LAST 27199
#define HAVE_STRDUP
''')
deps=Path(args_in.deps).resolve()
ff=deps/'ffmpeg-8.1.2'
(ff/'libavutil/avconfig.h').write_text('#define AV_HAVE_BIGENDIAN 0\n#define AV_HAVE_FAST_UNALIGNED 1\n')
sdl=next(deps.glob('SDL3-*'))/'x86_64-w64-mingw32/include'
sources=re.findall(r"'(src/[^']+\.c)'",(root/'app/meson.build').read_text().split('feature_test_macros')[0])
sources+=['src/util/command.c','src/sys/win/file.c','src/sys/win/process.c']
zig=Path(args_in.zig).resolve()
runtime=stage/'runtime/scrcpy'
args=[str(zig),'cc','-target','x86_64-windows-gnu','-O2','-DNDEBUG','-D_GNU_SOURCE','-D_POSIX_C_SOURCE=200809L','-D_XOPEN_SOURCE=700','-DWINVER=0x0a00',
      '-ffile-prefix-map='+str(root)+'=/usr/src/scrcpy',
      '-fmacro-prefix-map='+str(root)+'=/usr/src/scrcpy',
      '-ffile-prefix-map='+str(deps)+'=/usr/src/deps',
      '-fmacro-prefix-map='+str(deps)+'=/usr/src/deps',
      '-ffile-prefix-map='+str(stage)+'=/usr/src/appdeck',
      '-fmacro-prefix-map='+str(stage)+'=/usr/src/appdeck',
      '-I'+str(build),'-I'+str(root/'app/src'),'-I'+str(sdl),'-I'+str(ff)]
args += [str(root/'app'/f) for f in sources]
args += [str(runtime/f) for f in ['SDL3.dll','avcodec-62.dll','avformat-62.dll','avutil-60.dll','swresample-6.dll']]
args += ['-lws2_32','-luser32','-lshell32','-o',str(runtime/'scrcpy-appdeck.exe')]
env=os.environ.copy();env['ZIG_GLOBAL_CACHE_DIR']=str(deps/'zig-cache')
subprocess.run(args,check=True,env=env)
print('Built scrcpy-appdeck.exe')

\n