using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace AppDeck {
    internal static class DeviceCache {
        public static string Folder(Device device,string dataRoot=null){
            string identity=String.IsNullOrWhiteSpace(device.HardwareId)?device.Serial??"":device.HardwareId;
            using(var hash=SHA256.Create()){string key=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(identity))).Replace("-","").Substring(0,24);return Path.Combine(dataRoot??Paths.Data,"icons","v2-"+key);}
        }
        public static List<AndroidApp> ReadCatalog(Device device,string dataRoot=null){
            try{
                var root=XElement.Load(Path.Combine(Folder(device,dataRoot),"catalog.xml"));if(root.Name!="catalog"||(int?)root.Attribute("schema")!=1)return new List<AndroidApp>();
                return root.Elements("app").Take(2000).Select(e=>new AndroidApp{Name=(string)e.Attribute("name"),Package=(string)e.Attribute("package")}).Where(a=>Commands.ValidPackage(a.Package)&&!String.IsNullOrWhiteSpace(a.Name)).GroupBy(a=>a.Package).Select(g=>g.First()).ToList();
            }catch(IOException){}catch(UnauthorizedAccessException){}catch(System.Xml.XmlException){}catch(FormatException){}catch(OverflowException){}
            return new List<AndroidApp>();
        }
        public static bool SaveCatalog(Device device,IEnumerable<AndroidApp> apps,string dataRoot=null){
            string folder=Folder(device,dataRoot),temp=Path.Combine(folder,"catalog-"+Guid.NewGuid().ToString("N")+".tmp"),path=Path.Combine(folder,"catalog.xml");
            try{
                Directory.CreateDirectory(folder);var root=new XElement("catalog",new XAttribute("schema",1),new XAttribute("savedUtc",DateTime.UtcNow.ToString("o")),apps.Where(a=>!a.System&&Commands.ValidPackage(a.Package)&&!String.IsNullOrWhiteSpace(a.Name)).GroupBy(a=>a.Package).Select(g=>g.First()).Take(2000).Select(a=>new XElement("app",new XAttribute("package",a.Package),new XAttribute("name",a.Name))));
                root.Save(temp);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);return true;
            }catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}catch(ArgumentException){return false;}
            finally{try{if(File.Exists(temp))File.Delete(temp);}catch(IOException){}catch(UnauthorizedAccessException){}}
        }
    }
}
