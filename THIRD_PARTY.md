# 第三方来源

运行包基于 [Genymobile/scrcpy v4.1](https://github.com/Genymobile/scrcpy/releases/tag/v4.1)。官方 Windows x64 ZIP SHA256：`5b12172b3264b2889f4583ee64752ce832e29bc8b1089dca81093459697165db`。

scrcpy.exe、scrcpy-server、SDL3、FFmpeg、libusb、ADB 和相关 DLL 保持官方版本。scrcpy 的 Apache-2.0 许可证保留在 runtime/scrcpy/LICENSE.txt。

新增 scrcpy-appdeck.exe 是 AppDeck 修改版：停用旧快捷键，增加可关闭的滚轮音量映射和防抖。它并非官方原始客户端。输入补丁、额外源文件、依赖来源及构建说明位于 native/，原客户端仍保留。

appdeck-layout.jar 由本项目 native/DisplayLayout.java 和 native/TaskSession.java 构建，用于副屏排版、原任务接管与后台归还；它通过 ADB 临时运行，不安装 APK、不修改 Android 框架或应用安装包。旧版 appdeck-reset.jar / ResetDisplay.java 仅作为历史实现保留，当前程序不再调用。

SDL、FFmpeg、libusb 和 Android Platform Tools 使用各自许可证。本次链接原有官方运行包中的 DLL，未修改其二进制。

安装包使用 [Inno Setup](https://jrsoftware.org/isinfo.php) 构建；`installer/ChineseSimplified.isl` 来自 [jrsoftware/issrc](https://github.com/jrsoftware/issrc/blob/main/Files/Languages/ChineseSimplified.isl)，遵循 Inno Setup 项目的许可条款。
