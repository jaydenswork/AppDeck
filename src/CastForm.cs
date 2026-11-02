using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppDeck {
    internal sealed class CastForm:ModernForm {
        public string Serial {get;private set;}
        public readonly string Package,DeviceId;
        string pendingSerial;
        readonly AndroidApp app;readonly bool flex,audio,autoOff,clipboard,landscape;
        readonly int preset;readonly Panel viewport=new Panel();readonly Label stateLabel=Theme.Label("正在连接…",9);readonly ConnectionView placeholder=new ConnectionView();
        readonly System.Windows.Forms.Timer attachTimer=new System.Windows.Forms.Timer {Interval=100};
        readonly StringBuilder recent=new StringBuilder();readonly object logLock=new object();
        readonly string logFile;readonly ToolStripMenuItem offButton,reconnect;readonly IconButton pin;
        readonly bool preview;readonly ContextMenuStrip menu=new ContextMenuStrip();
        readonly TaskLease lease;Process process;IntPtr video=IntPtr.Zero;int displayId=-1,videoWidth,videoHeight;bool closeAllowed,stopping,screenOwned;
        DateTime started;
        readonly System.Windows.Forms.Timer displayTimer=new System.Windows.Forms.Timer{Interval=2000};
        Task configureTask,layoutTask,powerTask;bool directionReady,layoutBusy,layoutUploaded,powerChecked;string directionWarning="";
        internal bool AutoWakeDisplay=true;
        internal bool VideoReady {get{return ready;}}
        internal int ActiveDisplay {get{return displayId;}}
        internal int ReceivedFrames {get{return frameCount;}}
        internal bool ClosingRequested {get;private set;}
        internal Func<Task<bool>> CloseCleanupForChecks;
        readonly string remoteLayoutPath="/data/local/tmp/appdeck-layout-"+Guid.NewGuid().ToString("N")+".jar";
        NotifyIcon tray;ContextMenuStrip trayMenu;
        public bool HiddenToTray {get;private set;}
        internal EdgeDockController EdgeDock {get;private set;}
        protected override bool ShowWithoutActivation{get{return EdgeDock!=null&&EdgeDock.Revealing||base.ShowWithoutActivation;}}
        int frameCount,lifeVersion;bool ready,launching;Task<bool> stopTask;Task closeTask;
        public CastForm(string serial,AndroidApp app,string title,int preset,bool flex,bool audio,bool autoOff,bool clipboard,bool preview=false,bool landscape=false,string deviceId="",bool edgeAutoHide=true){
            this.preview=preview;this.landscape=landscape;
            Serial=serial;DeviceId=deviceId??"";Package=app.Package;lease=new TaskLease(serial,remoteLayoutPath,DeviceId);this.app=app;this.preset=preset;this.flex=flex;this.audio=audio;this.autoOff=autoOff;this.clipboard=clipboard;
            Text=title+" · AppDeck";Caption.Compact=true;
            Size resolution=DisplayProfile.Resolution(preset,landscape);int viewWidth=landscape?900:preset==3?560:438;ClientSize=new Size(viewWidth+8,(int)Math.Round(viewWidth*(double)resolution.Height/resolution.Width)+76);MinimumSize=new Size(330,300);StartPosition=FormStartPosition.CenterScreen;KeyPreview=true;
            string stamp=DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");Directory.CreateDirectory(Path.Combine(Paths.Data,"logs"));logFile=Path.Combine(Paths.Data,"logs",stamp+"-"+Package+".log");
            Caption.AddLeading("back","返回",async(s,e)=>await Input("keyevent","4"));
            pin=Caption.AddAction("pin","置顶窗口",(s,e)=>{TopMost=!TopMost;UpdatePin();FocusVideo();});
            IconButton more=null;more=Caption.AddAction("more","更多操作",(s,e)=>menu.Show(more,new Point(0,more.Height)));
            var layout=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty,Padding=Padding.Empty};
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,24));Body.Controls.Add(layout);
            offButton=new ToolStripMenuItem("手机锁屏",null,async(s,e)=>await ChangePower(false));menu.Items.Add(offButton);
            menu.Items.Add("修改窗口标题",null,(s,e)=>Rename());
            menu.Items.Add("快捷键",null,(s,e)=>{using(var d=new ShortcutDialog())d.ShowDialog(this);});
            EdgeDock=new EdgeDockController(this,()=>menu.Visible||launching||stopping||closeAllowed||!preview&&!ready);
            EdgeDock.Enabled=edgeAutoHide;
            reconnect=new ToolStripMenuItem("重新连接",null,async(s,e)=>{try{if(await Stop()){if(pendingSerial!=null){lease.Rebind(pendingSerial);Serial=pendingSerial;pendingSerial=null;}stopping=false;await Start();}}catch(Exception ex){Theme.Error(this,ex);}});menu.Items.Add(reconnect);
            menu.Items.Add("切换主题",null,(s,e)=>{Theme.Set(!Theme.Dark);var settings=Preferences.Load();settings.Dark=Theme.Dark;if(!preview)settings.Save();});Theme.Menu(menu);
            viewport.Dock=DockStyle.Fill;viewport.Margin=Padding.Empty;
            placeholder.Dock=DockStyle.Fill;viewport.Controls.Add(placeholder);
            placeholder.RetryRequested+=(s,e)=>reconnect.PerformClick();placeholder.DiagnosticsRequested+=(s,e)=>MainForm.ProcessFile(Path.GetDirectoryName(logFile));
            reconnect.EnabledChanged+=(s,e)=>placeholder.CanRetry=reconnect.Enabled;reconnect.TextChanged+=(s,e)=>placeholder.ActionText=reconnect.Text;
            viewport.Resize+=(s,e)=>FitVideo();layout.Controls.Add(viewport,0,0);
            stateLabel.Dock=DockStyle.Fill;stateLabel.AutoEllipsis=true;stateLabel.TextAlign=ContentAlignment.MiddleLeft;stateLabel.Padding=new Padding(8,0,0,0);stateLabel.Margin=Padding.Empty;layout.Controls.Add(stateLabel,0,1);
            Theme.Watch(this,()=>{viewport.BackColor=Theme.Paper;placeholder.BackColor=Theme.Paper;placeholder.ForeColor=Theme.Muted;layout.BackColor=Theme.Surface;stateLabel.BackColor=Theme.Surface;stateLabel.ForeColor=Theme.Muted;});
            attachTimer.Tick+=(s,e)=>AttachTick();Shown+=async(s,e)=>{if(!preview)await Start();};
            if(preview){placeholder.Visible=false;var sample=new ReadingPreview{Dock=DockStyle.Fill};viewport.Controls.Add(sample);sample.BringToFront();stateLabel.Text="界面预览 · 示例内容";}
            Activated+=(s,e)=>{Shortcuts.Touch(this);FocusVideo();};
            Shown+=(s,e)=>{if(TopLevel)Shortcuts.Add(this);};
            displayTimer.Tick+=async(s,e)=>{if(!layoutBusy&&!stopping&&directionReady){layoutTask=FitAppTasks(lifeVersion);await layoutTask;}};
            Disposed+=(s,e)=>{Shortcuts.Remove(this);lease.Abandon();displayTimer.Dispose();attachTimer.Dispose();menu.Dispose();if(tray!=null)tray.Dispose();if(trayMenu!=null)trayMenu.Dispose();};
            FormClosing+=async(s,e)=>{if(closeAllowed)return;e.Cancel=true;await StopAndClose();};
        }
        void UpdatePin(){pin.Active=TopMost;pin.Hint(TopMost?"取消置顶":"置顶窗口");pin.Invalidate();}
        public bool HasDisplayProfile(int otherPreset,bool otherLandscape){return preset==otherPreset&&landscape==otherLandscape;}
        public bool MatchesDevice(Device device){return device.SamePhone(Serial,DeviceId);}
        public bool OfferConnection(Device device){
            if(preview||IsDisposed||closeAllowed||ClosingRequested||!MatchesDevice(device)||device.State!="device"||launching)return false;
            if(process!=null&&!process.HasExited)return false;
            bool changed=pendingSerial!=device.Serial;pendingSerial=device.Serial;
            if(!stopping){ShowReconnectHint();}return changed;
        }
        public void ConnectionUnavailable(){
            if(pendingSerial==null)return;pendingSerial=null;
            if(!stopping&&!launching&&(process==null||process.HasExited))ShowReconnectHint();
        }
        void ShowReconnectHint(){
            placeholder.Visible=true;reconnect.Enabled=true;offButton.Enabled=false;
            bool available=pendingSerial!=null;reconnect.Text=available?"恢复投屏":"重新连接";
            placeholder.Stage=available?1:0;placeholder.Text=available?"手机已重新连接\n恢复投屏后将继续使用原来的应用页面。":"连接已结束\n恢复手机连接后，可在这里重新连接。";
            stateLabel.Text=available?"手机已重新连接 · 可恢复投屏":"连接已结束 · 正在等待手机重新连接";
        }
        void UI(Action action){if(IsDisposed||Disposing)return;try{BeginInvoke((Action)(()=>{if(!IsDisposed)action();}));}catch(InvalidOperationException){}}
        void Log(string text){
            lock(logLock){recent.AppendLine(text);if(recent.Length>18000)recent.Remove(0,recent.Length-12000);try{File.AppendAllText(logFile,DateTime.Now.ToString("HH:mm:ss.fff ")+text+Environment.NewLine,Encoding.UTF8);}catch{}}
        }
        void Receive(Process owner,string line){
            if(line==null)return;Log(line);UI(()=>{
                if(process!=owner||stopping)return;
                var d=Regex.Match(line,@"New display: (\d+)x(\d+)/\d+ \(id=(\d+)\)");
                if(d.Success){videoWidth=Int32.Parse(d.Groups[1].Value);videoHeight=Int32.Parse(d.Groups[2].Value);displayId=Int32.Parse(d.Groups[3].Value);placeholder.Stage=1;placeholder.Text="正在启动 "+app.Name+"\n手机已连接，正在准备独立应用窗口。";stateLabel.Text="手机已连接 · 正在准备应用副屏…";configureTask=ConfigureDisplay(lifeVersion);}
                var t=Regex.Match(line,@"Texture: (\d+)x(\d+)");if(t.Success){videoWidth=Int32.Parse(t.Groups[1].Value);videoHeight=Int32.Parse(t.Groups[2].Value);frameCount++;if(!ready){attachTimer.Interval=100;attachTimer.Start();}}
                if(line.IndexOf("ERROR:",StringComparison.Ordinal)>=0)stateLabel.Text=line;
                WriteStatus();
            });
        }
        async Task Start(){
            if(preview||launching||IsDisposed||closeAllowed)return;
            if(process!=null&&!process.HasExited)return;
            launching=true;stopping=false;stopTask=null;int version=++lifeVersion;
            attachTimer.Interval=100;
            reconnect.Text="重新连接";
            displayId=-1;video=IntPtr.Zero;ready=false;directionReady=false;directionWarning="";configureTask=null;powerTask=null;powerChecked=false;frameCount=0;reconnect.Enabled=false;offButton.Enabled=false;
            placeholder.Visible=true;placeholder.Stage=0;placeholder.Text="正在连接手机\n连接后将打开 "+app.Name+" 的独立窗口。";stateLabel.Text="正在连接手机…";
            try{
                var probe=await Commands.Adb(Serial,"get-state");probe.Ensure();if(probe.Output.Trim()!="device")throw new Exception("设备未连接，请重新连接后点击“重连”。");
                if(version!=lifeVersion||stopping||IsDisposed)return;
                await TaskLease.Recover(Serial,DeviceId);
                var helper=await Commands.Adb(Serial,"push",Path.Combine(Paths.Runtime,"appdeck-layout.jar"),remoteLayoutPath);helper.Ensure();layoutUploaded=true;
                if(version!=lifeVersion||stopping||IsDisposed)return;
                string size=flex?(Math.Max(1,viewport.ClientSize.Width)+"x"+Math.Max(1,viewport.ClientSize.Height)+"/160"):DisplayProfile.Argument(preset,landscape);
                var args=new List<string>{"-s",Serial,"--new-display="+size,"--no-vd-system-decorations","--no-vd-destroy-content",
                    "--display-ime-policy=local","--keep-active","--video-bit-rate=8M","--max-fps=45","--window-borderless","--no-window-aspect-ratio-lock",
                    "--window-title=AppDeck-video-"+Guid.NewGuid().ToString("N"),"--window-width="+Math.Max(1,viewport.ClientSize.Width),"--window-height="+Math.Max(1,viewport.ClientSize.Height),"--mouse-bind=++++","--no-terminal-title"};
                if(flex)args.Add("--flex-display");
                if(audio){args.Add("--audio-source=playback");args.Add("--audio-dup");}else args.Add("--no-audio");
                if(!clipboard)args.Add("--no-clipboard-autosync");
                var p=new Process {StartInfo=Commands.Info(Paths.Scrcpy,args),EnableRaisingEvents=true};
                p.OutputDataReceived+=(s,e)=>Receive(p,e.Data);p.ErrorDataReceived+=(s,e)=>Receive(p,e.Data);
                p.Exited+=(s,e)=>{try{Log("scrcpy exit code: "+p.ExitCode);}catch{}UI(async()=>{if(process!=p||stopping)return;reconnect.Enabled=false;await Stop();if(IsDisposed||closeAllowed)return;
                    stopping=false;ShowReconnectHint();
                });};
                process=p;started=DateTime.UtcNow;Log("AppDeck session; package="+Package+"; serial="+Serial+"; preset="+size+"; flex="+flex);
                p.Start();p.BeginOutputReadLine();p.BeginErrorReadLine();attachTimer.Start();
            }catch(Exception ex){Log(ex.ToString());if(!IsDisposed&&!stopping){placeholder.Text="无法打开副屏\n"+ex.Message;stateLabel.Text="连接失败";reconnect.Enabled=true;}
                if(process!=null){try{if(!process.HasExited)process.Kill();}catch{}process.Dispose();process=null;}
            }finally{launching=false;}
        }
        async void AttachTick(){
            if(process==null||process.HasExited){attachTimer.Stop();return;}
            if(AutoWakeDisplay&&!powerChecked&&directionReady&&frameCount==0&&(DateTime.UtcNow-started).TotalSeconds>=3){int version=lifeVersion;powerChecked=true;powerTask=WakeVirtualDisplay();await powerTask;if(IsDisposed||stopping||version!=lifeVersion)return;}
            // scrcpy creates its SDL HWND hidden, before a frame has initialized content_size.
            // Forcing it visible or resizing it here can trigger a divide-by-zero in screen.c.
            // Wait for both the texture log and scrcpy's own ShowWindow before taking ownership.
            if(video==IntPtr.Zero&&frameCount>0&&directionReady){
                var h=Native.FindScrcpy(process.Id);
                if(h!=IntPtr.Zero&&Native.IsWindowVisible(h)){
                    try{Native.Embed(h,viewport.Handle);video=h;FitVideo();Log("Embedded SDL window "+h);}
                    catch(Exception ex){attachTimer.Stop();Log(ex.ToString());placeholder.Text="窗口嵌入失败；原生窗口仍可使用。\n"+ex.Message;reconnect.Enabled=true;return;}
                }
            }
            if(video!=IntPtr.Zero&&displayId>=0&&frameCount>0&&directionReady&&!ready){
                ready=true;placeholder.Visible=false;offButton.Enabled=true;reconnect.Enabled=true;WriteStatus();FocusVideo();
                if(autoOff)await ChangePower(false);
            }
            if(!ready&&(DateTime.UtcNow-started).TotalSeconds>30){attachTimer.Interval=500;placeholder.Text=directionReady?"正在等待应用画面\n应用已启动，但画面尚未到达。\n可以继续等待，或尝试重新连接。":"准备应用耗时较长\n请确认手机连接和应用状态，然后重试。";stateLabel.Text=directionReady?"等待视频首帧 · 可重新连接":"应用副屏准备超时";reconnect.Enabled=true;}
        }
        internal async Task WakeVirtualDisplay(){
            int version=lifeVersion;if(!Current(version))return;
            try{var result=await Commands.Adb(Serial,"shell","CLASSPATH="+remoteLayoutPath,"app_process","/","DisplayPower",displayId.ToString());result.Ensure();Log("Virtual display power: "+result.Output.Trim());if(Current(version)&&!ready)stateLabel.Text="副屏电源正常 · 正在等待画面…";}
            catch(Exception ex){Log("Virtual display power: "+ex.Message);}
        }
        void FitVideo(){if(video!=IntPtr.Zero&&Native.IsWindow(video)&&Native.GetParent(video)==viewport.Handle)Native.MoveWindow(video,0,0,Math.Max(1,viewport.ClientSize.Width),Math.Max(1,viewport.ClientSize.Height),true);}
        void FocusVideo(){if(video!=IntPtr.Zero&&Native.IsWindow(video))Native.SetFocus(video);}
        void WriteStatus(){if(ready)stateLabel.Text=String.Format("副屏 {0} · {1} × {2} · {3}",displayId,videoWidth,videoHeight,directionWarning.Length>0?directionWarning:flex?"随窗口排版":landscape?"独立横屏":"独立竖屏");}
        async Task Input(params string[] args){
            if(displayId<0||!ready)return;
            try{var all=new List<string>{"input","-d",displayId.ToString()};all.AddRange(args);await Commands.Shell(Serial,all.ToArray());FocusVideo();}catch(Exception ex){Theme.Error(this,ex);}
        }
        async Task ChangePower(bool on){
            if(preview)return;
            offButton.Enabled=false;
            try{
                if(!on){
                    // Restore only a display this session actually switched off.
                    var before=await Commands.Adb(Serial,"shell","cmd","display","get-displays");before.Ensure();
                    if(Regex.IsMatch(before.Output,@"(?m)^Display id 0:.*\bstate ON\b"))screenOwned=true;
                    await Commands.Shell(Serial,"cmd","display","power-off","0");
                    stateLabel.Text="手机实体屏已关闭 · 副屏继续运行";Log("Physical display power-off");
                }else{
                    await Commands.Shell(Serial,"cmd","display","power-reset","0");
                    await Commands.Shell(Serial,"input","-d","0","keyevent","KEYCODE_WAKEUP");screenOwned=false;
                    stateLabel.Text="已恢复实体屏电源；如手机自行锁定，请在手机解锁。";Log("Physical display power-reset");
                }
            }catch(Exception ex){Theme.Error(this,ex);}finally{if(!IsDisposed){offButton.Enabled=ready;FocusVideo();}}
        }
        public void UpdateTitle(string title,bool save=true){if(String.IsNullOrWhiteSpace(title))return;if(save&&!preview)WindowTitles.Save(Package,title);Text=title.Trim()+" · AppDeck";if(tray!=null)tray.Text=Text.Length>63?Text.Substring(0,63):Text;}
        void Rename(){using(var d=new ModernForm {Text="窗口标题",ClientSize=new Size(370,195),MinimumSize=new Size(370,195),StartPosition=FormStartPosition.CenterParent}){
            var field=new Field("窗口标题"){Location=new Point(20,20),Width=320};var t=field.Input;t.Text=Text.EndsWith(" · AppDeck",StringComparison.Ordinal)?Text.Substring(0,Text.Length-10):Text;t.MaxLength=100;var ok=Theme.Button("保存",(s,e)=>{if(!String.IsNullOrWhiteSpace(t.Text)){try{UpdateTitle(t.Text.Trim());d.Close();}catch(Exception ex){Theme.Error(d,ex);}}},true);ok.Location=new Point(220,80);d.Body.Controls.Add(field);d.Body.Controls.Add(ok);d.AcceptButton=ok;d.ShowDialog(this);}}
        public void HideToTray(){
            if(IsDisposed||!Enabled||HiddenToTray||ClosingRequested)return;
            EdgeDock.PrepareForInteraction();
            if(tray==null){
                trayMenu=new ContextMenuStrip();trayMenu.Items.Add("显示窗口",null,(s,e)=>RestoreFromTray());trayMenu.Items.Add("关闭窗口",null,async(s,e)=>await StopAndClose());Theme.Menu(trayMenu);
                tray=new NotifyIcon{Icon=Icon,ContextMenuStrip=trayMenu};tray.DoubleClick+=(s,e)=>RestoreFromTray();
            }
            tray.Text=Text.Length>63?Text.Substring(0,63):Text;tray.Visible=true;HiddenToTray=true;
            // Hide preserves our HWND and its cross-process SDL child. Changing ShowInTaskbar
            // would recreate the parent HWND and can destroy the video window.
            Hide();Log("Hidden to tray; handle="+Handle);Shortcuts.Touch(this);
        }
        public void RestoreFromTray(){
            if(IsDisposed||ClosingRequested)return;HiddenToTray=false;if(tray!=null)tray.Visible=false;
            EdgeDock.PrepareForInteraction();
            if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;
            Show();Activate();FitVideo();FocusVideo();Shortcuts.Touch(this);Log("Restored; handle="+Handle);
        }
        bool Current(int version){return version==lifeVersion&&!stopping&&!IsDisposed&&displayId>0;}
        async Task ConfigureDisplay(int version){
            // Configuration is per virtual display; display 0 and global app-compat settings
            // are never changed. Launch only after configuring to avoid an initial rotation race.
            string id=displayId.ToString();
            try{
                foreach(var command in new[]{
                    new[]{"wm","set-ignore-orientation-request","-d",id,"true"},
                    new[]{"wm","fixed-to-user-rotation","-d",id,"enabled"},
                    new[]{"wm","user-rotation","-d",id,"lock","0"},
                    new[]{"wm","set-display-windowing-mode","-d",id,"5"}}){
                    if(!Current(version))return;
                    var result=await Commands.Adb(Serial,new[]{"shell"}.Concat(command).ToArray());
                    if(result.Code!=0||Regex.IsMatch(result.Combined,@"(?i)error|exception|unknown command"))throw new Exception(result.Combined.Trim());
                }
                Log("Independent orientation configured on display "+id);
            }catch(Exception ex){directionWarning="系统未完整支持独立方向";Log("Display orientation: "+ex.Message);}
            if(!Current(version))return;
            try{
                var result=await Commands.Adb(Serial,"shell","cmd","package","resolve-activity","--brief","-a","android.intent.action.MAIN","-c","android.intent.category.LAUNCHER",Package);result.Ensure();
                var component=Regex.Match(result.Output,@"(?m)^([A-Za-z0-9_.]+/[A-Za-z0-9_.$]+)\s*$");
                if(!component.Success)throw new Exception("找不到应用启动入口");
                if(!Current(version))return;
                Log(await lease.Acquire(displayId,Package,component.Groups[1].Value));
                if(!Current(version))return;await FitAppTasks(version,true);
                if(!Current(version))return;directionReady=true;placeholder.Stage=2;placeholder.Text="正在等待应用画面\n应用已启动，画面准备好后会自动显示。";stateLabel.Text="应用已启动 · 正在等待画面…";displayTimer.Start();
            }catch(Exception ex){Log("Launch: "+ex.Message);if(Current(version)){
                string error=ex.Message;attachTimer.Stop();
                // Schedule rollback after ConfigureDisplay completes; Stop waits for this task.
                UI(async()=>{if(!Current(version))return;bool returned=await Stop();if(IsDisposed)return;
                    placeholder.Visible=true;placeholder.Text="打开应用失败\n"+error;
                    if(returned)stateLabel.Text="点击更多 → 重新连接";reconnect.Enabled=true;
                });
            }}
        }
        async Task FitAppTasks(int version,bool refresh=false){
            if(layoutBusy||!Current(version))return;layoutBusy=true;
            try{
                Size size=flex?new Size(Math.Max(1,videoWidth),Math.Max(1,videoHeight)):DisplayProfile.Resolution(preset,landscape);
                var arguments=new List<string>{"shell","CLASSPATH="+remoteLayoutPath,"app_process","/","DisplayLayout",displayId.ToString(),size.Width.ToString(),size.Height.ToString()};
                if(refresh)arguments.Add("refresh");
                var result=await Commands.Adb(Serial,arguments.ToArray());result.Ensure();
                if(!Current(version))return;
                if(Regex.IsMatch(result.Output,@"changed=[1-9]"))Log("Applied task layout: "+result.Output.Trim());
                if(!result.Output.Contains("tasks="))throw new Exception(result.Combined.Trim());

            }catch(Exception ex){if(Current(version)){directionWarning="窗口尺寸同步失败";Log("Task bounds: "+ex.Message);}}
            finally{layoutBusy=false;}
        }
        Task<bool> Stop(){if(stopTask!=null&&(!stopTask.IsCompleted||stopTask.Status==TaskStatus.RanToCompletion&&stopTask.Result))return stopTask;stopTask=StopCore();return stopTask;}
        async Task<bool> StopCore(){
            var stopWatch=Stopwatch.StartNew();stopping=true;lifeVersion++;attachTimer.Stop();displayTimer.Stop();ready=false;
            if(configureTask!=null){try{await configureTask;}catch{}}
            if(layoutTask!=null){try{await layoutTask;}catch{}}
            if(powerTask!=null){try{await powerTask;}catch{}}
            // Return tasks below the phone's current foreground before removing the display.
            // If return fails while scrcpy is alive, keep the session instead of losing a page.
            try{Log(await lease.Return());Log("Task return completed in "+stopWatch.ElapsedMilliseconds+" ms");}
            catch(Exception ex){
                Log("Task return: "+ex.Message);
                if(process!=null&&!process.HasExited){
                    stopping=false;ready=video!=IntPtr.Zero;reconnect.Enabled=true;Enabled=true;stopTask=null;
                    stateLabel.Text="任务暂未归还，请恢复连接后重试关闭";
                    if(directionReady)displayTimer.Start();attachTimer.Start();return false;
                }
                lease.Abandon(); // Journal is retried after the device reconnects.
            }

            if(screenOwned){try{await Commands.Shell(Serial,"cmd","display","power-reset","0");Log("Restored owned physical display");}catch(Exception ex){Log("Display restore: "+ex.Message);}screenOwned=false;}
            Task cleanup=null;if(layoutUploaded){cleanup=Commands.Run(Paths.Adb,new[]{"-s",Serial,"shell","rm","-f",remoteLayoutPath},1500);layoutUploaded=false;}
            var p=process;if(p!=null){
                try{
                    if(!p.HasExited){
                        var handle=video!=IntPtr.Zero?video:Native.FindScrcpy(p.Id);
                        if(handle!=IntPtr.Zero)Native.PostMessage(handle,0x0010,IntPtr.Zero,IntPtr.Zero);else p.CloseMainWindow();
                        bool exited=await Task.Run(()=>p.WaitForExit(1500));
                        if(!exited){Log("Graceful close timed out after task return; terminating owned scrcpy process");p.Kill();await Task.Run(()=>p.WaitForExit(1000));}
                    }
                }catch(Exception ex){Log("Stop: "+ex.Message);}finally{p.Dispose();}
            }
            process=null;video=IntPtr.Zero;displayId=-1;
            if(cleanup!=null){try{await cleanup;}catch{}}
            Log("Session cleanup completed in "+stopWatch.ElapsedMilliseconds+" ms");
            return true;
        }
        public Task StopAndClose(){if(closeTask!=null&&!closeTask.IsCompleted)return closeTask;closeTask=CloseCore();return closeTask;}
        async Task CloseCore(){
            if(closeAllowed||IsDisposed)return;
            var watch=Stopwatch.StartNew();ClosingRequested=true;stopping=true;attachTimer.Stop();displayTimer.Stop();Enabled=false;EdgeDock.PrepareForClose();if(tray!=null)tray.Visible=false;Hide();Log("Close requested; window hidden in "+watch.ElapsedMilliseconds+" ms");
            // Defer disposal beyond FormClosing, while keeping the message loop and lease alive.
            await Task.Yield();bool stopped=false;
            try{stopped=await (CloseCleanupForChecks==null?Stop():CloseCleanupForChecks());}catch(Exception error){Log("Close cleanup: "+error);}
            if(!stopped){ClosingRequested=false;stopping=false;HiddenToTray=false;Enabled=true;stateLabel.Text="任务暂未归还，请恢复手机连接后重试关闭";Show();Log("Close deferred; window restored for retry");return;}
            closeAllowed=true;attachTimer.Dispose();menu.Dispose();Close();
        }
    }
}
