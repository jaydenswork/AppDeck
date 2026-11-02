using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppDeck {
    internal static class EdgeDockChecks {
        static void Check(bool ok,string name){if(!ok)throw new Exception("edge dock: "+name);}
        public static void Geometry(){
            var area=new Rectangle(-1920,40,1920,1040);var size=new Size(360,600);
            foreach(DockEdge edge in new[]{DockEdge.Left,DockEdge.Right,DockEdge.Top,DockEdge.Bottom}){
                var aligned=EdgeDockLayout.Align(new Rectangle(-1500,200,size.Width,size.Height),area,edge);
                Check(EdgeDockLayout.Nearest(aligned,area,14,edge)==edge,"all four edges on a negative-coordinate monitor");
                Check(aligned.Size==size&&area.Contains(aligned),"snapping preserves dimensions and respects working area");
                var strip=EdgeDockLayout.Tab(aligned,area,edge,4);
                Check(area.Contains(strip)&&(strip.Width==4||strip.Height==4),"reveal strip remains inside monitor working area");
                var hidden=EdgeDockLayout.Retracted(aligned,area,edge,4);var visible=Rectangle.Intersect(hidden,area);
                Check(hidden.Size==size&&(visible.Width==4||visible.Height==4),"retracted position exposes only an edge strip");
                Check(EdgeDockLayout.Interpolate(aligned,hidden,0)==aligned&&EdgeDockLayout.Interpolate(aligned,hidden,1)==hidden,"animation endpoints preserve window geometry");
            }
            Check(EdgeDockLayout.Nearest(new Rectangle(-1200,200,360,600),area,14)==DockEdge.None,"free windows do not dock");
            Check(EdgeDockLayout.Nearest(new Rectangle(area.Left+14,200,360,600),area,14)==DockEdge.Left&&EdgeDockLayout.Nearest(new Rectangle(area.Left+15,200,360,600),area,14)==DockEdge.None,"snap threshold boundary");
            Check(EdgeDockLayout.Nearest(new Rectangle(area.Left,area.Top,360,600),area,14,DockEdge.Top)==DockEdge.Top,"corner keeps preferred edge");
            Check(EdgeDockLayout.Align(new Rectangle(-1500,-100,360,1600),area,DockEdge.Left).Top==area.Top,"oversized windows keep their title accessible");
            var monitors=new[]{new DockDisplay{Name="left",Area=area},new DockDisplay{Name="right",Area=new Rectangle(0,0,1920,1040)}};
            Check(EdgeDockLayout.DisplayFor(new Rectangle(-100,100,600,600),monitors).Name=="right","monitor selection follows greatest overlap");
        }
        public static int Desktop(string folder){
            Directory.CreateDirectory(folder);int result=1;
            using(var runner=new Runner()){runner.Shown+=async(s,e)=>{
                try{await Windows(folder);File.WriteAllText(Path.Combine(folder,"edge-dock-check.txt"),"PASS: four-edge native move messages; cumulative pointer movement detaches; 220 ms hide / 40 ms hover delays; 140 ms clipped slide with unchanged HWND and dimensions; no activation on hover; menu/dialog/drag pause; pin preservation; minimize/maximize; tray integration; monitor work-area change and disconnect; disposal; main-window isolation; cast and main caption close buttons; immediate hide with delayed cleanup; responsive UI while pending; failed return restores window; retry and duplicate-close coalescing. Real HWND operations with injected pointer, hit target, time and display topology on the primary monitor; no physical cursor movement or Android device access. Physical mouse feel and actual scrcpy playback require separate acceptance.\r\n",Encoding.UTF8);result=0;}
                catch(Exception ex){File.WriteAllText(Path.Combine(folder,"edge-dock-check.txt"),ex.ToString(),Encoding.UTF8);}
                finally{runner.Close();}
            };Application.Run(runner);}return result;
        }
        static async Task Windows(string folder){
            Geometry();var area=Screen.PrimaryScreen.WorkingArea;long now=0;bool busy=false,occluded=false;Point point=new Point(area.Right+100,area.Bottom+100);
            var screens=new[]{new DockDisplay{Name="test",Area=area}};Process childProcess=null;IntPtr child=IntPtr.Zero;
            using(var focus=new Form{Text="AppDeck focus check",ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(area.Left+area.Width/2,area.Top+area.Height/2,120,90)})
            using(var form=new CheckForm{Text="AppDeck edge check",StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(area.Left+90,area.Top+100,360,300),TopMost=true}){
                EdgeDockController controller=null;
                form.DockController=controller=new EdgeDockController(form,()=>busy,()=>point,()=>now,()=>screens,p=>occluded?IntPtr.Zero:controller.Collapsed&&controller.TabBounds.Contains(p)?controller.TabHandle:form.Bounds.Contains(p)?Native.GetAncestor(child==IntPtr.Zero?form.Body.Handle:child,2):IntPtr.Zero);
                var dock=form.DockController;form.Show();focus.Show();await Task.Delay(50);
                if(form.NativeBorderApplied){int thickness;Check(DwmGetWindowAttribute(form.Handle,37,out thickness,4)==0&&thickness>0,"DWM reports a visible native frame border");}
                try{
                    childProcess=Process.Start(new ProcessStartInfo(Application.ExecutablePath,"--edge-test-child"){UseShellExecute=false,CreateNoWindow=true});
                    bool ready=await Task.Run(()=>childProcess.WaitForInputIdle(5000));Check(ready,"test child starts");
                    for(int attempt=0;attempt<20&&child==IntPtr.Zero;attempt++){EnumWindows((h,p)=>{uint pid;Native.GetWindowThreadProcessId(h,out pid);if(pid==childProcess.Id&&Native.IsWindowVisible(h)){child=h;return false;}return true;},IntPtr.Zero);if(child==IntPtr.Zero)await Task.Delay(50);}
                    Check(child!=IntPtr.Zero,"foreign child HWND exists");Native.Embed(child,form.Body.Handle);Native.MoveWindow(child,0,0,form.Body.Width,form.Body.Height,true);
                    var ownerHandle=form.Handle;var originalSize=form.Size;
                    foreach(DockEdge edge in new[]{DockEdge.Left,DockEdge.Right,DockEdge.Top,DockEdge.Bottom}){
                        Drag(form,EdgeDockLayout.Align(new Rectangle(area.Left+90,area.Top+100,form.Width,form.Height),area,edge));
                        Check(dock.Edge==edge,"native drag docks "+edge);focus.Activate();await Task.Delay(25);var foreground=Native.GetForegroundWindow();
                        point=form.Body.PointToScreen(new Point(30,30));now+=700;dock.Poll();now+=700;dock.Poll();
                        Check(form.Visible&&!dock.Collapsed,"pointer in embedded child prevents collapse");
                        point=new Point(area.Right+100,area.Bottom+100);now+=300;dock.Poll();now+=219;dock.Poll();Check(!dock.Collapsed&&!dock.Animating,"collapse delay");now+=2;dock.Poll();
                        Check(dock.Animating&&form.Visible,"collapse starts a slide instead of disappearing");now+=70;dock.Poll();Check(form.Bounds!=dock.ExpandedBounds&&form.Size==originalSize,"animation moves without resizing");now+=80;dock.Poll();
                        Check(dock.Collapsed&&!form.Visible&&Native.IsWindow(form.Handle)&&Native.IsWindow(child),"collapse keeps both HWNDs "+edge);
                        Check(Native.GetParent(child)==form.Body.Handle&&form.Handle==ownerHandle&&form.Size==originalSize,"native child parent and size preserved "+edge);
                        Check(area.Contains(dock.TabBounds),"strip stays within working area "+edge);
                        foreground=Native.GetForegroundWindow();int activations=form.Activations;
                        point=new Point(dock.TabBounds.Left+dock.TabBounds.Width/2,dock.TabBounds.Top+dock.TabBounds.Height/2);now+=40;dock.Poll();now+=39;dock.Poll();Check(dock.Collapsed,"hover delay");now+=2;dock.Poll();
                        Check(dock.Animating&&form.Visible,"hover starts a reveal animation");FinishAnimation(dock,ref now);
                        Check(!dock.Collapsed&&form.Visible&&form.Bounds==dock.ExpandedBounds&&form.TopMost,"hover reveals without changing pin "+edge);
                        Check(form.Activations==activations,"hover does not activate "+edge);
                    }
                    busy=true;point=new Point(area.Right+100,area.Bottom+100);now+=1000;dock.Poll();now+=1000;dock.Poll();Check(!dock.Collapsed,"open menu pauses collapse");busy=false;
                    using(var dialog=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Bounds=new Rectangle(area.Left+area.Width/2,area.Top+100,120,90)}){
                        dialog.Show(form);now+=1000;dock.Poll();now+=1000;dock.Poll();Check(!dock.Collapsed,"owned dialog pauses collapse");dialog.Close();
                    }
                    SendMessage(form.Handle,0x231,IntPtr.Zero,IntPtr.Zero);now+=1000;dock.Poll();now+=1000;dock.Poll();Check(!dock.Collapsed,"dragging pauses collapse");SendMessage(form.Handle,0x232,IntPtr.Zero,IntPtr.Zero);
                    point=form.Body.PointToScreen(new Point(30,30));occluded=true;Collapse(dock,ref now);Check(dock.Collapsed,"another window covering the pointer counts as leaving");occluded=false;
                    dock.Enabled=false;dock.Enabled=true;form.TopMost=false;
                    Drag(form,EdgeDockLayout.Align(form.Bounds,area,DockEdge.Left));point=new Point(area.Right+100,area.Bottom+100);Collapse(dock,ref now);
                    point=new Point(dock.TabBounds.Left+dock.TabBounds.Width/2,dock.TabBounds.Top+dock.TabBounds.Height/2);now+=100;dock.Poll();now+=45;dock.Poll();FinishAnimation(dock,ref now);Check(form.Visible&&!form.TopMost,"hover preserves an unpinned window");form.TopMost=true;
                    Drag(form,new Rectangle(area.Left,area.Top+100,360,300));
                    point=form.Caption.PointToScreen(new Point(70,20));SendMessage(form.Handle,0x231,IntPtr.Zero,IntPtr.Zero);
                    for(int step=0;step<8;step++){point.Offset(5,0);var proposal=form.Bounds;proposal.Offset(5,0);form.Bounds=Moving(form,proposal);}
                    SendMessage(form.Handle,0x232,IntPtr.Zero,IntPtr.Zero);Check(dock.Edge==DockEdge.None&&form.Left>=area.Left+35,"small consecutive mouse movements detach from the edge");
                    Drag(form,new Rectangle(area.Left+area.Width/2-180,area.Top+area.Height/2-150,360,300));Check(dock.Edge==DockEdge.None,"dragging away undocks");
                    now+=1000;dock.Poll();now+=1000;dock.Poll();Check(form.Visible,"free window remains visible");
                    Drag(form,EdgeDockLayout.Align(form.Bounds,area,DockEdge.Left));form.WindowState=FormWindowState.Maximized;await Task.Delay(25);Check(dock.Edge==DockEdge.None&&!dock.Collapsed,"maximizing cancels docking");form.WindowState=FormWindowState.Normal;
                    Drag(form,EdgeDockLayout.Align(new Rectangle(area.Left+90,area.Top+100,360,300),area,DockEdge.Left));form.WindowState=FormWindowState.Minimized;await Task.Delay(25);Check(dock.Edge==DockEdge.None,"minimize has no reveal strip");form.WindowState=FormWindowState.Normal;
                    Drag(form,EdgeDockLayout.Align(new Rectangle(area.Left+90,area.Top+100,360,300),area,DockEdge.Right));point=new Point(area.Right+100,area.Bottom+100);Collapse(dock,ref now);Check(dock.Collapsed,"collapsed before monitor change");
                    var newArea=new Rectangle(area.Left+30,area.Top+20,area.Width-70,area.Height-50);screens=new[]{new DockDisplay{Name="test",Area=newArea}};now+=1100;dock.Poll();Check(newArea.Contains(dock.TabBounds),"working area change moves strip");
                    screens=new[]{new DockDisplay{Name="replacement",Area=area}};now+=1100;dock.Poll();Check(form.Visible&&!dock.Collapsed&&dock.Edge==DockEdge.None&&area.Contains(form.Bounds),"removed monitor restores reachable window");
                    form.DrawToBitmapAndSave(Path.Combine(folder,"edge-dock-window.png"));
                    Drag(form,EdgeDockLayout.Align(form.Bounds,area,DockEdge.Left));Collapse(dock,ref now);dock.Enabled=false;Check(form.Visible&&!dock.Collapsed&&dock.Edge==DockEdge.None,"disabling reveals collapsed window");
                    Check(Native.IsWindow(child)&&form.Handle==ownerHandle,"full cycle preserves native child");
                    var stripHandle=dock.TabHandle;dock.Dispose();Check(!Native.IsWindow(stripHandle)&&Native.IsWindow(child),"disposal removes only the reveal strip");
                }finally{
                    if(child!=IntPtr.Zero&&Native.IsWindow(child))Native.PostMessage(child,0x10,IntPtr.Zero,IntPtr.Zero);
                    if(childProcess!=null){if(!childProcess.WaitForExit(2000))childProcess.Kill();childProcess.Dispose();}
                    dock.Dispose();form.Close();focus.Close();
                }
            }
            using(var cast=new CastForm("preview",new AndroidApp{Name="贴边测试",Package="io.appdeck.edgecheck"},"贴边测试",0,false,false,false,false,true,edgeAutoHide:false)){
                cast.StartPosition=FormStartPosition.Manual;cast.Bounds=new Rectangle(area.Left+120,area.Top+100,360,520);cast.Show();await Task.Delay(25);
                Drag(cast,EdgeDockLayout.Align(cast.Bounds,area,DockEdge.Left));Check(!cast.EdgeDock.Enabled&&cast.EdgeDock.Edge==DockEdge.None,"new cast honors disabled auto-hide preference");
                cast.EdgeDock.Enabled=true;
                Drag(cast,EdgeDockLayout.Align(cast.Bounds,area,DockEdge.Left));Check(cast.EdgeDock.Edge==DockEdge.Left,"cast window owns docking controller");
                var handle=cast.Handle;cast.HideToTray();Check(cast.HiddenToTray&&!cast.Visible&&!cast.EdgeDock.Collapsed,"Alt+W tray behavior remains distinct");
                cast.RestoreFromTray();Check(cast.Visible&&!cast.HiddenToTray&&cast.Handle==handle,"tray restoration preserves cast HWND");await cast.StopAndClose();
            }
            using(var main=new MainForm(new[]{"--preview"})){
                main.StartPosition=FormStartPosition.Manual;main.Location=new Point(area.Left+30,area.Top+30);main.CreateControl();var before=main.Bounds;
                var result=Moving(main,new Rectangle(area.Left+8,before.Top,before.Width,before.Height));Check(result.Left==area.Left+8,"main window has no snap controller");
            }
            using(var cast=new CastForm("preview",new AndroidApp{Name="延迟关闭回归",Package="io.appdeck.delayedclose"},"延迟关闭回归",0,false,false,false,false,true)){
                cast.StartPosition=FormStartPosition.Manual;cast.Bounds=new Rectangle(area.Left+100,area.Top+100,360,520);cast.Show();await Task.Delay(25);
                var cleanup=new TaskCompletionSource<bool>();int calls=0;cast.CloseCleanupForChecks=()=>{calls++;return cleanup.Task;};var watch=Stopwatch.StartNew();
                CloseButton(cast).PerformClick();var closing=cast.StopAndClose();
                Check(!cast.Visible&&cast.ClosingRequested&&!cast.IsDisposed&&watch.ElapsedMilliseconds<200,"close immediately hides the window while preserving cleanup ownership");
                Check(Object.ReferenceEquals(closing,cast.StopAndClose()),"repeated close shares one cleanup operation");
                int ticks=0;using(var pulse=new System.Windows.Forms.Timer{Interval=20}){pulse.Tick+=(s,e)=>ticks++;pulse.Start();await Task.Delay(160);pulse.Stop();}
                Check(ticks>=2&&calls==1&&!cast.IsDisposed,"slow cleanup keeps the UI responsive and the host alive");
                cleanup.SetResult(false);await closing;Check(cast.Visible&&cast.Enabled&&!cast.ClosingRequested&&!cast.IsDisposed,"failed task return restores a usable window");
                cast.CloseCleanupForChecks=()=>Task.FromResult(true);await cast.StopAndClose();Check(cast.IsDisposed,"retry closes after cleanup succeeds");
            }
            using(var cast=new CastForm("preview",new AndroidApp{Name="关闭回归",Package="io.appdeck.closecheck"},"关闭回归",0,false,false,false,false,true)){
                cast.StartPosition=FormStartPosition.Manual;cast.Bounds=new Rectangle(area.Left+100,area.Top+100,360,520);cast.Show();await Task.Delay(25);
                CloseButton(cast).PerformClick();for(int retry=0;retry<20&&!cast.IsDisposed;retry++)await Task.Delay(25);Check(cast.IsDisposed,"cast caption close button completes cleanup");
            }
            using(var main=new MainForm(new[]{"--preview"})){
                main.StartPosition=FormStartPosition.Manual;main.Location=new Point(area.Left+20,area.Top+20);main.Show();await Task.Delay(25);
                CloseButton(main).PerformClick();for(int retry=0;retry<20&&!main.IsDisposed;retry++)await Task.Delay(25);Check(main.IsDisposed,"main caption close button exits with no active leases");
            }
        }
        static void Drag(Form form,Rectangle target){
            SendMessage(form.Handle,0x231,IntPtr.Zero,IntPtr.Zero);form.Bounds=Moving(form,target);SendMessage(form.Handle,0x232,IntPtr.Zero,IntPtr.Zero);
        }
        static void Collapse(EdgeDockController dock,ref long now){now+=1000;dock.Poll();now+=230;dock.Poll();FinishAnimation(dock,ref now);}
        static void FinishAnimation(EdgeDockController dock,ref long now){now+=70;dock.Poll();now+=80;dock.Poll();}
        static Button CloseButton(ModernForm form){foreach(Control control in form.Caption.Controls)if(control is Button&&control.AccessibleName=="关闭窗口")return (Button)control;throw new Exception("Close button missing");}
        static Rectangle Moving(Form form,Rectangle target){
            IntPtr p=Marshal.AllocHGlobal(16);try{Marshal.StructureToPtr(new Rect(target),p,false);SendMessage(form.Handle,0x216,IntPtr.Zero,p);var r=(Rect)Marshal.PtrToStructure(p,typeof(Rect));return Rectangle.FromLTRB(r.L,r.T,r.R,r.B);}finally{Marshal.FreeHGlobal(p);}
        }
        public static int Child(){using(var form=new ChildForm()){Application.Run(form);}return 0;}
        sealed class Runner:Form {
            public Runner(){ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Bounds=new Rectangle(-30000,-30000,10,10);Opacity=0;}
            protected override bool ShowWithoutActivation{get{return true;}}
        }
        sealed class ChildForm:Form {
            public ChildForm(){Text="AppDeck native child check";ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;Bounds=new Rectangle(-30000,-30000,320,240);BackColor=Theme.Paper;Controls.Add(new Label{Text="本机嵌入窗口测试\n不连接手机",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=Theme.Font(14),ForeColor=Theme.Ink});}
            protected override bool ShowWithoutActivation{get{return true;}}
        }
        sealed class CheckForm:ModernForm {
            public EdgeDockController DockController;
            public int Activations;
            protected override void OnActivated(EventArgs e){Activations++;base.OnActivated(e);}
            protected override bool ShowWithoutActivation{get{return DockController!=null&&DockController.Revealing||base.ShowWithoutActivation;}}
            public void DrawToBitmapAndSave(string path){using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(Point.Empty,Size));b.Save(path);}}
        }
        [StructLayout(LayoutKind.Sequential)]struct Rect {
            public int L,T,R,B;public Rect(Rectangle r){L=r.Left;T=r.Top;R=r.Right;B=r.Bottom;}
        }
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
        [DllImport("user32.dll")]static extern bool EnumWindows(Native.EnumProc callback,IntPtr parameter);
        [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr window,int attribute,out int value,int size);
    }
}
