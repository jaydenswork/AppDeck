using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.CompilerServices;

namespace AppDeck {
    // Cache only the final, device-sized rasters. Text stays on the normal GDI path.
    internal static class SmoothDrawing {
        const int Pad=2,Limit=192;const long Budget=12*1024*1024;
        struct Key:IEquatable<Key> {
            public int W,H,X,Y,Radius,Stroke,Argb,Kind;public Image Source;
            public bool Equals(Key other){return W==other.W&&H==other.H&&X==other.X&&Y==other.Y&&Radius==other.Radius&&Stroke==other.Stroke&&Argb==other.Argb&&Kind==other.Kind&&Object.ReferenceEquals(Source,other.Source);}
            public override bool Equals(object value){return value is Key&&Equals((Key)value);}
            public override int GetHashCode(){unchecked{int h=W;h=h*31+H;h=h*31+X;h=h*31+Y;h=h*31+Radius;h=h*31+Stroke;h=h*31+Argb;h=h*31+Kind;return h*31+(Source==null?0:RuntimeHelpers.GetHashCode(Source));}}
        }
        sealed class Entry {public Bitmap Bitmap;public LinkedListNode<Key> Node;}
        static readonly Dictionary<Key,Entry> cache=new Dictionary<Key,Entry>();
        static readonly LinkedList<Key> order=new LinkedList<Key>();static long bytes;
        public static void Fill(Graphics graphics,RectangleF bounds,Color color,float radius){Draw(graphics,bounds,color,radius,0,null,0);}
        public static void Border(Graphics graphics,RectangleF bounds,Color color,float radius,float stroke){Draw(graphics,bounds,color,radius,stroke,null,1);}
        public static void Image(Graphics graphics,RectangleF bounds,Image image,float radius){Draw(graphics,bounds,Color.Transparent,radius,0,image,2);}
        static int Fixed(float value){return (int)Math.Round(value*256);}
        static void Draw(Graphics graphics,RectangleF bounds,Color color,float radius,float stroke,Image source,int kind){
            if(bounds.Width<=0||bounds.Height<=0)return;int left=(int)Math.Floor(bounds.Left),top=(int)Math.Floor(bounds.Top);
            var key=new Key{W=Fixed(bounds.Width),H=Fixed(bounds.Height),X=Fixed(bounds.Left-left),Y=Fixed(bounds.Top-top),Radius=Fixed(radius),Stroke=Fixed(stroke),Argb=color.ToArgb(),Kind=kind,Source=source};
            Entry entry;Bitmap bitmap;bool temporary=false;
            if(cache.TryGetValue(key,out entry)){order.Remove(entry.Node);order.AddLast(entry.Node);bitmap=entry.Bitmap;}
            else{
                bitmap=Render(key);long size=(long)bitmap.Width*bitmap.Height*4;temporary=size>Budget;
                if(!temporary){while(order.Count>0&&(cache.Count>=Limit||bytes+size>Budget)){var first=order.First;var old=cache[first.Value];bytes-=(long)old.Bitmap.Width*old.Bitmap.Height*4;old.Bitmap.Dispose();cache.Remove(first.Value);order.RemoveFirst();}cache.Add(key,new Entry{Bitmap=bitmap,Node=order.AddLast(key)});bytes+=size;}
            }
            try{graphics.DrawImageUnscaled(bitmap,left-Pad,top-Pad);}finally{if(temporary)bitmap.Dispose();}
        }
        static Bitmap Render(Key key){
            float width=key.W/256f,height=key.H/256f,x=Pad+key.X/256f,y=Pad+key.Y/256f;
            int w=(int)Math.Ceiling(width+key.X/256f)+2*Pad,h=(int)Math.Ceiling(height+key.Y/256f)+2*Pad,samples=w*h>250000?2:4;
            using(var high=new Bitmap(w*samples,h*samples,PixelFormat.Format32bppPArgb)){
                using(var g=Graphics.FromImage(high))using(var path=PaintKit.Round(new RectangleF(x,y,width,height),key.Radius/256f)){
                    g.ScaleTransform(samples,samples);g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                    if(key.Kind==0){using(var brush=new SolidBrush(Color.FromArgb(key.Argb)))g.FillPath(brush,path);}
                    else if(key.Kind==1){using(var pen=new Pen(Color.FromArgb(key.Argb),key.Stroke/256f))g.DrawPath(pen,path);}
                    else using(var scaled=new Bitmap(high.Width,high.Height,PixelFormat.Format32bppPArgb)){
                        using(var imageGraphics=Graphics.FromImage(scaled))using(var attributes=new ImageAttributes()){
                            imageGraphics.InterpolationMode=InterpolationMode.HighQualityBicubic;imageGraphics.PixelOffsetMode=PixelOffsetMode.HighQuality;attributes.SetWrapMode(WrapMode.TileFlipXY);
                            imageGraphics.DrawImage(key.Source,new Rectangle((int)Math.Round(x*samples),(int)Math.Round(y*samples),(int)Math.Round(width*samples),(int)Math.Round(height*samples)),0,0,key.Source.Width,key.Source.Height,GraphicsUnit.Pixel,attributes);
                        }
                        // FillPath applies coverage to alpha; a Graphics clip region has hard edges.
                        using(var brush=new TextureBrush(scaled,WrapMode.Clamp)){brush.ScaleTransform(1f/samples,1f/samples);g.FillPath(brush,path);}
                    }
                }
                var result=new Bitmap(w,h,PixelFormat.Format32bppPArgb);
                using(var g=Graphics.FromImage(result))using(var attributes=new ImageAttributes()){
                    g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;attributes.SetWrapMode(WrapMode.TileFlipXY);
                    g.DrawImage(high,new Rectangle(0,0,w,h),0,0,high.Width,high.Height,GraphicsUnit.Pixel,attributes);
                }
                return result;
            }
        }
    }
}
