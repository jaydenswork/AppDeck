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
                Theme.Set(dark);string suffix=dark?"dark":"light";
                using(var f=new MainForm(new[]{"--preview"})){Save(f,Path.Combine(folder,"home-"+suffix+".png"));SaveOptions(f,Path.Combine(folder,"home-options-"+suffix+".png"));f.Size=new Size(1050,740);Save(f,Path.Combine(folder,"home-compact-"+suffix+".png"));SaveOptions(f,Path.Combine(folder,"home-compact-options-"+suffix+".png"));}
                using(var f=new CastForm("preview",new AndroidApp{Name="阅读",Package="io.legado.app.release"},"阅读",0,false,false,false,false,true)){Save(f,Path.Combine(folder,"cast-"+suffix+".png"));f.Size=new Size(330,620);Save(f,Path.Combine(folder,"cast-compact-"+suffix+".png"));}
                using(var f=new CastForm("preview",new AndroidApp{Name="阅读",Package="io.legado.app.release"},"阅读",0,false,false,false,false,true,true))Save(f,Path.Combine(folder,"cast-landscape-"+suffix+".png"));
                using(var f=new ShortcutDialog())Save(f,Path.Combine(folder,"shortcuts-"+suffix+".png"));
                using(var f=new WirelessDialog())Save(f,Path.Combine(folder,"wireless-"+suffix+".png"));
                using(var f=new CloseChoiceDialog())Save(f,Path.Combine(folder,"close-choice-"+suffix+".png"));
                SaveMenu(Path.Combine(folder,"cast-menu-"+suffix+".png"));
            }
            File.WriteAllText(Path.Combine(folder,"render-check.txt"),"PASS: 22 offscreen renders; light/dark; main normal/compact including scrolled options; cast portrait/compact/landscape; shortcut/wireless/close dialogs; caption button bounds; cast menu without check/image columns. No device access or desktop capture.\r\n");return 0;
        }catch(Exception ex){File.WriteAllText(Path.Combine(folder,"error.txt"),ex.ToString());return 1;}}
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
        static void Save(Form form,string path){form.TopLevel=false;form.CreateControl();Layout(form);var modern=form as ModernForm;if(modern!=null){int expected=form is CloseChoiceDialog?1:3;if(modern.Caption.ConfiguredWindowButtonCount!=expected)throw new Exception("Unexpected caption button count: "+path);modern.Caption.PerformLayout();if(!modern.Caption.WindowButtonBoundsReady)throw new Exception("Caption buttons were not laid out while the form was hidden: "+path);}form.Visible=true;Layout(form);if(modern!=null)foreach(Control button in modern.Caption.Controls)if(button.Visible&&(button.Left<0||button.Right>modern.Caption.Width||button.Width<24))throw new Exception("Caption button clipped: "+path);using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
        static void Layout(Control c){c.CreateControl();c.PerformLayout();foreach(Control child in c.Controls)Layout(child);}
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
