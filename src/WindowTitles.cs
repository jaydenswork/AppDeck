using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace AppDeck {
    internal static class WindowTitles {
        public static event Action<string> Changed;
        static string FilePath {get{return Path.Combine(Paths.Data,"window-titles.xml");}}
        static XDocument Read(){return File.Exists(FilePath)?XDocument.Load(FilePath):new XDocument(new XElement("windowTitles"));}
        public static string Get(string package,string fallback){
            try{var item=Read().Root.Elements("app").FirstOrDefault(e=>(string)e.Attribute("package")==package);string title=item==null?null:(string)item.Attribute("title");return String.IsNullOrWhiteSpace(title)?fallback:title;}catch{return fallback;}
        }
        public static void Save(string package,string title){
            if(!Commands.ValidPackage(package)||String.IsNullOrWhiteSpace(title))return;title=title.Trim();if(title.Length>100)title=title.Substring(0,100);
            Directory.CreateDirectory(Paths.Data);var doc=Read();var item=doc.Root.Elements("app").FirstOrDefault(e=>(string)e.Attribute("package")==package);
            if(item==null){item=new XElement("app",new XAttribute("package",package));doc.Root.Add(item);}item.SetAttributeValue("title",title);
            string temp=FilePath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{doc.Save(temp);if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);}finally{if(File.Exists(temp))File.Delete(temp);}
            var changed=Changed;if(changed!=null)changed(package);
        }
    }
}
