using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace AppDeck {
    internal static class DrawingQualityChecks {
        public static void Render(string folder,string theme){
            using(var source=new Bitmap(192,192,PixelFormat.Format32bppPArgb)){
                using(var g=Graphics.FromImage(source))g.Clear(Color.FromArgb(26,164,230));
                foreach(float scale in new[]{1f,1.25f,1.5f,2f}){
                    int size=(int)(48*scale);using(var mask=new Bitmap(size+8,size+8,PixelFormat.Format32bppPArgb)){
                        using(var g=Graphics.FromImage(mask))SmoothDrawing.Image(g,new RectangleF(4,4,size,size),source,size*.24f);
                        var center=mask.GetPixel(4+size/2,4+size/2);if(center.A<250||Math.Abs(center.R-26)>2||Math.Abs(center.G-164)>2||Math.Abs(center.B-230)>2)throw new Exception("Rounded image changed the image interior");
                        var levels=new HashSet<int>();for(int y=4;y<4+size/3;y++)for(int x=4;x<4+size/3;x++){int alpha=mask.GetPixel(x,y).A;if(alpha>0&&alpha<255)levels.Add(alpha);}
                        if(levels.Count<8)throw new Exception("Rounded image lacks fractional edge coverage at scale "+scale);
                    }
                    using(var border=new Bitmap(size+12,size+12,PixelFormat.Format32bppPArgb)){
                        using(var g=Graphics.FromImage(border))PaintKit.Border(g,new RectangleF(4.5f,4.5f,size,size),Color.White,size*.24f);
                        var levels=new HashSet<int>();for(int y=3;y<4+size/3;y++)for(int x=3;x<4+size/3;x++){int alpha=border.GetPixel(x,y).A;if(alpha>0&&alpha<255)levels.Add(alpha);}
                        if(levels.Count<8)throw new Exception("Rounded border lacks fractional edge coverage at scale "+scale);
                    }
                    using(var bitmap=new Bitmap((int)(480*scale),(int)(136*scale))){
                        using(var g=Graphics.FromImage(bitmap)){
                            g.Clear(Theme.Paper);g.SmoothingMode=SmoothingMode.AntiAlias;
                            var oldIcon=new RectangleF(16*scale,20*scale,48*scale,48*scale);var state=g.Save();using(var path=PaintKit.Round(oldIcon,oldIcon.Width*.24f))g.SetClip(path);g.DrawImage(source,oldIcon);g.Restore(state);
                            SmoothDrawing.Image(g,new RectangleF(96*scale,20*scale,48*scale,48*scale),source,48*scale*.24f);
                            var oldBorder=new RectangleF(184*scale+.5f,20*scale+.5f,112*scale,48*scale);using(var path=PaintKit.Round(oldBorder,12*scale))using(var pen=new Pen(Theme.ControlLine))g.DrawPath(pen,path);
                            PaintKit.Border(g,new RectangleF(328*scale+.5f,20*scale+.5f,112*scale,48*scale),Theme.ControlLine,12*scale);
                            using(var font=Theme.Font(9*scale))using(var brush=new SolidBrush(Theme.Ink)){g.DrawString("旧图标裁切",font,brush,12*scale,88*scale);g.DrawString("新图标绘制",font,brush,92*scale,88*scale);g.DrawString("旧圆角边框",font,brush,188*scale,88*scale);g.DrawString("新圆角边框",font,brush,332*scale,88*scale);}
                        }
                        bitmap.Save(Path.Combine(folder,"edge-quality-"+(int)(scale*100)+"-"+theme+".png"),ImageFormat.Png);
                    }
                }
            }
        }
    }
}
