using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AppDeck {
    // Each lease has its own journal; unrelated application tasks are never removed.
    internal sealed class TaskLease {
        static readonly HashSet<string> active=new HashSet<string>();
        static string Folder {get{return Path.Combine(Paths.Data,"task-sessions");}}
        readonly string serial,jar,state,file;
        public TaskLease(string serial,string jar){
            this.serial=serial;this.jar=jar;string id=Guid.NewGuid().ToString("N");
            state="/data/local/tmp/appdeck-session-"+id+".properties";file=Path.Combine(Folder,id+".xml");
        }
        public bool Pending {get{return File.Exists(file);}}
        static Task<Result> Call(string serial,string jar,params string[] args){
            return Commands.Adb(serial,new[]{"shell","CLASSPATH="+jar,"app_process","/","TaskSession"}.Concat(args).ToArray());
        }
        public async Task<string> Acquire(int display,string package,string component){
            if(Pending)throw new Exception("上次任务尚未归还，请先恢复手机连接。");
            Directory.CreateDirectory(Folder);
            new XDocument(new XElement("lease",new XAttribute("serial",serial),new XAttribute("jar",jar),new XAttribute("state",state))).Save(file);
            active.Add(file);
            var result=await Call(serial,jar,"acquire",state,display.ToString(),package,component);result.Ensure();
            if(!result.Output.Contains("ACQUIRED task="))throw new Exception("无法确认任务接管结果："+result.Combined.Trim());
            return result.Output.Trim();
        }
        public async Task<string> Return(){
            if(!Pending)return "No active task lease";
            var result=await Call(serial,jar,"release",state);result.Ensure();
            if(!result.Output.Contains("RELEASED"))throw new Exception("无法确认任务归还结果："+result.Combined.Trim());
            File.Delete(file);active.Remove(file);return result.Output.Trim();
        }
        public void Abandon(){active.Remove(file);}
        public static async Task Recover(string serial){
            if(!Directory.Exists(Folder))return;
            foreach(string path in Directory.GetFiles(Folder,"*.xml")){
                if(!active.Add(path))continue;
                try{
                    var e=XDocument.Load(path).Root;if((string)e.Attribute("serial")!=serial)continue;
                    string jar=(string)e.Attribute("jar"),state=(string)e.Attribute("state");
                    if(!System.Text.RegularExpressions.Regex.IsMatch(jar??"",@"^/data/local/tmp/appdeck-layout-[a-f0-9]{32}\.jar$")||!System.Text.RegularExpressions.Regex.IsMatch(state??"",@"^/data/local/tmp/appdeck-session-[a-f0-9]{32}\.properties$"))continue;
                    var push=await Commands.Adb(serial,"push",Path.Combine(Paths.Runtime,"appdeck-layout.jar"),jar);push.Ensure();
                    var result=await Call(serial,jar,"recover",state);result.Ensure();
                    if(result.Output.Contains("RELEASED")){
                        File.Delete(path);await Commands.Adb(serial,"shell","rm","-f",jar);
                    }
                }catch(Exception ex){
                    try{File.AppendAllText(Path.Combine(Folder,"recovery.log"),DateTime.Now.ToString("s")+" "+Path.GetFileName(path)+" "+ex.Message+Environment.NewLine);}catch{}
                }finally{active.Remove(path);}
            }
        }
    }
}
