using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AppDeck {
    internal static class Theme {
        public static bool Dark { get; private set; }
        public static event EventHandler Changed;
        public static Color Ink { get { return C(Dark?0xE8E8E6:0x272826); } }
        public static Color Muted { get { return C(Dark?0x9B9E98:0x747870); } }
        public static Color Paper { get { return C(Dark?0x181918:0xF7F8F5); } }
        public static Color Surface { get { return C(Dark?0x20221F:0xFFFFFF); } }
        public static Color Soft { get { return C(Dark?0x2B2E29:0xEEF0EB); } }
        public static Color Line { get { return C(Dark?0x353932:0xE0E4DC); } }
        public static Color Accent { get { return C(Dark?0xAD9DFF:0x7155E8); } }
        public static Color AccentFill { get { return C(Dark?0x8670ED:0x7155E8); } }
        public static Color AccentSoft { get { return C(Dark?0x322D4C:0xF0EBFF); } }
        public static Color Green { get { return C(Dark?0x79D6B1:0x22876A); } }
        public static Color C(int rgb){return Color.FromArgb((rgb>>16)&255,(rgb>>8)&255,rgb&255);}
        public static Font Font(float size=10,bool bold=false){return new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular);}
        public static void Set(bool dark){if(Dark==dark)return;Dark=dark;var handler=Changed;if(handler!=null)handler(null,EventArgs.Empty);}
        public static void Watch(Control control,Action apply){EventHandler handler=(s,e)=>{if(!control.IsDisposed)apply();};Changed+=handler;control.Disposed+=(s,e)=>Changed-=handler;apply();}
        public static Button Button(string text,EventHandler click,bool primary=false){var b=new ModernButton{Text=text,Primary=primary};b.Click+=click;return b;}
        public static Label Label(string text,float size=10,bool bold=false){var l=new Label{Text=text,AutoSize=true,Font=Font(size,bold),Margin=new Padding(0,0,0,8),BackColor=Color.Transparent};Watch(l,()=>l.ForeColor=Ink);return l;}
        public static Label Hint(string text,float size=9){var l=Label(text,size);Watch(l,()=>l.ForeColor=Muted);return l;}
        public static void Error(IWin32Window owner,Exception ex){MessageBox.Show(owner,ex.Message,"AppDeck",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        public static void Menu(ContextMenuStrip menu){Watch(menu,()=>{menu.BackColor=Surface;menu.ForeColor=Ink;menu.Renderer=new MenuRenderer();menu.Font=Font(9);menu.ShowImageMargin=false;menu.Padding=new Padding(5);foreach(ToolStripItem i in menu.Items){i.ForeColor=Ink;i.Padding=new Padding(10,5,10,5);}});}
        sealed class MenuRenderer:ToolStripProfessionalRenderer {
            public MenuRenderer():base(new MenuColors()){RoundedEdges=false;}
            protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=e.Item.Enabled?Ink:Muted;base.OnRenderItemText(e);}
        }
        sealed class MenuColors:ProfessionalColorTable {
            public override Color MenuItemSelected{get{return AccentSoft;}}
            public override Color MenuItemBorder{get{return AccentSoft;}}
            public override Color ToolStripDropDownBackground{get{return Surface;}}
            public override Color MenuBorder{get{return Line;}}
            public override Color SeparatorDark{get{return Line;}}
            public override Color SeparatorLight{get{return Line;}}
        }
    }
    internal static class PaintKit {
        public static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));if(d<=0){p.AddRectangle(r);return p;}p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
        public static void Fill(Graphics g,RectangleF r,Color c,float radius=10){using(var p=Round(r,radius))using(var b=new SolidBrush(c))g.FillPath(b,p);}
        public static void Border(Graphics g,RectangleF r,Color c,float radius=10,float width=1){using(var p=Round(r,radius))using(var pen=new Pen(c,width))g.DrawPath(pen,p);}
        public static void Text(Graphics g,string text,Font font,Color color,Rectangle r,TextFormatFlags flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix){TextRenderer.DrawText(g,text,font,r,color,flags);}
        public static void Icon(Graphics g,string name,RectangleF box,Color color){
            var save=g.Save();g.SmoothingMode=SmoothingMode.AntiAlias;g.TranslateTransform(box.X,box.Y);g.ScaleTransform(box.Width/24f,box.Height/24f);
            using(var p=new Pen(color,1.7f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})using(var b=new SolidBrush(color)){
                switch(name){
                case "back":g.DrawLines(p,new[]{new PointF(14,5),new PointF(7,12),new PointF(14,19)});break;
                case "arrow":g.DrawLine(p,5,12,19,12);g.DrawLines(p,new[]{new PointF(13,6),new PointF(19,12),new PointF(13,18)});break;
                case "close":g.DrawLine(p,7,7,17,17);g.DrawLine(p,17,7,7,17);break;
                case "min":g.DrawLine(p,6,12,18,12);break;
                case "max":g.DrawRectangle(p,6,6,12,12);break;
                case "restore":g.DrawRectangle(p,5,8,11,11);g.DrawLines(p,new[]{new PointF(8,5),new PointF(19,5),new PointF(19,16)});break;
                case "more":g.FillEllipse(b,4,10.5f,3,3);g.FillEllipse(b,10.5f,10.5f,3,3);g.FillEllipse(b,17,10.5f,3,3);break;
                case "search":g.DrawEllipse(p,4,4,12,12);g.DrawLine(p,14,14,20,20);break;
                case "chevron":g.DrawLines(p,new[]{new PointF(7,10),new PointF(12,15),new PointF(17,10)});break;
                case "pin":g.DrawLines(p,new[]{new PointF(8,4),new PointF(16,4),new PointF(15,10),new PointF(19,14),new PointF(5,14),new PointF(9,10),new PointF(8,4)});g.DrawLine(p,12,14,12,21);break;
                case "star":var pts=new PointF[10];for(int i=0;i<10;i++){double a=-Math.PI/2+i*Math.PI/5;float rad=i%2==0?9:4.3f;pts[i]=new PointF(12+(float)Math.Cos(a)*rad,12+(float)Math.Sin(a)*rad);}g.DrawPolygon(p,pts);break;
                case "grid":for(int y=0;y<2;y++)for(int x=0;x<2;x++)g.DrawRectangle(p,4+x*10,4+y*10,6,6);break;
                case "book":g.DrawLines(p,new[]{new PointF(3,5),new PointF(8,5),new PointF(12,7),new PointF(16,5),new PointF(21,5),new PointF(21,19),new PointF(16,19),new PointF(12,21),new PointF(8,19),new PointF(3,19),new PointF(3,5)});g.DrawLine(p,12,7,12,21);break;
                case "phone":using(var path=Round(new RectangleF(6,2,12,20),3))g.DrawPath(p,path);g.DrawLine(p,10,18,14,18);break;
                case "refresh":g.DrawArc(p,5,5,14,14,35,290);g.DrawLines(p,new[]{new PointF(19,4),new PointF(19,10),new PointF(13,10)});break;
                case "wifi":g.DrawArc(p,1,4,22,19,217,106);g.DrawArc(p,5,9,14,13,220,100);g.FillEllipse(b,10.5f,18,3,3);break;
                case "sun":g.DrawEllipse(p,8,8,8,8);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(p,12+(float)Math.Cos(a)*8,12+(float)Math.Sin(a)*8,12+(float)Math.Cos(a)*10,12+(float)Math.Sin(a)*10);}break;
                case "moon":using(var path=new GraphicsPath()){path.AddArc(3,3,18,18,40,280);path.AddArc(9,-1,15,18,125,-170);path.CloseFigure();g.DrawPath(p,path);}break;
                case "check":g.DrawLines(p,new[]{new PointF(5,12),new PointF(10,17),new PointF(19,7)});break;
                case "chat":using(var path=Round(new RectangleF(3,4,18,14),5))g.DrawPath(p,path);g.DrawLines(p,new[]{new PointF(8,18),new PointF(5,21),new PointF(5,17)});g.FillEllipse(b,7,10,2,2);g.FillEllipse(b,15,10,2,2);break;
                case "help":g.DrawEllipse(p,3,3,18,18);g.DrawArc(p,9,7,6,6,190,230);g.DrawLine(p,12,12,12,14);g.FillEllipse(b,11.2f,17,1.6f,1.6f);break;
                case "logo":using(var path=Round(new RectangleF(4,3,12,16),3))g.DrawPath(p,path);using(var path=Round(new RectangleF(9,7,12,16),3))g.DrawPath(p,path);break;
                case "disconnect":g.DrawLine(p,5,5,19,19);using(var path=Round(new RectangleF(7,2,10,20),2))g.DrawPath(p,path);break;
                }
            }g.Restore(save);
        }
    }
    internal sealed class ModernButton:Button {
        bool hover,down;public bool Primary,Quiet,Selected,AlignContentLeft;public string Glyph="";
        public ModernButton(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);AutoSize=false;Size=new Size(120,40);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;Font=Theme.Font(9.5f);Margin=new Padding(0);Theme.Watch(this,()=>{ForeColor=Theme.Ink;BackColor=Theme.Surface;Invalidate();});}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;down=false;Invalidate();base.OnMouseLeave(e);}protected override void OnMouseDown(MouseEventArgs e){down=true;Invalidate();base.OnMouseDown(e);}protected override void OnMouseUp(MouseEventArgs e){down=false;Invalidate();base.OnMouseUp(e);}protected override void OnEnabledChanged(EventArgs e){Invalidate();base.OnEnabledChanged(e);}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?Theme.Surface:Parent.BackColor);float s=g.DpiX/96f;
            var r=new RectangleF(0.5f,0.5f,Width-1,Height-1);Color bg=Primary?(Enabled?Theme.AccentFill:Theme.Soft):Selected?Theme.AccentSoft:(down||hover?Theme.Soft:BackColor);if(!Quiet||hover||Selected)PaintKit.Fill(g,r,bg,9*s);
            if(!Primary&&!Quiet&&!Selected)PaintKit.Border(g,r,Theme.Line,9*s);
            var fg=!Enabled?Theme.Muted:Primary?Color.White:Selected?Theme.Accent:Theme.Ink;
            int icon=Glyph.Length>0?(int)(18*s):0;int offset=icon>0?(int)(42*s):(int)(12*s);if(icon>0)PaintKit.Icon(g,Glyph,new RectangleF(13*s,(Height-icon)/2f,icon,icon),fg);
            PaintKit.Text(g,Text,Font,fg,new Rectangle(AlignContentLeft?offset:icon>0?(int)(34*s):0,0,Width-(AlignContentLeft?offset+8:icon>0?(int)(34*s):0),Height),TextFormatFlags.VerticalCenter|(AlignContentLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter)|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            if(Focused&&ShowFocusCues)PaintKit.Border(g,new RectangleF(3,3,Width-6,Height-6),Theme.Accent,7*s);
        }
    }
    internal sealed class IconButton:Button {
        public string Glyph;public bool Active,Danger;bool hover;readonly ToolTip tip=new ToolTip();
        public IconButton(string glyph,string hint){Glyph=glyph;AccessibleName=hint;Size=new Size(38,36);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;TabStop=true;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);tip.SetToolTip(this,hint);Theme.Watch(this,()=>{BackColor=Theme.Surface;Invalidate();});}
        public void Hint(string hint){AccessibleName=hint;tip.SetToolTip(this,hint);}
        protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent==null?Theme.Surface:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float s=g.DpiX/96f;
            if(hover||Active)PaintKit.Fill(g,new RectangleF(3,3,Width-6,Height-6),Danger?Theme.C(0xD74759):Active?Theme.AccentSoft:Theme.Soft,7*s);
            var color=!Enabled?Theme.Muted:Danger&&hover?Color.White:Active?Theme.Accent:Theme.Ink;PaintKit.Icon(g,Glyph,new RectangleF((Width-19*s)/2,(Height-19*s)/2,19*s,19*s),color);
            if(Focused&&ShowFocusCues)PaintKit.Border(g,new RectangleF(3,3,Width-6,Height-6),Theme.Accent,7*s);
        }
    }
    internal sealed class SurfacePanel:Panel {
        public bool Outline=true;public SurfacePanel(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);Padding=new Padding(18);Theme.Watch(this,()=>{BackColor=Theme.Surface;Invalidate();});}
        protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Parent==null?Theme.Paper:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;PaintKit.Fill(e.Graphics,new RectangleF(0,0,Width,Height),BackColor,14*e.Graphics.DpiX/96);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;if(Outline)PaintKit.Border(e.Graphics,new RectangleF(.5f,.5f,Width-1,Height-1),Theme.Line,14*e.Graphics.DpiX/96);}
    }
    internal sealed class Field:Panel {
        public readonly TextBox Input=new TextBox();readonly bool search;readonly IconButton clear;
        public Field(string hint,bool isSearch=false){search=isSearch;DoubleBuffered=true;Height=42;Padding=new Padding(search?39:12,11,search?39:12,9);Input.BorderStyle=BorderStyle.None;Input.Dock=DockStyle.Fill;Input.Font=Theme.Font(10);Input.AccessibleName=hint;NativeHint.Set(Input,hint);Controls.Add(Input);Input.GotFocus+=(s,e)=>Invalidate();Input.LostFocus+=(s,e)=>Invalidate();
            if(search){clear=new IconButton("close","清空搜索"){Dock=DockStyle.Right,Width=30,Visible=false};clear.Click+=(s,e)=>{Input.Clear();Input.Focus();};Controls.Add(clear);Input.TextChanged+=(s,e)=>{clear.Visible=Input.TextLength>0;};}
            Theme.Watch(this,()=>{BackColor=Theme.Surface;Input.BackColor=Theme.Surface;Input.ForeColor=Theme.Ink;Invalidate();});}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;float s=e.Graphics.DpiX/96;PaintKit.Border(e.Graphics,new RectangleF(.5f,.5f,Width-1,Height-1),Input.Focused?Theme.Accent:Theme.Line,9*s);if(search)PaintKit.Icon(e.Graphics,"search",new RectangleF(12*s,(Height-18*s)/2,18*s,18*s),Theme.Muted);}
    }
    internal sealed class ScrollArea:Panel {
        public FlowLayoutPanel Content;int offset;bool arranging,drag;float scale=1;
        public ScrollArea(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);Theme.Watch(this,()=>{BackColor=Theme.Surface;Invalidate();});}
        int MaxScroll{get{return Content==null?0:Math.Max(0,Content.Height-ClientSize.Height);}}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);if(arranging||Content==null)return;arranging=true;using(var g=CreateGraphics())scale=g.DpiX/96;int width=Math.Max(1,ClientSize.Width-(int)(15*scale));Content.Width=width;foreach(Control c in Content.Controls)if(!c.AutoSize)c.Width=width;offset=Math.Min(offset,MaxScroll);Content.Location=new Point(0,-offset);arranging=false;Invalidate();}
        void ScrollTo(int value){offset=Math.Max(0,Math.Min(MaxScroll,value));if(Content!=null)Content.Top=-offset;Invalidate();}
        protected override void OnMouseWheel(MouseEventArgs e){ScrollTo(offset-e.Delta/120*(int)(45*scale));base.OnMouseWheel(e);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(MaxScroll>0&&e.X>Width-14*scale){drag=true;Capture=true;ScrollTo((int)((float)e.Y/Math.Max(1,Height)*MaxScroll));}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(drag)ScrollTo((int)((float)e.Y/Math.Max(1,Height)*MaxScroll));}
        protected override void OnMouseUp(MouseEventArgs e){drag=false;Capture=false;base.OnMouseUp(e);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(MaxScroll<=0)return;float h=Math.Max(28*scale,Height*(float)Height/Content.Height),top=offset*(Height-h)/MaxScroll;PaintKit.Fill(e.Graphics,new RectangleF(Width-6*scale,top,4*scale,h),Theme.Line,2*scale);}
    }
    internal sealed class ToggleRow:CheckBox {
        public string Detail;public ToggleRow(string text,string detail,bool value){Text=text;Detail=detail;Checked=value;AutoSize=false;Height=50;Cursor=Cursors.Hand;Font=Theme.Font(9.5f);AccessibleName=text;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Theme.Watch(this,()=>{BackColor=Theme.Surface;ForeColor=Theme.Ink;Invalidate();});}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float s=g.DpiX/96;int w=(int)(Width-54*s);PaintKit.Text(g,Text,Font,Theme.Ink,new Rectangle(0,(int)(5*s),w,(int)(22*s)));using(var f=Theme.Font(8))PaintKit.Text(g,Detail,f,Theme.Muted,new Rectangle(0,(int)(28*s),w,(int)(20*s)));
            var r=new RectangleF(Width-39*s,17*s,36*s,21*s);PaintKit.Fill(g,r,Checked?Theme.AccentFill:Theme.Line,11*s);using(var b=new SolidBrush(Checked?Color.White:Theme.Surface))g.FillEllipse(b,r.X+(Checked?18:3)*s,r.Y+3*s,15*s,15*s);if(Focused&&ShowFocusCues)PaintKit.Border(g,new RectangleF(r.X-3,r.Y-3,r.Width+6,r.Height+6),Theme.Accent,13*s);}
        protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}
    }
    internal sealed class AppBadge:Control {
        public AndroidApp App;public AppBadge(){Size=new Size(50,50);DoubleBuffered=true;Theme.Watch(this,()=>Invalidate());}
        public static void Draw(Graphics g,RectangleF r,AndroidApp app){string key=app==null?"AppDeck":app.Package;int hash=0;foreach(char c in key)hash=unchecked(hash*31+c);int[] colors={0x7155E8,0x278B72,0xC07836,0x3E80C8,0xBE607A};Color color=Theme.C(colors[(hash&0x7fffffff)%colors.Length]);PaintKit.Fill(g,r,Color.FromArgb(Theme.Dark?55:25,color),r.Width*.27f);using(var font=Theme.Font(r.Width*.29f,true))PaintKit.Text(g,app==null?"A":app.Name.Substring(0,1),font,Theme.Dark?Blend(color,Color.White,.35f):color,Rectangle.Round(r),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);}
        static Color Blend(Color a,Color b,float t){return Color.FromArgb((int)(a.R*(1-t)+b.R*t),(int)(a.G*(1-t)+b.G*t),(int)(a.B*(1-t)+b.B*t));}
        protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Draw(e.Graphics,new RectangleF(0,0,Width,Height),App);}
    }
    internal sealed class CatalogView:ScrollableControl {
        List<AndroidApp> items=new List<AndroidApp>();HashSet<string> favorites=new HashSet<string>();int selected=-1,hover=-1;float scale=1;int cols=2,cardWidth=210,cardHeight=100,gap=12;public string EmptyTitle="等待手机连接",EmptyDetail="连接 USB 或无线 ADB 后，应用会出现在这里。";
        public event EventHandler SelectionChanged,OpenRequested,FavoriteRequested;
        public AndroidApp SelectedApp{get{return selected>=0&&selected<items.Count?items[selected]:null;}}
        int scrollOffset,maxScroll;bool scrollDrag;
        public CatalogView(){DoubleBuffered=true;AutoScroll=false;TabStop=true;AccessibleName="应用卡片列表";SetStyle(ControlStyles.Selectable|ControlStyles.ResizeRedraw,true);Theme.Watch(this,()=>{BackColor=Theme.Paper;Invalidate();});}
        public void SetItems(List<AndroidApp> data,HashSet<string> stars,string package){items=data;favorites=stars;selected=items.FindIndex(a=>a.Package==package);if(selected<0&&items.Count>0)selected=0;scrollOffset=0;Measure();Notify();Invalidate();}
        void Notify(){var a=SelectedApp;AccessibleDescription=a==null?EmptyTitle:"已选择 "+a.Name+"，按 Enter 打开窗口";var h=SelectionChanged;if(h!=null)h(this,EventArgs.Empty);}
        void Measure(){using(var g=CreateGraphics())scale=g.DpiX/96f;gap=(int)(12*scale);cardHeight=(int)(98*scale);int available=Math.Max(1,ClientSize.Width-(int)(14*scale));cols=Math.Max(1,available/(int)(190*scale));cardWidth=Math.Max(1,(available-gap*(cols-1))/cols);maxScroll=Math.Max(0,((items.Count+cols-1)/cols)*(cardHeight+gap)-gap-ClientSize.Height);scrollOffset=Math.Min(scrollOffset,maxScroll);}
        protected override void OnResize(EventArgs e){base.OnResize(e);Measure();Invalidate();}
        Rectangle Card(int i){return new Rectangle((i%cols)*(cardWidth+gap),(i/cols)*(cardHeight+gap)-scrollOffset,cardWidth,cardHeight);}
        int Hit(Point p){for(int i=0;i<items.Count;i++)if(Card(i).Contains(p))return i;return -1;}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(scrollDrag){ScrollTo((int)((float)e.Y/Math.Max(1,Height)*maxScroll));return;}int h=Hit(e.Location);if(h!=hover){hover=h;Cursor=h>=0?Cursors.Hand:Cursors.Default;Invalidate();}}
        protected override void OnMouseLeave(EventArgs e){hover=-1;Invalidate();base.OnMouseLeave(e);}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();if(maxScroll>0&&e.X>Width-14*scale){scrollDrag=true;Capture=true;ScrollTo((int)((float)e.Y/Math.Max(1,Height)*maxScroll));return;}int h=Hit(e.Location);if(h<0)return;selected=h;Notify();var r=Card(h);if(e.X>r.Right-34*scale&&e.Y<r.Top+32*scale){var handler=FavoriteRequested;if(handler!=null)handler(this,EventArgs.Empty);}Invalidate();}
        protected override void OnMouseUp(MouseEventArgs e){scrollDrag=false;Capture=false;base.OnMouseUp(e);}
        void ScrollTo(int y){scrollOffset=Math.Max(0,Math.Min(maxScroll,y));Invalidate();}
        protected override void OnMouseWheel(MouseEventArgs e){ScrollTo(scrollOffset-e.Delta/120*(int)(55*scale));base.OnMouseWheel(e);}
        protected override void OnMouseDoubleClick(MouseEventArgs e){base.OnMouseDoubleClick(e);int h=Hit(e.Location);if(h<0||e.X>Card(h).Right-34*scale&&e.Y<Card(h).Top+32*scale)return;var action=OpenRequested;if(action!=null)action(this,EventArgs.Empty);}
        protected override bool IsInputKey(Keys key){var k=key&Keys.KeyCode;return k==Keys.Up||k==Keys.Down||k==Keys.Left||k==Keys.Right||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Enter){e.Handled=true;var action=OpenRequested;if(action!=null)action(this,EventArgs.Empty);return;}int next=selected;if(e.KeyCode==Keys.Right)next++;else if(e.KeyCode==Keys.Left)next--;else if(e.KeyCode==Keys.Down)next+=cols;else if(e.KeyCode==Keys.Up)next-=cols;else return;e.Handled=true;if(items.Count==0)return;selected=Math.Max(0,Math.Min(items.Count-1,next));var r=Card(selected);if(r.Bottom>ClientSize.Height)ScrollTo(scrollOffset+r.Bottom-ClientSize.Height+gap);else if(r.Top<0)ScrollTo(scrollOffset+r.Top);Notify();Invalidate();}
        protected override void OnScroll(ScrollEventArgs se){base.OnScroll(se);Invalidate();}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;if(items.Count==0){float cx=ClientSize.Width/2f,cy=ClientSize.Height*.36f;PaintKit.Fill(g,new RectangleF(cx-38*scale,cy-38*scale,76*scale,76*scale),Theme.Soft,24*scale);PaintKit.Icon(g,"phone",new RectangleF(cx-18*scale,cy-18*scale,36*scale,36*scale),Theme.Muted);using(var f=Theme.Font(13,true))PaintKit.Text(g,EmptyTitle,f,Theme.Ink,new Rectangle(0,(int)(cy+53*scale),ClientSize.Width,(int)(30*scale)),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);using(var f=Theme.Font(9))PaintKit.Text(g,EmptyDetail,f,Theme.Muted,new Rectangle(10,(int)(cy+88*scale),ClientSize.Width-20,(int)(70*scale)),TextFormatFlags.HorizontalCenter|TextFormatFlags.Top|TextFormatFlags.WordBreak);return;}
            using(var nameFont=Theme.Font(10,true))using(var packageFont=Theme.Font(8))for(int i=0;i<items.Count;i++){var r=Card(i);if(r.Bottom<0||r.Top>Height)continue;bool active=i==selected;var a=items[i];PaintKit.Fill(g,new RectangleF(r.X+.5f,r.Y+.5f,r.Width-1,r.Height-1),active?Theme.AccentSoft:Theme.Surface,12*scale);PaintKit.Border(g,new RectangleF(r.X+.5f,r.Y+.5f,r.Width-1,r.Height-1),active?Theme.Accent:(i==hover?Theme.Muted:Theme.Line),12*scale,active?1.5f:1);
                AppBadge.Draw(g,new RectangleF(r.X+14*scale,r.Y+16*scale,40*scale,40*scale),a);PaintKit.Text(g,a.Name,nameFont,Theme.Ink,new Rectangle(r.X+(int)(64*scale),r.Y+(int)(19*scale),r.Width-(int)(89*scale),(int)(30*scale)));PaintKit.Text(g,a.Package,packageFont,Theme.Muted,new Rectangle(r.X+(int)(14*scale),r.Y+(int)(65*scale),r.Width-(int)(28*scale),(int)(20*scale)));
                if(favorites.Contains(a.Package)||i==hover)PaintKit.Icon(g,"star",new RectangleF(r.Right-27*scale,r.Top+11*scale,15*scale,15*scale),favorites.Contains(a.Package)?Theme.Accent:Theme.Muted);
            }
            if(maxScroll>0){float thumb=Math.Max(32*scale,Height*(float)Height/(Height+maxScroll));PaintKit.Fill(g,new RectangleF(Width-6*scale,scrollOffset*(Height-thumb)/maxScroll,4*scale,thumb),Theme.Line,2*scale);}
        }
    }
    internal class ModernForm:Form {
        public readonly Panel Body=new Panel();public readonly CaptionBar Caption;public bool FullBleed;
        public ModernForm(){AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;Font=Theme.Font();try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}FormBorderStyle=FormBorderStyle.None;DoubleBuffered=true;Padding=new Padding(4);Caption=new CaptionBar(this){Dock=DockStyle.Top,Height=44};Body.Dock=DockStyle.Fill;Controls.Add(Body);Controls.Add(Caption);Theme.Watch(this,()=>{BackColor=Theme.Surface;ForeColor=Theme.Ink;Body.BackColor=Theme.Paper;ApplyDwm();Invalidate(true);});TextChanged+=(s,e)=>Caption.Invalidate();}
        protected override CreateParams CreateParams{get{var cp=base.CreateParams;cp.Style=(cp.Style&~0x00C00000)|0x00040000|0x00080000|0x00020000|0x00010000;cp.ExStyle&=~0x00020301;return cp;}}
        protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);ApplyDwm();Native.SetWindowPos(Handle,IntPtr.Zero,0,0,0,0,0x0037);}
        void ApplyDwm(){if(!IsHandleCreated)return;int dark=Theme.Dark?1:0,corners=2,noBorder=-2;try{DwmSetWindowAttribute(Handle,20,ref dark,4);DwmSetWindowAttribute(Handle,33,ref corners,4);DwmSetWindowAttribute(Handle,34,ref noBorder,4);var m=new Margins();DwmExtendFrameIntoClientArea(Handle,ref m);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){}}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(Theme.Line))e.Graphics.DrawRectangle(pen,0,0,ClientSize.Width-1,ClientSize.Height-1);}
        protected override void WndProc(ref Message m){
            // Reserving no non-client area is not enough: DefWindowProc can repaint a
            // classic caption on activation or text changes. The client owns all chrome.
            if(m.Msg==0x83||m.Msg==0x85||m.Msg==0xAE||m.Msg==0xAF){m.Result=IntPtr.Zero;return;}
            if(m.Msg==0x86){m.Result=new IntPtr(1);return;}
            if(m.Msg==0x84&&!FullBleed&&WindowState==FormWindowState.Normal){long pos=m.LParam.ToInt64();var p=PointToClient(new Point((short)(pos&65535),(short)((pos>>16)&65535)));int edge=Math.Max(5,Padding.Left);bool l=p.X<edge,r=p.X>=ClientSize.Width-edge,t=p.Y<edge,b=p.Y>=ClientSize.Height-edge;int hit=t?(l?13:r?14:12):b?(l?16:r?17:15):l?10:r?11:0;if(hit!=0){m.Result=new IntPtr(hit);return;}}
            if(m.Msg==0x24){base.WndProc(ref m);var info=(MinMax)Marshal.PtrToStructure(m.LParam,typeof(MinMax));var monitor=Screen.FromHandle(Handle);var area=FullBleed?monitor.Bounds:monitor.WorkingArea;info.MaxPosition=new NativePoint(area.Left-monitor.Bounds.Left,area.Top-monitor.Bounds.Top);info.MaxSize=new NativePoint(area.Width,area.Height);info.MinTrack=new NativePoint(MinimumSize.Width,MinimumSize.Height);Marshal.StructureToPtr(info,m.LParam,false);return;}
            base.WndProc(ref m);
        }
        public void BeginMove(){ReleaseCapture();SendMessage(Handle,0xA1,new IntPtr(2),IntPtr.Zero);}
        public void ToggleMaximize(){WindowState=WindowState==FormWindowState.Maximized?FormWindowState.Normal:FormWindowState.Maximized;Caption.Invalidate();}
        [StructLayout(LayoutKind.Sequential)]struct NativePoint{public int X,Y;public NativePoint(int x,int y){X=x;Y=y;}}
        [StructLayout(LayoutKind.Sequential)]struct MinMax{public NativePoint Reserved,MaxSize,MaxPosition,MinTrack,MaxTrack;}
        [StructLayout(LayoutKind.Sequential)]struct Margins{public int Left,Right,Top,Bottom;}
        [DllImport("user32.dll")]static extern bool ReleaseCapture();[DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr h,int msg,IntPtr w,IntPtr l);
        [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
        [DllImport("dwmapi.dll")]static extern int DwmExtendFrameIntoClientArea(IntPtr h,ref Margins margins);
    }
    internal sealed class CaptionBar:Control {
        readonly ModernForm owner;readonly List<IconButton> actions=new List<IconButton>();readonly IconButton minimize,maximize,close;public bool Compact;public string Subtitle="";public IconButton Leading;
        public CaptionBar(ModernForm form){owner=form;DoubleBuffered=true;minimize=new IconButton("min","最小化");maximize=new IconButton("max","最大化 / 还原");close=new IconButton("close","关闭窗口"){Danger=true};minimize.Click+=(s,e)=>owner.WindowState=FormWindowState.Minimized;maximize.Click+=(s,e)=>owner.ToggleMaximize();close.Click+=(s,e)=>owner.Close();Controls.Add(minimize);Controls.Add(maximize);Controls.Add(close);Theme.Watch(this,()=>{BackColor=Theme.Surface;Invalidate();});MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Left&&e.Clicks==1)owner.BeginMove();};DoubleClick+=(s,e)=>owner.ToggleMaximize();owner.Resize+=(s,e)=>{maximize.Glyph=owner.WindowState==FormWindowState.Maximized?"restore":"max";maximize.Invalidate();};}
        public IconButton AddAction(string glyph,string hint,EventHandler action){var b=new IconButton(glyph,hint);b.Click+=action;actions.Add(b);Controls.Add(b);PerformLayout();return b;}
        public IconButton AddLeading(string glyph,string hint,EventHandler action){Leading=new IconButton(glyph,hint);Leading.Click+=action;Controls.Add(Leading);PerformLayout();return Leading;}
        protected override void OnLayout(LayoutEventArgs le){base.OnLayout(le);float s=Height/44f;int w=(int)(Compact?34*s:40*s),right=Width;close.Visible=owner.ControlBox;maximize.Visible=owner.ControlBox&&owner.MaximizeBox;minimize.Visible=owner.ControlBox&&owner.MinimizeBox;foreach(var b in new[]{close,maximize,minimize}){if(!b.Visible)continue;right-=w;b.SetBounds(right,0,w,Height);}right-=(int)(8*s);for(int i=actions.Count-1;i>=0;i--){right-=w;actions[i].SetBounds(right,0,w,Height);}if(Leading!=null)Leading.SetBounds(0,0,w,Height);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var g=e.Graphics;float s=Height/44f;g.SmoothingMode=SmoothingMode.AntiAlias;int left=(int)(14*s);if(Leading==null){PaintKit.Fill(g,new RectangleF(10*s,8*s,28*s,28*s),Theme.AccentFill,8*s);PaintKit.Icon(g,"logo",new RectangleF(15*s,12*s,19*s,19*s),Color.White);left=(int)(48*s);}else left=Leading.Right+5;
            int end=actions.Count>0?actions[0].Left-8:(minimize.Visible?minimize.Left:maximize.Visible?maximize.Left:close.Left)-10;string text=owner.Text.Replace(" · AppDeck","");using(var f=Theme.Font(Compact?9:10,true))PaintKit.Text(g,text,f,Theme.Ink,new Rectangle(left,0,Math.Max(0,end-left),Height));using(var p=new Pen(Theme.Line))g.DrawLine(p,0,Height-1,Width,Height-1);}
    }
}
