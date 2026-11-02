using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AppDeck {
    internal enum DockEdge { None,Left,Right,Top,Bottom }
    internal sealed class DockDisplay {
        public string Name;
        public Rectangle Area;
        public static DockDisplay[] Read(){return Screen.AllScreens.Select(s=>new DockDisplay{Name=s.DeviceName,Area=s.WorkingArea}).ToArray();}
    }
    internal static class EdgeDockLayout {
        public static Rectangle Align(Rectangle bounds,Rectangle area,DockEdge edge){
            if(edge==DockEdge.Left||edge==DockEdge.Right){bounds.X=edge==DockEdge.Left?area.Left:area.Right-bounds.Width;bounds.Y=Clamp(bounds.Y,area.Top,area.Bottom-bounds.Height);}
            if(edge==DockEdge.Top||edge==DockEdge.Bottom){bounds.Y=edge==DockEdge.Top?area.Top:area.Bottom-bounds.Height;bounds.X=Clamp(bounds.X,area.Left,area.Right-bounds.Width);}
            return bounds;
        }
        static int Clamp(int n,int low,int high){return Math.Max(low,Math.Min(n,high));}
        public static DockEdge Nearest(Rectangle bounds,Rectangle area,int distance,DockEdge preferred=DockEdge.None){
            var edges=new[]{DockEdge.Left,DockEdge.Right,DockEdge.Top,DockEdge.Bottom};
            var gaps=new[]{Math.Abs((long)bounds.Left-area.Left),Math.Abs((long)bounds.Right-area.Right),Math.Abs((long)bounds.Top-area.Top),Math.Abs((long)bounds.Bottom-area.Bottom)};
            long best=distance+1L;DockEdge chosen=DockEdge.None;
            for(int i=0;i<edges.Length;i++)if(gaps[i]<best||gaps[i]==best&&edges[i]==preferred){best=gaps[i];chosen=edges[i];}
            return best<=distance?chosen:DockEdge.None;
        }
        public static Rectangle Tab(Rectangle bounds,Rectangle area,DockEdge edge,int thickness){
            bounds=Rectangle.Intersect(bounds,area);thickness=Math.Max(2,thickness);
            if(edge==DockEdge.Left||edge==DockEdge.Right)return new Rectangle(edge==DockEdge.Left?area.Left:area.Right-thickness,bounds.Top,thickness,Math.Max(1,bounds.Height));
            return new Rectangle(bounds.Left,edge==DockEdge.Top?area.Top:area.Bottom-thickness,Math.Max(1,bounds.Width),thickness);
        }
        public static Rectangle Retracted(Rectangle bounds,Rectangle area,DockEdge edge,int thickness){
            if(edge==DockEdge.Left)bounds.X=area.Left-bounds.Width+thickness;
            if(edge==DockEdge.Right)bounds.X=area.Right-thickness;
            if(edge==DockEdge.Top)bounds.Y=area.Top-bounds.Height+thickness;
            if(edge==DockEdge.Bottom)bounds.Y=area.Bottom-thickness;
            return bounds;
        }
        public static Rectangle Interpolate(Rectangle from,Rectangle to,double progress){
            double t=Math.Max(0,Math.Min(1,progress)),ease=1-Math.Pow(1-t,3);
            return new Rectangle((int)Math.Round(from.X+(to.X-from.X)*ease),(int)Math.Round(from.Y+(to.Y-from.Y)*ease),to.Width,to.Height);
        }
        public static DockDisplay DisplayFor(Rectangle bounds,DockDisplay[] displays){
            return displays.OrderByDescending(s=>{var r=Rectangle.Intersect(bounds,s.Area);return (long)Math.Max(0,r.Width)*Math.Max(0,r.Height);})
                .ThenBy(s=>Math.Abs((long)bounds.Left+bounds.Width/2-(s.Area.Left+s.Area.Width/2))+Math.Abs((long)bounds.Top+bounds.Height/2-(s.Area.Top+s.Area.Height/2))).First();
        }
    }
    internal sealed class EdgeDockController:NativeWindow,IDisposable {
        readonly Form owner;
        readonly Func<bool> busy;
        readonly Func<Point> pointer;
        readonly Func<long> clock;
        readonly Func<DockDisplay[]> displays;
        readonly Func<Point,IntPtr> rootAtPoint;
        readonly Timer timer=new Timer{Interval=40};
        readonly Stopwatch watch=Stopwatch.StartNew();
        EdgeTab tab;
        DockDisplay display;
        Rectangle expanded;
        DockEdge candidate;
        bool enabled=true,disposed,dragging,moved,applying,changingVisibility,pointerDrag,animating,hiding;
        Point dragAnchor;
        Rectangle dragBounds,animationFrom,animationTo;
        long animationAt;
        long leaveAt=-1,hoverAt=-1,holdUntil,nextDisplayCheck;
        public DockEdge Edge {get;private set;}
        public bool Collapsed {get;private set;}
        public bool Revealing {get;private set;}
        public bool Animating {get{return animating;}}
        public Rectangle ExpandedBounds {get{return expanded;}}
        internal Rectangle TabBounds {get{return tab==null?Rectangle.Empty:tab.Bounds;}}
        internal IntPtr TabHandle {get{return tab==null?IntPtr.Zero:tab.Handle;}}
        public bool Enabled {get{return enabled;}set{if(enabled==value)return;enabled=value;if(!enabled){bool show=Collapsed||animating;PrepareForInteraction();if(show)ShowOwner(false);Clear();}}}
        public EdgeDockController(Form owner,Func<bool> busy=null,Func<Point> pointer=null,Func<long> clock=null,Func<DockDisplay[]> displays=null,Func<Point,IntPtr> rootAtPoint=null){
            this.owner=owner;this.busy=busy??(()=>false);this.pointer=pointer??(()=>Cursor.Position);this.clock=clock??(()=>watch.ElapsedMilliseconds);this.displays=displays??DockDisplay.Read;
            this.rootAtPoint=rootAtPoint??(point=>Native.GetAncestor(Native.WindowFromPoint(new ScreenPoint(point)),2));
            owner.HandleCreated+=HandleCreated;owner.HandleDestroyed+=HandleDestroyed;owner.VisibleChanged+=VisibleChanged;owner.LocationChanged+=BoundsChanged;owner.SizeChanged+=BoundsChanged;owner.Disposed+=OwnerDisposed;
            timer.Tick+=(s,e)=>Poll();if(owner.IsHandleCreated)AssignHandle(owner.Handle);
        }
        int Scale(int value){return Math.Max(2,(int)Math.Round(value*owner.DeviceDpi/96f));}
        void HandleCreated(object sender,EventArgs e){if(!disposed)AssignHandle(owner.Handle);}
        void HandleDestroyed(object sender,EventArgs e){ReleaseHandle();if(tab!=null)tab.Hide();timer.Stop();}
        void OwnerDisposed(object sender,EventArgs e){Dispose();}
        void VisibleChanged(object sender,EventArgs e){
            if(changingVisibility||disposed)return;
            if(!owner.Visible){CancelAnimation();Collapsed=false;if(tab!=null)tab.Hide();timer.Stop();}
            else if(Edge!=DockEdge.None){holdUntil=clock()+300;ResetDelays();timer.Start();}
        }
        void BoundsChanged(object sender,EventArgs e){
            if(disposed||applying||dragging||Edge==DockEdge.None)return;
            if(owner.WindowState!=FormWindowState.Normal){PrepareForInteraction();Clear();return;}
            if(display==null)return;
            if(EdgeDockLayout.Nearest(owner.Bounds,display.Area,Scale(2),Edge)!=Edge){PrepareForInteraction();Clear();return;}
            expanded=owner.Bounds;if(Collapsed&&tab!=null)tab.Bounds=EdgeDockLayout.Tab(expanded,display.Area,Edge,Scale(4));
        }
        void ResetDelays(){leaveAt=hoverAt=-1;}
        void Clear(){CancelAnimation();Edge=DockEdge.None;candidate=DockEdge.None;ResetDelays();timer.Stop();if(tab!=null)tab.Hide();}
        protected override void WndProc(ref Message m){
            if(!disposed&&enabled){
                if(m.Msg==0x231){PrepareForInteraction();dragging=true;moved=false;candidate=DockEdge.None;dragAnchor=pointer();dragBounds=owner.Bounds;pointerDrag=Control.MouseButtons!=MouseButtons.None;ResetDelays();}
                if(m.Msg==0x216&&owner.WindowState==FormWindowState.Normal&&m.LParam!=IntPtr.Zero){
                    var rect=(WindowRect)Marshal.PtrToStructure(m.LParam,typeof(WindowRect));var bounds=Rectangle.FromLTRB(rect.Left,rect.Top,rect.Right,rect.Bottom);
                    Point current=pointer();if(dragging&&(pointerDrag||current!=dragAnchor)){pointerDrag=true;bounds.X=dragBounds.Left+current.X-dragAnchor.X;bounds.Y=dragBounds.Top+current.Y-dragAnchor.Y;}
                    var screens=displays();if(screens.Length>0){var screen=EdgeDockLayout.DisplayFor(bounds,screens);candidate=EdgeDockLayout.Nearest(bounds,screen.Area,Scale(14),Edge);
                        moved=true;if(candidate!=DockEdge.None)bounds=EdgeDockLayout.Align(bounds,screen.Area,candidate);
                        if(candidate!=DockEdge.None||pointerDrag){Marshal.StructureToPtr(new WindowRect(bounds),m.LParam,false);m.Result=new IntPtr(1);return;}}
                }
            }
            base.WndProc(ref m);
            if(m.Msg==0x232&&!disposed&&enabled){dragging=false;FinishMove();}
        }
        void FinishMove(){
            if(owner.WindowState!=FormWindowState.Normal){Clear();return;}
            var screens=displays();if(screens.Length==0){Clear();return;}
            var screen=EdgeDockLayout.DisplayFor(owner.Bounds,screens);
            var edge=EdgeDockLayout.Nearest(owner.Bounds,screen.Area,moved?Scale(14):Scale(2),candidate!=DockEdge.None?candidate:Edge);
            if(edge==DockEdge.None||!moved&&Edge==DockEdge.None){Clear();return;}
            display=screen;Edge=edge;expanded=EdgeDockLayout.Align(owner.Bounds,display.Area,Edge);Move(expanded);
            holdUntil=clock()+100;nextDisplayCheck=clock()+1000;ResetDelays();timer.Start();
        }
        void Move(Rectangle bounds){
            applying=true;try{Native.SetWindowPos(owner.Handle,IntPtr.Zero,bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0214);}finally{applying=false;}
        }
        bool PointerInside(Point point,IntPtr window,Rectangle bounds){
            return bounds.Contains(point)&&rootAtPoint(point)==window;
        }
        internal void Poll(){
            if(disposed||!enabled||Edge==DockEdge.None||!owner.IsHandleCreated)return;
            long now=clock();
            if(now>=nextDisplayCheck){
                nextDisplayCheck=now+1000;var screens=displays();if(screens.Length==0)return;
                var current=screens.FirstOrDefault(s=>s.Name==display.Name);
                if(current==null){bool show=Collapsed||animating;PrepareForInteraction();var screen=EdgeDockLayout.DisplayFor(expanded,screens);expanded=EdgeDockLayout.Align(expanded,screen.Area,DockEdge.Top);Move(expanded);if(show)ShowOwner(false);Clear();return;}
                if(current.Area!=display.Area){CancelAnimation();display=current;expanded=EdgeDockLayout.Align(expanded,current.Area,Edge);Move(expanded);if(tab!=null)tab.Bounds=EdgeDockLayout.Tab(expanded,current.Area,Edge,Scale(4));}
            }
            if(dragging||!owner.Enabled||owner.WindowState!=FormWindowState.Normal||busy()||Control.MouseButtons!=MouseButtons.None||owner.OwnedForms.Any(f=>f.Visible)){if(animating){CancelAnimation();Move(expanded);}ResetDelays();return;}
            Point point=pointer();
            if(animating){if(hiding&&PointerInside(point,owner.Handle,owner.Bounds))Reveal(false);AdvanceAnimation(now);return;}
            if(Collapsed){
                if(tab!=null&&tab.Visible&&PointerInside(point,tab.Handle,tab.Bounds)){
                    if(hoverAt<0)hoverAt=now;if(now-hoverAt>=40)Reveal(false);
                }else hoverAt=-1;
            }else if(owner.Visible){
                if(PointerInside(point,owner.Handle,owner.Bounds)){leaveAt=-1;return;}
                if(now<holdUntil)return;
                if(leaveAt<0)leaveAt=now;if(now-leaveAt>=220)Collapse();
            }
        }
        void Collapse(){
            if(Collapsed||Edge==DockEdge.None)return;
            expanded=owner.Bounds;ResetDelays();
            if(tab==null){tab=new EdgeTab();tab.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left)Reveal(true);};}
            tab.Bounds=EdgeDockLayout.Tab(expanded,display.Area,Edge,Scale(4));
            StartAnimation(true,owner.Bounds,EdgeDockLayout.Retracted(expanded,display.Area,Edge,Scale(4)));
        }
        void Reveal(bool activate){
            if(!Collapsed&&!animating)return;
            Rectangle start=animating?owner.Bounds:EdgeDockLayout.Retracted(expanded,display.Area,Edge,Scale(4));
            Collapsed=false;if(tab!=null)tab.Hide();ResetDelays();holdUntil=clock()+160;
            Clip(start);Move(start);ShowOwner(activate);StartAnimation(false,start,expanded);
        }
        void ShowOwner(bool activate){
            changingVisibility=true;Revealing=!activate;bool wasEnabled=owner.Enabled;
            if(!activate&&wasEnabled)owner.Enabled=false;
            try{owner.Show();Native.SetWindowPos(owner.Handle,owner.TopMost?new IntPtr(-1):IntPtr.Zero,0,0,0,0,0x0213);if(activate)owner.Activate();}
            finally{if(!activate&&wasEnabled)owner.Enabled=true;Revealing=false;changingVisibility=false;}
        }
        void StartAnimation(bool hide,Rectangle from,Rectangle to){
            animating=true;hiding=hide;animationFrom=from;animationTo=to;animationAt=clock();timer.Interval=15;timer.Start();
        }
        void AdvanceAnimation(long now){
            double progress=(now-animationAt)/140.0;var bounds=EdgeDockLayout.Interpolate(animationFrom,animationTo,progress);Clip(bounds);Move(bounds);
            if(progress<1)return;animating=false;timer.Interval=40;
            if(hiding){
                Collapsed=true;changingVisibility=true;try{owner.Hide();CancelAnimation();Move(expanded);tab.Show();}finally{changingVisibility=false;}
            }else{CancelAnimation();Move(expanded);holdUntil=clock()+100;}
        }
        void Clip(Rectangle bounds){
            var visible=Rectangle.Intersect(bounds,display.Area);IntPtr region=Native.CreateRectRgn(visible.Left-bounds.Left,visible.Top-bounds.Top,visible.Right-bounds.Left,visible.Bottom-bounds.Top);
            if(region!=IntPtr.Zero&&Native.SetWindowRgn(owner.Handle,region,true)==0)Native.DeleteObject(region);
        }
        void CancelAnimation(){
            animating=false;timer.Interval=40;if(owner.IsHandleCreated)Native.SetWindowRgn(owner.Handle,IntPtr.Zero,true);
        }
        public void PrepareForInteraction(){
            bool restore=animating;CancelAnimation();if(restore)Move(expanded);Collapsed=false;if(tab!=null)tab.Hide();timer.Stop();ResetDelays();holdUntil=clock()+300;
        }
        public void PrepareForClose(){PrepareForInteraction();Clear();}
        public void Dispose(){
            if(disposed)return;disposed=true;CancelAnimation();timer.Stop();timer.Dispose();
            owner.HandleCreated-=HandleCreated;owner.HandleDestroyed-=HandleDestroyed;owner.VisibleChanged-=VisibleChanged;owner.LocationChanged-=BoundsChanged;owner.SizeChanged-=BoundsChanged;owner.Disposed-=OwnerDisposed;
            if(Handle!=IntPtr.Zero)ReleaseHandle();if(tab!=null)tab.Dispose();watch.Stop();
        }
        [StructLayout(LayoutKind.Sequential)]struct WindowRect {
            public int Left,Top,Right,Bottom;
            public WindowRect(Rectangle r){Left=r.Left;Top=r.Top;Right=r.Right;Bottom=r.Bottom;}
        }
        sealed class EdgeTab:Form {
            public EdgeTab(){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;StartPosition=FormStartPosition.Manual;AutoScaleMode=AutoScaleMode.None;DoubleBuffered=true;Text="AppDeck · 移入展开投屏";Cursor=Cursors.Hand;Theme.Watch(this,()=>{BackColor=Theme.Accent;Invalidate();});}
            protected override bool ShowWithoutActivation{get{return true;}}
            protected override CreateParams CreateParams{get{var cp=base.CreateParams;cp.ExStyle|=0x08000080;return cp;}}
            protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);}
        }
    }
    [StructLayout(LayoutKind.Sequential)]internal struct ScreenPoint {
        public int X,Y;
        public ScreenPoint(Point point){X=point.X;Y=point.Y;}
    }
}
