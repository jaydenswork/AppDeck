using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppDeck {
    internal static class WirelessDialogChecks {
        static void Check(bool value,string message){if(!value)throw new Exception("wireless dialog: "+message);}
        static Control Find(Control root,Func<Control,bool> predicate){foreach(Control child in root.Controls){if(predicate(child))return child;var result=Find(child,predicate);if(result!=null)return result;}return null;}
        public static async Task Run(string folder){
            int mode=0;var monitor=new WirelessMonitor(Path.Combine(folder,"layout-test-peers.xml"),(args,timeout,input)=>{
                string output="";
                if(args[0]=="devices")output="List of devices attached\n"+(mode==0?"192.0.2.10:32123 device model:Layout_Phone transport_id:1\n":"");
                else if(args[0]=="mdns")output="List of discovered mdns services\n"+(mode==0?"adb-layout _adb-tls-connect._tcp 192.0.2.10:32123\n":mode==1?"adb-layout-pair _adb-tls-pairing._tcp 192.0.2.10:31237\n":"");
                else if(args.Length>3&&args[2]=="shell")output="wireless-layout-phone\n";
                else if(args[0]=="connect")output="connected to 192.0.2.10:32123\n";
                else throw new Exception("Unexpected test command");
                return Task.FromResult(new Result{Code=0,Output=output,Error=""});
            });
            var area=Screen.PrimaryScreen.WorkingArea;
            using(var dialog=new WirelessDialog(monitor)){
                dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(area.Left+40,area.Top+40);dialog.Show();await monitor.Refresh();await Task.Delay(50);
                Check(dialog.MinimumSize.Height>=400&&dialog.Height>=400,"minimum height and content area survive the fixed-width constraint");
                Size original=dialog.Size;int resizes=0;dialog.SizeChanged+=(s,e)=>resizes++;
                for(int i=0;i<20;i++){await monitor.Refresh();await Task.Yield();}
                Check(dialog.Size==original&&resizes==0,"unchanged discovery must not resize the window: "+original+" -> "+dialog.Size+", changes="+resizes);
                dialog.MaximumSize=new Size(dialog.Width,430);var manual=(Button)Find(dialog,c=>c is Button&&c.Text=="无法发现？手动填写地址");Check(manual!=null,"manual entry exists");manual.PerformClick();await Task.Delay(50);
                var content=(FlowLayoutPanel)Find(dialog,c=>c is FlowLayoutPanel&&((FlowLayoutPanel)c).AutoScroll);
                Check(content!=null&&content.VerticalScroll.Visible&&!content.HorizontalScroll.Visible,"constrained manual form uses vertical scrolling without a horizontal strip");
                int width=dialog.Width;for(int i=0;i<10;i++)await monitor.Refresh();Check(dialog.Width==width,"scrollbars and polling keep width fixed");
                dialog.MaximumSize=new Size(dialog.Width,Int16.MaxValue);manual.PerformClick();await Task.Delay(30);
                mode=1;await monitor.Refresh();await Task.Delay(30);Size pairing=dialog.Size;var code=(Field)Find(dialog,c=>c is Field&&c.AccessibleName=="无线调试配对码");
                Check(code!=null&&code.Visible,"pairing section appears");code.Input.Text="123456";
                mode=2;await monitor.Refresh();await Task.Delay(30);Check(dialog.Size==pairing&&code.Visible&&code.Input.Text=="123456","a missed advertisement preserves pairing layout and typed code");
                mode=0;await monitor.Refresh();await Task.Delay(30);Check(dialog.Width==original.Width,"pairing state cannot change width");
                Check(dialog.Opacity==1&&dialog.TransparencyKey.IsEmpty,"dialog remains opaque");dialog.Close();
            }
        }
    }
}
