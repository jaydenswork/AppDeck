# AppDeck 2.4 的运行时改动

`scrcpy-appdeck.exe` 基于 scrcpy v4.1，许可证为 Apache-2.0。原始 `scrcpy.exe`、Android server、SDL 和 FFmpeg DLL 均保留未修改版本。

修改仅在输入处理：停用原有 MOD 组合键和 F11；根据 `APPDECK_INPUT_CONFIG` 指向的本地 INI 文件，将当前投屏窗口的滚轮转成音量键。没有全局键盘或鼠标钩子。连续滚动使用前沿触发，静默间隔达到设定值后才允许下一次。

## 构建客户端

准备 Python 3、Zig 0.14.1，以及解压后的以下源码／开发包：

- [scrcpy v4.1](https://github.com/Genymobile/scrcpy/archive/refs/tags/v4.1.tar.gz)，本次源码归档 SHA256：`537b2ade623cb94b6edddfa5c61bf0b0af21484aa8365ea2531b686ea573249a`。
- [SDL3 3.4.10 MinGW 开发包](https://www.libsdl.org/release/SDL3-devel-3.4.10-mingw.tar.gz)，SHA256：`39dd2ac370bf33d6332a21ed768d8d49c37cc6f3211d788ead765102722639a8`。
- [FFmpeg 8.1.2](https://ffmpeg.org/releases/ffmpeg-8.1.2.tar.xz)，SHA256：`464beb5e7bf0c311e68b45ae2f04e9cc2af88851abb4082231742a74d97b524c`。

将 SDL 和 FFmpeg 解压在同一目录，执行：

```powershell
python native/build-client.py --source C:/build/scrcpy-4.1 --deps C:/build/deps --zig C:/build/zig/zig.exe
```

脚本应用 `scrcpy-input.patch` 对应修改，使用 `appdeck_input.h`；链接本项目已有的官方 DLL，生成 `runtime/scrcpy/scrcpy-appdeck.exe`。本客户端只编译应用所需的 ADB 连接与 SDK 输入路径，未编译 USB AOA/OTG 路径；USB ADB 正常可用。

防抖测试：`zig cc native/test-wheel.c -o wheel-test.exe`，运行生成的程序。设置 `APPDECK_INPUT_CONFIG` 时，测试文件须包含 Enabled=1、WheelVolume=1、WheelQuietMs=500。

## Android 副屏排版

`DisplayLayout.java` 是 AppDeck 自有辅助程序，不安装 APK。通过 WindowContainerTransaction 将指定副屏的任务切换为自由窗口模式，再通过 resizeTask 的实际边界变化通知应用重新排版。ColorOS 上只更新任务配置或对相同边界调用 RESIZE_MODE_FORCED 不足以刷新实际应用窗口，因此用宽度 2px 的临时变化后立即设置最终尺寸。只在开始会话或任务布局变化时执行；首次排版完成后嵌入画面。

入口拒绝 displayId ≤ 0，只操作当时归属目标副屏的任务，不改主屏或全局旋转／兼容开关。每个会话使用独立临时 jar，结束后删除。首次调用附加 refresh 参数，后续尺寸一致的任务跳过。

```powershell
javac -d build-layout native/DisplayLayout.java native/TaskSession.java
java -cp ANDROID_SDK/build-tools/34.0.0/lib/d8.jar com.android.tools.r8.D8 --min-api 30 --output runtime/scrcpy/appdeck-layout.jar build-layout/DisplayLayout.class build-layout/TaskSession.class
```

## 原任务接管与后台归还

TaskSession.java 使用 shell 权限调用系统任务接口，不安装 APK。接管前记录当前用户、启动周期、显示器唯一标识、任务 ID 和原始窗口配置；已有任务通过临时空根容器跨屏移动，不重发启动 Intent。只有无运行任务时才使用 startActivityFromRecents；无最近任务时使用普通 NEW_TASK，禁用 MULTIPLE_TASK。

归还时将任务通过本程序创建的空根容器移动到主屏底部，并解开容器；随后才恢复主屏窗口配置，最后删除空容器。不能先在横屏副屏恢复 fullscreen：实测 ColorOS 会因此遗留 Activity 绘制偏移。操作只删除本程序创建且确认为空的容器，不移除用户任务，不调用 force-stop。

scrcpy 使用 --no-vd-destroy-content。正常关闭先执行 TaskSession release，成功后才退出 scrcpy；归还失败时保留窗口以便重试。每个会话的本地 XML 与手机 properties 记录支持断连后的保守恢复，手机重启后过期记录不再用于操作可能复用的任务 ID。意外断线造成的系统自动回迁无法保证完全不置前。

2.2 的 ResetDisplay.java / appdeck-reset.jar 已不再调用，保留供历史修复参考。2.3 的销毁副屏页面策略由上述租借与归还流程替代。
