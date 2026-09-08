using System;
using System.Drawing;
using System.Windows.Forms;

namespace AppDeck {
    internal enum MainCloseAction { Ask=0, Exit=1, Tray=2 }

    internal sealed class CloseChoiceDialog:ModernForm {
        readonly ToggleRow remember=new ToggleRow("记住我的选择","下次关闭主窗口时直接执行",false);
        public MainCloseAction Choice { get; private set; }
        public bool Remember { get { return remember.Checked; } }

        public CloseChoiceDialog(){
            Text="关闭 AppDeck";ClientSize=new Size(500,330);MinimumSize=new Size(500,330);MaximumSize=new Size(500,330);StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;MinimizeBox=false;MaximizeBox=false;
            var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(28,22,28,22),Margin=Padding.Empty};
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,38));content.RowStyles.Add(new RowStyle(SizeType.Absolute,58));content.RowStyles.Add(new RowStyle(SizeType.Absolute,62));content.RowStyles.Add(new RowStyle(SizeType.Percent,100));Body.Controls.Add(content);
            var title=Theme.Label("关闭主界面时",18,true);title.Dock=DockStyle.Fill;content.Controls.Add(title,0,0);
            var detail=Theme.Hint("你可以完全退出 AppDeck，或让它继续在系统托盘中运行。",9);detail.AutoSize=false;detail.Dock=DockStyle.Fill;detail.TextAlign=ContentAlignment.MiddleLeft;content.Controls.Add(detail,0,1);
            var buttons=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0,4,0,4)};buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            var exit=new ModernButton{Text="退出程序",Dock=DockStyle.Fill,Margin=new Padding(0,0,6,0),Font=Theme.Font(10,true)};exit.Click+=(s,e)=>Finish(MainCloseAction.Exit);buttons.Controls.Add(exit,0,0);
            var tray=new ModernButton{Text="最小化到托盘",Dock=DockStyle.Fill,Margin=new Padding(6,0,0,0),Font=Theme.Font(10,true),Primary=true};tray.Click+=(s,e)=>Finish(MainCloseAction.Tray);buttons.Controls.Add(tray,1,0);content.Controls.Add(buttons,0,2);
            remember.Dock=DockStyle.Fill;remember.Margin=new Padding(0,10,0,0);content.Controls.Add(remember,0,3);AcceptButton=tray;
            Theme.Watch(this,()=>{content.BackColor=buttons.BackColor=Theme.Paper;});
        }
        void Finish(MainCloseAction choice){Choice=choice;DialogResult=DialogResult.OK;Close();}
    }
}
