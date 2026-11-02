using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace AppDeck {
    // Renders this application's own controls offscreen; no desktop capture or device access.
    internal static class UIPreview {
        public static int Render(string folder){try{
            Directory.CreateDirectory(folder);
            foreach(bool dark in new[]{false,true}){
                Theme.Set(dark);CheckContrast();string suffix=dark?"dark":"light";
                using(var f=new MainForm(new[]{"--preview"})){Save(f,Path.Combine(folder,"home-"+suffix+".png"));SaveOptions(f,Path.Combine(folder,"home-options-"+suffix+".png"));f.Size=new Size(1050,740);Save(f,Path.Combine(folder,"home-compact-"+suffix+".png"));SaveOptions(f,Path.Combine(folder,"home-compact-options-"+suffix+".png"));}
                using(var f=new MainForm(new[]{"--preview"})){f.PreviewCatalog(true);Save(f,Path.Combine(folder,"home-loading-"+suffix+".png"));f.PreviewCatalog(false);Save(f,Path.Combine(folder,"home-empty-"+suffix+".png"));}
                using(var f=new CastForm("preview",new AndroidApp{Name="阅读",Package="io.legado.app.release"},"阅读",0,false,false,false,false,true)){Save(f,Path.Combine(folder,"cast-"+suffix+".png"));f.Size=new Size(330,620);Save(f,Path.Combine(folder,"cast-compact-"+suffix+".png"));}
                using(var f=new CastForm("preview",new AndroidApp{Name="阅读",Package="io.legado.app.release"},"阅读",0,false,false,false,false,true,true))Save(f,Path.Combine(folder,"cast-landscape-"+suffix+".png"));
                using(var f=new ShortcutDialog())Save(f,Path.Combine(folder,"shortcuts-"+suffix+".png"));
                using(var f=new WirelessDialog())Save(f,Path.Combine(folder,"wireless-"+suffix+".png"));
                using(var f=new WirelessDialog()){f.Preview(true,false);Save(f,Path.Combine(folder,"wireless-pair-"+suffix+".png"));}
                using(var f=new WirelessDialog()){f.Preview(false,true);Save(f,Path.Combine(folder,"wireless-manual-"+suffix+".png"));}
                using(var f=new ModernForm{Text="阅读 · AppDeck",ClientSize=new Size(438,680)}){var view=new ConnectionView{Dock=DockStyle.Fill,Stage=2,Text="正在等待应用画面\n应用已启动，但画面尚未到达。\n可以继续等待，或尝试重新连接。",CanRetry=true};f.Body.Controls.Add(view);Save(f,Path.Combine(folder,"cast-waiting-"+suffix+".png"));f.Size=new Size(330,340);Save(f,Path.Combine(folder,"cast-waiting-compact-"+suffix+".png"));f.Size=new Size(330,300);Save(f,Path.Combine(folder,"cast-waiting-min-"+suffix+".png"));foreach(Control action in view.Controls)if(action.Bottom>view.Height||action.Left<0||action.Right>view.Width)throw new Exception("Waiting action clipped at minimum size");}
                using(var f=new CloseChoiceDialog())Save(f,Path.Combine(folder,"close-choice-"+suffix+".png"));
                SaveMenu(Path.Combine(folder,"cast-menu-"+suffix+".png"));
                DrawingQualityChecks.Render(folder,suffix);
            }
            File.WriteAllText(Path.Combine(folder,"render-check.txt"),"PASS: 44 offscreen renders; light/dark; main normal/compact/scrolled/loading/empty states; cast portrait/compact/landscape and waiting states down to 330x300; shortcut/wireless discovery/pairing/manual/close dialogs; caption and waiting action bounds; full-width caption dividers including below buttons; text/button contrast >=4.5 and control borders >=3.0; cast menu without check/image columns; rounded-image/border fractional alpha and unchanged image interior at 100/125/150/200 percent raster scales. No device access or desktop capture; raster scales do not change system DPI.\r\n");return 0;
        }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"error.txt"),ex.ToString());return 1;}}
        static double Luminance(Color c){double[] rgb={c.R/255.0,c.G/255.0,c.B/255.0};for(int i=0;i<3;i++)rgb[i]=rgb[i]<=.04045?rgb[i]/12.92:Math.Pow((rgb[i]+.055)/1.055,2.4);return rgb[0]*.2126+rgb[1]*.7152+rgb[2]*.0722;}
        static double Contrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
        static void CheckContrast(){foreach(Color bg in new[]{Theme.Paper,Theme.Surface})if(Contrast(Theme.Ink,bg)<4.5||Contrast(Theme.Muted,bg)<4.5||Contrast(Theme.ControlLine,bg)<3)throw new Exception("Text or control boundary contrast below target");foreach(Color bg in new[]{Theme.AccentFill,Theme.PrimaryHover,Theme.PrimaryPressed})if(Contrast(Color.White,bg)<4.5)throw new Exception("Primary button contrast below target");}
        static ScrollArea FindOptions(Control parent){foreach(Control child in parent.Controls){var area=child as ScrollArea;if(area!=null)return area;var nested=FindOptions(child);if(nested!=null)return nested;}return null;}
        static void SaveOptions(Form form,string path){
            var area=FindOptions(form);if(area==null)throw new Exception("Main options panel missing");
            area.ScrollTo(Int32.MaxValue);Save(form,path);
            var toggle=area.Content.Controls[area.Content.Controls.Count-1];
            if(toggle.Top+area.Content.Top<0||toggle.Bottom+area.Content.Top>area.Height)throw new Exception("Last option clipped after scrolling: "+path);
            area.ScrollTo(0);
        }
        static void SaveMenu(string path){
            using(var menu=new ContextMenuStrip()){
                foreach(string label in new[]{"手机锁屏","修改窗口标题","快捷键","重新连接","切换主题"})menu.Items.Add(new ToolStripMenuItem(label));
                Theme.Menu(menu);menu.CreateControl();menu.PerformLayout();menu.Size=menu.GetPreferredSize(Size.Empty);
                using(var bitmap=new Bitmap(menu.Width,menu.Height)){
                    menu.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
                    if(menu.ShowCheckMargin||menu.ShowImageMargin)throw new Exception("Cast menu should not reserve a check column");
                    bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
                }
            }
        }
        static void Save(Form form,string path){form.TopLevel=false;form.CreateControl();Layout(form);var modern=form as ModernForm;if(modern!=null){int expected=form is CloseChoiceDialog||form is WirelessDialog?1:3;if(modern.Caption.ConfiguredWindowButtonCount!=expected)throw new Exception("Unexpected caption button count: "+path);modern.Caption.PerformLayout();if(!modern.Caption.WindowButtonBoundsReady)throw new Exception("Caption buttons were not laid out while the form was hidden: "+path);}form.Visible=true;Layout(form);if(modern!=null)CheckCaptionLine(modern.Caption);if(modern!=null)foreach(Control button in modern.Caption.Controls)if(button.Visible&&(button.Left<0||button.Right>modern.Caption.Width||button.Width<24))throw new Exception("Caption button clipped: "+path);using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
        static void Layout(Control c){c.CreateControl();c.PerformLayout();foreach(Control child in c.Controls)Layout(child);}
        static void CheckCaptionLine(CaptionBar caption){using(var bitmap=new Bitmap(caption.Width,caption.Height)){caption.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));int reference=bitmap.GetPixel(2,caption.Height-1).ToArgb();if(reference==Theme.Surface.ToArgb())throw new Exception("Caption divider missing");foreach(Control button in caption.Controls)if(button.Visible&&bitmap.GetPixel(button.Left+button.Width/2,caption.Height-1).ToArgb()!=reference)throw new Exception("Caption button covers the divider");}}
    }
    internal sealed class ReadingPreview:Control {
        public ReadingPreview(){DoubleBuffered=true;Theme.Watch(this,()=>{BackColor=Theme.Paper;Invalidate();});}
        protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Theme.Dark?Theme.C(0x191B24):Theme.C(0xFAF8F3));float s=g.DpiX/96;int pad=(int)(32*s);using(var small=Theme.Font(9))using(var title=Theme.Font(17,true))using(var body=Theme.Font(12)){
            PaintKit.Text(g,"山间来信",small,Theme.Muted,new Rectangle(pad,(int)(28*s),Width-2*pad,(int)(24*s)));
            PaintKit.Text(g,"第一章  风经过的地方",title,Theme.Ink,new Rectangle(pad,(int)(85*s),Width-2*pad,(int)(42*s)));
            string[] lines={"清晨，窗外的山还藏在薄雾里。","她推开窗，微凉的风穿过书页，","停在昨天没有读完的那一行。","","远处的路渐渐亮了起来。","一封信安静地躺在桌角，没有","署名，只有熟悉的字迹。","","她把灯关掉，坐回窗边。","今天的故事，从这里开始。"};int y=(int)(158*s);foreach(var line in lines){if(y+32*s>Height-76*s)break;PaintKit.Text(g,line,body,Theme.Ink,new Rectangle(pad,y,Width-2*pad,(int)(32*s)));y+=(int)(36*s);}
            PaintKit.Text(g,"阅读界面示意  ·  实际内容来自手机应用",small,Theme.Muted,new Rectangle(pad,Height-(int)(44*s),Width-2*pad,(int)(24*s)));
        }}
    }
}
