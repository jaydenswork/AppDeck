using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppDeck {
    internal static class CastChecks {
        public static int Live(string serial,string package,string folder){
            if(!Commands.ValidPackage(package))throw new ArgumentException("Invalid package");Directory.CreateDirectory(folder);int exit=1;
            using(var form=new CastForm(serial,new AndroidApp{Name="投屏诊断",Package=package},"AppDeck 投屏诊断",0,false,false,false,false)){
                form.AutoWakeDisplay=false;var area=Screen.PrimaryScreen.WorkingArea;form.StartPosition=FormStartPosition.Manual;form.Bounds=new Rectangle(area.Left+Math.Max(10,(area.Width-370)/2),area.Top+40,370,Math.Min(700,area.Height-80));
                form.Shown+=async(s,e)=>{
                    var report=new StringBuilder();
                    try{
                        for(int i=0;i<50&&!form.VideoReady;i++)await Task.Delay(200);
                        report.AppendLine("Before recovery: display="+form.ActiveDisplay+" textures="+form.ReceivedFrames+" ready="+form.VideoReady);
                        if(!form.VideoReady){await form.WakeVirtualDisplay();for(int i=0;i<50&&!form.VideoReady;i++)await Task.Delay(200);}
                        report.AppendLine("After recovery: display="+form.ActiveDisplay+" textures="+form.ReceivedFrames+" ready="+form.VideoReady);
                        exit=form.VideoReady?0:1;
                    }catch(Exception ex){report.AppendLine(ex.ToString());}
                    finally{
                        File.WriteAllText(Path.Combine(folder,"cast-check.txt"),report.ToString(),Encoding.UTF8);
                    }
                    var closeWatch=Stopwatch.StartNew();var close=form.StopAndClose();
                    File.AppendAllText(Path.Combine(folder,"cast-check.txt"),"Close response: hidden="+!form.Visible+" elapsedMs="+closeWatch.ElapsedMilliseconds+"\r\n");
                    await close;File.AppendAllText(Path.Combine(folder,"cast-check.txt"),"Cleanup complete: disposed="+form.IsDisposed+" elapsedMs="+closeWatch.ElapsedMilliseconds+"\r\n");
                    if(!form.IsDisposed){File.AppendAllText(Path.Combine(folder,"cast-check.txt"),"Cleanup not completed; session retained\r\n");exit=1;}
                };Application.Run(form);
            }return exit;
        }
    }
}
