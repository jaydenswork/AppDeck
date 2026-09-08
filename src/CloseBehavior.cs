using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppDeck {
    internal enum MainCloseAction { Ask=0, Exit=1, Tray=2 }

    internal sealed class CloseChoiceRadio:RadioButton {
        bool hover;
        public CloseChoiceRadio(string text,bool value=false){Text=text;Checked=value;AutoSize=false;Height=46;Cursor=Cursors.Hand;Font=Theme.Font(9);AccessibleName=text;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Theme.Watch(this,()=>{BackColor=Theme.Paper;ForeColor=Theme.Ink;Invalidate();});}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(Parent==null?Theme.Paper:Parent.BackColor);float s=g.DpiX/96f;var r=new RectangleF(.5f,.5f,Width-1,Height-1);PaintKit.Fill(g,r,Checked?Theme.AccentSoft:hover?Theme.Soft:Theme.Surface,9*s);PaintKit.Border(g,r,Checked?Theme.Accent:Theme.Line,9*s,Checked?1.4f:1f);var circle=new RectangleF(14*s,(Height-16*s)/2,16*s,16*s);using(var pen=new Pen(Checked?Theme.Accent:Theme.Muted,1.5f*s))g.DrawEllipse(pen,circle);if(Checked)using(var brush=new SolidBrush(Theme.AccentFill))g.FillEllipse(brush,circle.X+4*s,circle.Y+4*s,8*s,8*s);PaintKit.Text(g,Text,Font,Checked?Theme.Accent:Theme.Ink,new Rectangle((int)(40*s),0,Width-(int)(50*s),Height));if(Focused&&ShowFocusCues)PaintKit.Border(g,new RectangleF(3,3,Width-6,Height-6),Theme.Accent,7*s);}
    }

    internal sealed class CloseRememberCheck:CheckBox {
        bool hover;
        public CloseRememberCheck(){Text="下次不再提示";AutoSize=false;Size=new Size(150,32);Cursor=Cursors.Hand;Font=Theme.Font(9);AccessibleName=Text;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);Theme.Watch(this,()=>{BackColor=Theme.Paper;ForeColor=Theme.Ink;Invalidate();});}
        protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}protected override void OnCheckedChanged(EventArgs e){Invalidate();base.OnCheckedChanged(e);}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;g.Clear(Parent==null?Theme.Paper:Parent.BackColor);float s=g.DpiX/96f;var box=new RectangleF(1*s,(Height-17*s)/2,17*s,17*s);PaintKit.Fill(g,box,Checked?Theme.AccentFill:hover?Theme.AccentSoft:Theme.Surface,5*s);PaintKit.Border(g,box,Checked?Theme.AccentFill:hover?Theme.Accent:Theme.Line,5*s,1.2f);if(Checked)PaintKit.Icon(g,"check",new RectangleF(2.5f*s,(Height-14*s)/2,14*s,14*s),Color.White);PaintKit.Text(g,Text,Font,Theme.Ink,new Rectangle((int)(27*s),0,Width-(int)(29*s),Height));if(Focused&&ShowFocusCues)PaintKit.Border(g,new RectangleF(0,2,Width-1,Height-4),Theme.Accent,6*s);}
    }

    internal sealed class CloseChoiceDialog:ModernForm {
        readonly CloseChoiceRadio minimizeToTray=new CloseChoiceRadio("最小化到托盘",true);
        readonly CloseChoiceRadio exitProgram=new CloseChoiceRadio("直接退出程序");
        readonly CloseRememberCheck remember=new CloseRememberCheck();
        public MainCloseAction Choice { get; private set; }
        public bool Remember { get { return remember.Checked; } }

        public CloseChoiceDialog(){
            Text="关闭主窗口时";ClientSize=new Size(420,178);MinimumSize=new Size(420,178);MaximumSize=new Size(420,178);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;MaximizeBox=false;Caption.SetWindowButtons(false,false,true);
            var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(18,10,18,12),Margin=Padding.Empty};content.RowStyles.Add(new RowStyle(SizeType.Absolute,52));content.RowStyles.Add(new RowStyle(SizeType.Percent,100));Body.Controls.Add(content);
            var choices=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));choices.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));minimizeToTray.Dock=DockStyle.Fill;minimizeToTray.Margin=new Padding(0,2,5,2);exitProgram.Dock=DockStyle.Fill;exitProgram.Margin=new Padding(5,2,0,2);choices.Controls.Add(minimizeToTray,0,0);choices.Controls.Add(exitProgram,1,0);content.Controls.Add(choices,0,0);
            var bottom=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=Padding.Empty};bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,84));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,84));remember.Anchor=AnchorStyles.Left;remember.Margin=Padding.Empty;bottom.Controls.Add(remember,0,0);var ok=new ModernButton{Text="确定",Anchor=AnchorStyles.None,Size=new Size(76,32),Margin=Padding.Empty,Font=Theme.Font(9,true),Primary=true};ok.Click+=(s,e)=>Finish();bottom.Controls.Add(ok,1,0);var cancel=new ModernButton{Text="取消",Anchor=AnchorStyles.None,Size=new Size(76,32),Margin=Padding.Empty,Font=Theme.Font(9)};cancel.Click+=(s,e)=>{DialogResult=DialogResult.Cancel;Close();};bottom.Controls.Add(cancel,2,0);content.Controls.Add(bottom,0,1);AcceptButton=ok;CancelButton=cancel;
            Theme.Watch(this,()=>{content.BackColor=choices.BackColor=bottom.BackColor=Theme.Paper;});
        }
        void Finish(){Choice=exitProgram.Checked?MainCloseAction.Exit:MainCloseAction.Tray;DialogResult=DialogResult.OK;Close();}
    }
}
