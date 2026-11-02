using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AppDeck {
    internal static class AppIcons {
        static readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
        static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
        static string Folder(Device device){using(var hash=SHA256.Create()){string key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(device.HardwareId??device.Serial))).Replace("-","").Substring(0,24);return Path.Combine(Paths.Data,"icons","v2-"+key);}}
        public static void AssignCached(Device device,IEnumerable<AndroidApp> apps){
            string folder=Folder(device);foreach(var app in apps){string path=Path.Combine(folder,app.Package+".png");Image image;
                if(!images.TryGetValue(path,out image)&&File.Exists(path))try{using(var input=new MemoryStream(File.ReadAllBytes(path)))using(var decoded=Image.FromStream(input)){image=new Bitmap(decoded);images[path]=image;}}catch(ArgumentException){try{File.Delete(path);}catch(IOException){}}catch(IOException){}
                if(image!=null)app.Icon=image;
            }
        }
        public static async Task Fetch(Device device,IEnumerable<AndroidApp> apps){
            await gate.WaitAsync();try{
                string folder=Folder(device);var packages=apps.Select(a=>a.Package).Where(Commands.ValidPackage).Distinct().Where(p=>!File.Exists(Path.Combine(folder,p+".png"))||File.GetLastWriteTimeUtc(Path.Combine(folder,p+".png"))<DateTime.UtcNow.AddDays(-7)).ToList();
                if(packages.Count==0)return;Directory.CreateDirectory(folder);string remote="/data/local/tmp/appdeck-icons-"+Guid.NewGuid().ToString("N")+".jar";
                try{
                    var upload=await Commands.Adb(device.Serial,"push",Path.Combine(Paths.Runtime,"appdeck-layout.jar"),remote);upload.Ensure();
                    for(int first=0;first<packages.Count;first+=32){
                        var batch=packages.Skip(first).Take(32).ToArray();var args=new List<string>{"shell","CLASSPATH="+remote,"app_process","/","AppIcons"};args.AddRange(batch);
                        var result=await Commands.Adb(device.Serial,args.ToArray());result.Ensure();
                        foreach(string line in result.Output.Split('\n')){var fields=line.TrimEnd('\r').Split('\t');if(fields.Length!=3||fields[0]!="ICON"||!batch.Contains(fields[1])||fields[2].Length>262144)continue;
                            try{byte[] bytes=Convert.FromBase64String(fields[2]);using(var input=new MemoryStream(bytes))using(var image=Image.FromStream(input)){if(image.Width!=192||image.Height!=192)continue;}string path=Path.Combine(folder,fields[1]+".png"),temp=path+".tmp-"+Guid.NewGuid().ToString("N");try{File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}catch(FormatException){}catch(ArgumentException){}
                        }
                    }
                }catch(Exception error){try{File.AppendAllText(Path.Combine(Paths.Data,"icons.log"),DateTime.Now.ToString("s")+" "+error.Message+Environment.NewLine);}catch(IOException){}}
                try{await Commands.Adb(device.Serial,"shell","rm","-f",remote);}catch(Exception){}
            }finally{gate.Release();}
        }
    }
}
