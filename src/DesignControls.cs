using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppDeck {
    internal sealed class SegmentedChoice:Control {
        public readonly List<object> Items=new List<object>();int index=-1;
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex {get{return index;}set{if(index==value)return;index=value;Invalidate();var changed=SelectedIndexChanged;if(changed!=null)changed(this,EventArgs.Empty);}}
        public SegmentedChoice(){DoubleBuffered=true;Height=36;TabStop=true;Cursor=Cursors.Hand;Font=Theme.Font(10.5f);AccessibleRole=AccessibleRole.PageTabList;SetStyle(ControlStyles.Selectable|ControlStyles.ResizeRedraw,true);Theme.Watch(this,()=>{BackColor=Theme.Soft;Invalidate();});}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button==MouseButtons.Left&&Items.Count>0){Focus();SelectedIndex=Math.Min(Items.Count-1,e.X*Items.Count/Math.Max(1,Width));}}
        protected override bool IsInputKey(Keys key){return key==Keys.Left||key==Keys.Right||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Left||e.KeyCode==Keys.Right){SelectedIndex=Math.Max(0,Math.Min(Items.Count-1,index+(e.KeyCode==Keys.Left?-1:1)));e.Handled=true;}}
        protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent==null?Theme.Surface:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float s=g.DpiX/96;PaintKit.Fill(g,new RectangleF(0,0,Width,Height),Theme.Soft,8*s);for(int i=0;i<Items.Count;i++){var r=new RectangleF(i*Width/(float)Items.Count+3*s,3*s,Width/(float)Items.Count-6*s,Height-6*s);if(i==index){PaintKit.Fill(g,r,Theme.Surface,6*s);PaintKit.Border(g,r,Theme.Line,6*s);}PaintKit.Text(g,Items[i].ToString(),Font,i==index?Theme.Ink:Theme.Muted,Rectangle.Round(r),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);}if(Focused&&ShowFocusCues)PaintKit.Border(g,new RectangleF(1,1,Width-2,Height-2),Theme.Accent,8*s,1.5f);}
    }
    internal sealed class ConnectionView:Control {
        readonly ModernButton retry=new ModernButton{Text="重新连接",Primary=true},diagnostics=new ModernButton{Text="查看诊断",Quiet=true};
        int stage;bool canRetry;public int Stage {get{return stage;}set{stage=value;Invalidate();}}
        public bool CanRetry {get{return canRetry;}set{canRetry=value;retry.Visible=value;PerformLayout();}}
        public string ActionText {set{retry.Text=value;}}
        public event EventHandler RetryRequested,DiagnosticsRequested;
        public ConnectionView(){DoubleBuffered=true;SetStyle(ControlStyles.ResizeRedraw,true);Text="正在连接手机\n请保持手机与电脑的连接。";Controls.Add(retry);Controls.Add(diagnostics);retry.Click+=(s,e)=>{var h=RetryRequested;if(h!=null)h(this,e);};diagnostics.Click+=(s,e)=>{var h=DiagnosticsRequested;if(h!=null)h(this,e);};retry.Visible=false;Theme.Watch(this,()=>{BackColor=Theme.Paper;Invalidate();});}
        protected override void OnTextChanged(EventArgs e){Invalidate();base.OnTextChanged(e);}
        protected override void OnLayout(LayoutEventArgs e){base.OnLayout(e);float s=DeviceDpi/96f;bool compact=Height<400*s;int w=(int)(compact?112*s:120*s),h=(int)(compact?32*s:36*s),y=compact?Height-(int)(48*s):(int)(Height*.5+96*s);retry.SetBounds(compact?(Width-2*w-(int)(8*s))/2:(Width-w)/2,y,w,h);diagnostics.SetBounds(compact&&canRetry?(Width+(int)(8*s))/2:(Width-w)/2,compact?y:y+(canRetry?(int)(44*s):0),w,h);}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Theme.Paper);g.SmoothingMode=SmoothingMode.AntiAlias;float s=g.DpiX/96;bool compact=Height<400*s;int center=compact?(int)((Height>=280*s?88:40)*s):(int)(Height*.5-88*s),width=Math.Min(Width-(int)(32*s),(int)(380*s));PaintKit.Fill(g,new RectangleF(Width/2f-28*s,center-28*s,56*s,56*s),Theme.AccentSoft,16*s);PaintKit.Icon(g,"phone",new RectangleF(Width/2f-14*s,center-14*s,28*s,28*s),Theme.Accent);string[] lines=Text.Split(new[]{'\n'},2);using(var f=Theme.Font(12,true))PaintKit.Text(g,lines[0],f,Theme.Ink,new Rectangle((Width-width)/2,center+(int)(48*s),width,(int)(32*s)),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);using(var f=Theme.Font(9))PaintKit.Text(g,lines.Length>1?lines[1]:"请稍候，正在准备应用窗口。",f,Theme.Muted,new Rectangle((Width-width)/2,center+(int)(88*s),width,(int)((compact?48:64)*s)),TextFormatFlags.HorizontalCenter|TextFormatFlags.Top|TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            string[] steps={"连接手机","启动应用","等待画面"};if(!compact||Height>=280*s)using(var f=Theme.Font(9)){int total=(int)(264*s),left=(Width-total)/2;for(int i=0;i<3;i++){var r=new Rectangle(left+(int)(i*88*s),compact?(int)(12*s):center-(int)(76*s),(int)(88*s),(int)(24*s));PaintKit.Text(g,(i<stage?"✓ ":"")+steps[i],f,i==stage?Theme.Accent:Theme.Muted,r,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix);}}
        }
    }
}
