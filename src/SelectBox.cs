using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace AppDeck {
    internal sealed class SelectBox:Control {
        public readonly List<object> Items=new List<object>();int index=-1;ChoicePanel popup;
        public event EventHandler SelectedIndexChanged;
        public int SelectedIndex{get{return index;}set{if(index==value)return;index=value;Invalidate();if(popup!=null)popup.ClosePanel(false);var h=SelectedIndexChanged;if(h!=null)h(this,EventArgs.Empty);}}
        public object SelectedItem{get{return index>=0&&index<Items.Count?Items[index]:null;}set{SelectedIndex=Items.IndexOf(value);Invalidate();}}
        public SelectBox(){DoubleBuffered=true;TabStop=true;Cursor=Cursors.Hand;Height=36;Font=Theme.Font(9);AccessibleRole=AccessibleRole.ComboBox;SetStyle(ControlStyles.Selectable|ControlStyles.ResizeRedraw,true);Theme.Watch(this,()=>{BackColor=Theme.Surface;ForeColor=Theme.Ink;Invalidate();if(popup!=null)popup.Invalidate();});}
        protected override void OnClick(EventArgs e){base.OnClick(e);Focus();Open();}
        internal void Open(){
            if(Items.Count==0||!Enabled)return;
            if(popup!=null){popup.ClosePanel(true);return;}
            var host=FindForm();if(host==null)return;
            popup=new ChoicePanel(this,host);popup.ShowPanel();Invalidate();
        }
        protected override bool IsInputKey(Keys key){return key==Keys.Up||key==Keys.Down||base.IsInputKey(key);}
        protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Space||e.KeyCode==Keys.Enter||e.KeyCode==Keys.F4||e.Alt&&e.KeyCode==Keys.Down){Open();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Down||e.KeyCode==Keys.Up){if(Items.Count>0)SelectedIndex=Math.Max(0,Math.Min(Items.Count-1,index+(e.KeyCode==Keys.Down?1:-1)));e.Handled=true;}}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;float s=g.DpiX/96;PaintKit.Border(g,new RectangleF(.5f,.5f,Width-1,Height-1),Focused?Theme.Accent:Theme.Line,8*s);DrawValue(g,new Rectangle(0,0,Width,Height),false);}
        void DrawValue(Graphics g,Rectangle area,bool expanded){float s=g.DpiX/96;PaintKit.Text(g,SelectedItem==null?"尚未连接手机":SelectedItem.ToString(),Font,Enabled&&SelectedItem!=null?Theme.Ink:Theme.Muted,new Rectangle((int)(10*s),area.Top,Width-(int)(37*s),area.Height));var mark=new RectangleF(Width-26*s,area.Top+(area.Height-17*s)/2,17*s,17*s);if(expanded){var saved=g.Save();g.TranslateTransform(mark.X+mark.Width/2,mark.Y+mark.Height/2);g.RotateTransform(180);g.TranslateTransform(-mark.X-mark.Width/2,-mark.Y-mark.Height/2);PaintKit.Icon(g,"chevron",mark,Theme.Muted);g.Restore(saved);}else PaintKit.Icon(g,"chevron",mark,Theme.Muted);}
        protected override void Dispose(bool disposing){if(disposing&&popup!=null)popup.ClosePanel(false);base.Dispose(disposing);}

        // An overlay child of the same Form, not a floating menu/window. The header and
        // list are drawn together behind one uninterrupted outline, exactly over the field.
        sealed class ChoicePanel:Control,IMessageFilter {
            readonly SelectBox owner;readonly Form host;readonly int rowHeight,pad,headerHeight;
            int active,first,rows;bool above,closed;Rectangle header,list;
            public ChoicePanel(SelectBox owner,Form host){this.owner=owner;this.host=host;DoubleBuffered=true;TabStop=true;Font=owner.Font;Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.List;SetStyle(ControlStyles.Selectable|ControlStyles.ResizeRedraw,true);float scale;using(var g=owner.CreateGraphics())scale=g.DpiX/96;rowHeight=(int)(36*scale);pad=(int)(5*scale);headerHeight=owner.Height;active=Math.Max(0,owner.index);}
            public void ShowPanel(){
                Point location=host.PointToClient(owner.PointToScreen(Point.Empty));
                int below=host.ClientSize.Height-location.Y-headerHeight-5,up=location.Y-48;
                above=below<Math.Min(owner.Items.Count,4)*rowHeight&&up>below;
                int room=Math.Max(rowHeight,above?up:below);
                rows=Math.Max(1,Math.Min(Math.Min(owner.Items.Count,9),(room-2*pad)/rowHeight));
                int listHeight=rows*rowHeight+2*pad;
                SetBounds(location.X,above?location.Y-listHeight:location.Y,owner.Width,headerHeight+listHeight);
                header=new Rectangle(0,above?listHeight:0,Width,headerHeight);
                list=new Rectangle(pad,above?pad:headerHeight+pad,Width-2*pad,rows*rowHeight);
                first=Math.Max(0,Math.Min(active-rows+1,owner.Items.Count-rows));
                host.Controls.Add(this);BringToFront();host.Deactivate+=HostChanged;host.Resize+=HostChanged;host.LocationChanged+=HostChanged;owner.VisibleChanged+=OwnerChanged;owner.EnabledChanged+=OwnerChanged;
                Application.AddMessageFilter(this);Show();Focus();
            }
            void HostChanged(object sender,EventArgs e){ClosePanel(false);}
            void OwnerChanged(object sender,EventArgs e){if(!owner.Visible||!owner.Enabled)ClosePanel(false);}
            public bool PreFilterMessage(ref Message m){
                if(m.Msg==0x201||m.Msg==0x204||m.Msg==0x207||m.Msg==0x20B||m.Msg==0xA1||m.Msg==0xA4){if(m.HWnd!=Handle)ClosePanel(false);}
                else if(m.Msg==0x20A&&m.HWnd!=Handle)ClosePanel(false);
                return false;
            }
            public void ClosePanel(bool focus){if(closed)return;closed=true;Application.RemoveMessageFilter(this);host.Deactivate-=HostChanged;host.Resize-=HostChanged;host.LocationChanged-=HostChanged;owner.VisibleChanged-=OwnerChanged;owner.EnabledChanged-=OwnerChanged;owner.popup=null;host.Controls.Remove(this);Dispose();if(!owner.IsDisposed){owner.Invalidate();if(focus)owner.Focus();}}
            int RowAt(Point point){if(!list.Contains(point))return -1;int row=first+(point.Y-list.Top)/rowHeight;return row<owner.Items.Count?row:-1;}
            void Choose(){int choice=active;ClosePanel(true);owner.SelectedIndex=choice;}
            protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);int item=RowAt(e.Location);if(item>=0&&item!=active){active=item;Invalidate();}}
            protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button!=MouseButtons.Left)return;if(header.Contains(e.Location)){ClosePanel(true);return;}int item=RowAt(e.Location);if(item>=0){active=item;Choose();}}
            protected override void OnMouseWheel(MouseEventArgs e){first=Math.Max(0,Math.Min(owner.Items.Count-rows,first-Math.Sign(e.Delta)*3));Invalidate();}
            protected override bool IsInputKey(Keys key){return key==Keys.Up||key==Keys.Down||key==Keys.Home||key==Keys.End||base.IsInputKey(key);}
            protected override bool ProcessDialogKey(Keys key){if((key&Keys.KeyCode)==Keys.Escape){ClosePanel(true);return true;}if((key&Keys.KeyCode)==Keys.Tab){bool next=(key&Keys.Shift)==0;ClosePanel(true);host.SelectNextControl(owner,next,true,true,true);return true;}return base.ProcessDialogKey(key);}
            protected override void OnKeyDown(KeyEventArgs e){
                base.OnKeyDown(e);
                if(e.KeyCode==Keys.Enter||e.KeyCode==Keys.Space){Choose();e.SuppressKeyPress=true;return;}
                if(e.KeyCode==Keys.Up)active=Math.Max(0,active-1);else if(e.KeyCode==Keys.Down)active=Math.Min(owner.Items.Count-1,active+1);else if(e.KeyCode==Keys.Home)active=0;else if(e.KeyCode==Keys.End)active=owner.Items.Count-1;else return;
                if(active<first)first=active;if(active>=first+rows)first=active-rows+1;e.Handled=true;Invalidate();
            }
            protected override void OnPaint(PaintEventArgs e){
                var g=e.Graphics;g.Clear(Theme.Surface);g.SmoothingMode=SmoothingMode.AntiAlias;float scale=g.DpiX/96;
                owner.DrawValue(g,header,true);
                using(var pen=new Pen(Theme.Line)){int y=above?header.Top:header.Bottom;g.DrawLine(pen,1,y,Width-2,y);}
                for(int i=first;i<Math.Min(owner.Items.Count,first+rows);i++){
                    var row=new Rectangle(list.Left,list.Top+(i-first)*rowHeight,list.Width,rowHeight);
                    if(i==active||i==owner.index)PaintKit.Fill(g,new RectangleF(row.X,row.Y+2,row.Width,row.Height-4),i==active?Theme.AccentSoft:Theme.Soft,5*scale);
                    PaintKit.Text(g,owner.Items[i].ToString(),Font,Theme.Ink,new Rectangle(row.X+(int)(7*scale),row.Y,row.Width-(int)(34*scale),row.Height));
                    if(i==owner.index)PaintKit.Icon(g,"check",new RectangleF(row.Right-24*scale,row.Y+(row.Height-16*scale)/2,16*scale,16*scale),Theme.Accent);
                }
                if(owner.Items.Count>rows){float h=Math.Max(20,list.Height*(float)rows/owner.Items.Count),top=list.Top+first*(list.Height-h)/(owner.Items.Count-rows);PaintKit.Fill(g,new RectangleF(Width-4,top,2,h),Theme.Line,1);}
                PaintKit.Border(g,new RectangleF(.5f,.5f,Width-1,Height-1),Theme.Accent,8*scale);
            }
        }
    }
}
