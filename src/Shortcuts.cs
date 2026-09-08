using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace AppDeck {
    internal sealed class InputSettings {
        public bool Enabled=true, AltW=true, ToggleHidden=true, WheelVolume;
        public int WheelQuietMs=350;
        public static readonly string FilePath=Path.Combine(Paths.Data,"controls.ini");
        public static InputSettings Read(){
            var result=new InputSettings();
            if(!File.Exists(FilePath))return result;
            foreach(string line in File.ReadAllLines(FilePath)){
                var parts=line.Split('=');if(parts.Length!=2)continue;
                int n;if(!Int32.TryParse(parts[1],out n))continue;
                switch(parts[0].Trim()){
                    case "Enabled":result.Enabled=n!=0;break;
                    case "AltW":result.AltW=n!=0;break;
                    case "ToggleHidden":result.ToggleHidden=n!=0;break;
                    case "WheelVolume":result.WheelVolume=n!=0;break;
                    case "WheelQuietMs":result.WheelQuietMs=Math.Max(200,Math.Min(1000,n));break;
                }
            }return result;
        }
        public void Save(){
            Directory.CreateDirectory(Paths.Data);
            string text="[input]\r\nEnabled="+(Enabled?1:0)+"\r\nAltW="+(AltW?1:0)+"\r\nToggleHidden="+(ToggleHidden?1:0)+"\r\nWheelVolume="+(WheelVolume?1:0)+"\r\nWheelQuietMs="+WheelQuietMs+"\r\n";
            string temp=FilePath+".tmp";File.WriteAllText(temp,text,Encoding.ASCII);
            if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);
        }
    }

    // Register just one explicit system shortcut. No keyboard or mouse hooks are installed.
    // Disabling the switch destroys the message-only receiver and releases Alt+W immediately.
    internal static class Shortcuts {
        static readonly List<CastForm> windows=new List<CastForm>();
        public static readonly InputSettings Settings=InputSettings.Read();
        static HotkeyWindow receiver;static CastForm lastUsed;
        public static string Status="打开投屏窗口后生效";
        public static event Action Changed;
        public static void Add(CastForm form){windows.Add(form);lastUsed=form;Refresh();}
        public static void Remove(CastForm form){windows.Remove(form);if(lastUsed==form)lastUsed=windows.LastOrDefault();Refresh();}
        public static void Touch(CastForm form){lastUsed=form;}
        public static void Apply(){Settings.Save();Refresh();if(Changed!=null)Changed();}
        static void Refresh(){
            bool wanted=Settings.Enabled&&Settings.AltW&&windows.Any(f=>!f.IsDisposed);
            if(!wanted&&receiver!=null){receiver.Dispose();receiver=null;}
            if(wanted&&receiver==null){var next=new HotkeyWindow();if(next.Registered)receiver=next;else next.Dispose();}
            Status=!Settings.Enabled?"已关闭 · 不注册快捷键，滚轮恢复正常滚动":!Settings.AltW?"Alt+W 已关闭 · 不占用快捷键":!wanted?"打开投屏窗口后生效":receiver!=null?"Alt+W 已启用 · 可在其他软件前台时使用":"Alt+W 被其他软件占用 · 请释放后重新开启";
        }
        static void Invoke(){
            if(!Settings.Enabled||!Settings.AltW)return;
            IntPtr active=Native.GetAncestor(Native.GetForegroundWindow(),2);
            var target=windows.LastOrDefault(f=>!f.IsDisposed&&f.Visible&&f.Enabled&&f.Handle==active);
            if(target==null)target=lastUsed;
            if(target==null||target.IsDisposed||!target.Enabled)return;
            if(target.HiddenToTray){if(Settings.ToggleHidden)target.RestoreFromTray();}
            else target.HideToTray();
        }
        sealed class HotkeyWindow:NativeWindow,IDisposable {
            const int Id=0xAD21;public readonly bool Registered;
            public HotkeyWindow(){CreateHandle(new CreateParams{Caption="AppDeck shortcuts",Parent=new IntPtr(-3)});Registered=Native.RegisterHotKey(Handle,Id,0x4001,(uint)Keys.W);}
            protected override void WndProc(ref Message m){if(m.Msg==0x312&&m.WParam.ToInt32()==Id)Invoke();else base.WndProc(ref m);}
            public void Dispose(){if(Handle!=IntPtr.Zero){if(Registered)Native.UnregisterHotKey(Handle,Id);DestroyHandle();}}
        }
    }

    internal sealed class ShortcutDialog:ModernForm {
        readonly ToggleRow master,alt,wheel;readonly SelectBox behavior=new SelectBox(),quiet=new SelectBox();
        readonly Label status=Theme.Hint("",9);bool loading=true;
        public ShortcutDialog(){
            Text="快捷键";ClientSize=new Size(520,650);MinimumSize=new Size(520,650);StartPosition=FormStartPosition.CenterParent;
            var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(26,18,26,18)};Body.Controls.Add(panel);
            var heading=Theme.Label("按你的习惯操作",20,true);heading.Margin=new Padding(0,0,0,6);panel.Controls.Add(heading);
            var hint=Theme.Hint("设置即时生效，适用于所有独立投屏窗口。",9);hint.Margin=new Padding(0,0,0,16);panel.Controls.Add(hint);
            var s=Shortcuts.Settings;
            master=new ToggleRow("启用快捷操作","关闭后释放 Alt+W，停用滚轮音量映射",s.Enabled);
            alt=new ToggleRow("Alt + W","快速隐藏最近使用的投屏窗口",s.AltW);
            foreach(var row in new[]{master,alt}){row.Width=450;row.Height=58;row.Margin=Padding.Empty;panel.Controls.Add(row);}
            var modeLabel=Theme.Hint("Alt + W 的行为",8);modeLabel.Margin=new Padding(0,12,0,6);panel.Controls.Add(modeLabel);
            behavior.Width=450;behavior.Items.AddRange(new object[]{"隐藏 / 显示切换","仅隐藏（从托盘恢复）"});behavior.SelectedIndex=s.ToggleHidden?0:1;behavior.Margin=new Padding(0,0,0,16);panel.Controls.Add(behavior);
            wheel=new ToggleRow("鼠标滚轮触发音量键","向上：音量加 · 向下：音量减",s.WheelVolume){Width=450,Height=58,Margin=Padding.Empty};panel.Controls.Add(wheel);
            var explanation=Theme.Hint("需在小说应用内开启“音量键翻页”。\n仅作用于投屏画面；一次连续滚动只触发一页。",9);explanation.Margin=new Padding(0,5,0,12);panel.Controls.Add(explanation);
            panel.Controls.Add(Theme.Hint("两次滚动之间的最短停顿",8));quiet.Width=450;quiet.Items.AddRange(new object[]{"250 毫秒 · 灵敏","350 毫秒 · 默认","500 毫秒 · 稳定"});quiet.SelectedIndex=s.WheelQuietMs<=250?0:s.WheelQuietMs>=500?2:1;quiet.Margin=new Padding(0,0,0,15);panel.Controls.Add(quiet);
            status.AutoSize=false;status.Size=new Size(450,40);panel.Controls.Add(status);
            var done=Theme.Button("完成",(a,b)=>Close(),true);done.Width=450;done.Margin=new Padding(0,8,0,0);panel.Controls.Add(done);
            master.CheckedChanged+=(a,b)=>Save();alt.CheckedChanged+=(a,b)=>Save();wheel.CheckedChanged+=(a,b)=>Save();behavior.SelectedIndexChanged+=(a,b)=>Save();quiet.SelectedIndexChanged+=(a,b)=>Save();
            Theme.Watch(this,()=>panel.BackColor=Theme.Paper);loading=false;UpdateState();
        }
        void Save(){if(loading)return;var s=Shortcuts.Settings;s.Enabled=master.Checked;s.AltW=alt.Checked;s.ToggleHidden=behavior.SelectedIndex==0;s.WheelVolume=wheel.Checked;s.WheelQuietMs=new[]{250,350,500}[Math.Max(0,quiet.SelectedIndex)];try{Shortcuts.Apply();UpdateState();}catch(Exception ex){status.Text="保存失败："+ex.Message;}}
        void UpdateState(){alt.Enabled=wheel.Enabled=master.Checked;behavior.Enabled=master.Checked&&alt.Checked;quiet.Enabled=master.Checked&&wheel.Checked;status.Text=Shortcuts.Status;}
    }
}
