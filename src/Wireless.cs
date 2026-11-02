using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AppDeck {
    internal sealed class WirelessService {
        public string Name,Type,Address;
        public bool Pairing {get{return Type=="_adb-tls-pairing._tcp";}}
        public string Host {get{return Address.Substring(0,Address.LastIndexOf(':'));}}
        public bool Matches(string serial){return serial==Address||serial.TrimEnd('.')==Name+"."+Type;}
        public static List<WirelessService> Parse(string output){
            var list=new List<WirelessService>();
            foreach(string line in output.Split('\n')){
                var m=Regex.Match(line.Trim(),@"^(\S+)\s+(_adb-tls-(?:pairing|connect)\._tcp)\.?\s+(\S+)$");
                if(!m.Success||!Commands.ValidAddress(m.Groups[3].Value))continue;
                var service=new WirelessService{Name=m.Groups[1].Value,Type=m.Groups[2].Value,Address=m.Groups[3].Value};
                if(!list.Any(s=>s.Name==service.Name&&s.Type==service.Type&&s.Address==service.Address))list.Add(service);
            }
            return list;
        }
    }
    internal sealed class WirelessPeer {
        public string Id,Model;
        public readonly HashSet<string> Services=new HashSet<string>(StringComparer.Ordinal);
    }
    internal sealed class NearbyPhone {
        public string Host,Name;
        public WirelessService Pair,Connect;
        public bool Connected;
        public override string ToString(){return Name+" · "+Host+" · "+(Connected?"已连接":Pair!=null?"可配对":"可连接");}
    }
    internal sealed class WirelessMonitor {
        readonly Func<string[],int,string,Task<Result>> run;
        readonly string peerFile;
        readonly List<WirelessPeer> peers=new List<WirelessPeer>();
        readonly Dictionary<string,string> identities=new Dictionary<string,string>();
        readonly Dictionary<string,DateTime> attempts=new Dictionary<string,DateTime>();
        Task refreshTask;
        bool peersDirty;
        string pairedHost;DateTime pairUntil;
        public bool Busy {get;private set;}
        public List<Device> Devices {get;private set;}
        public List<WirelessService> Services {get;private set;}
        public string DiscoveryError {get;private set;}
        public event EventHandler Changed;
        public WirelessMonitor(string file=null,Func<string[],int,string,Task<Result>> execute=null){
            peerFile=file??Path.Combine(Paths.Data,"wireless-devices.xml");
            run=execute??((args,timeout,input)=>Commands.Run(Paths.Adb,args,timeout,input));
            Devices=new List<Device>();Services=new List<WirelessService>();DiscoveryError="";
            try{foreach(var e in XElement.Load(peerFile).Elements("device")){
                string id=(string)e.Attribute("id");if(String.IsNullOrWhiteSpace(id))continue;
                var peer=new WirelessPeer{Id=id,Model=(string)e.Attribute("model")??"Android"};
                foreach(var s in e.Elements("service"))if(!String.IsNullOrWhiteSpace(s.Value))peer.Services.Add(s.Value);
                peers.Add(peer);
            }}catch(IOException){}catch(System.Xml.XmlException){}catch(UnauthorizedAccessException){}
        }
        void Publish(){var h=Changed;if(h!=null)h(this,EventArgs.Empty);}
        public Task Refresh(){
            if(Busy)return Task.FromResult(0);
            if(refreshTask==null||refreshTask.IsCompleted)refreshTask=RefreshCore();
            return refreshTask;
        }
        async Task Discover(){
            try{
                var r=await run(new[]{"mdns","services"},6000,null);r.Ensure();
                if(!r.Output.Contains("List of discovered mdns services"))throw new Exception(r.Combined.Trim());
                Services=WirelessService.Parse(r.Output);DiscoveryError="";
            }catch(Exception ex){Services=new List<WirelessService>();DiscoveryError="无线发现暂不可用："+ex.Message;}
        }
        async Task RefreshCore(){
            var discovery=Discover();var deviceList=run(new[]{"devices","-l"},6000,null);
            try{
                await Task.WhenAll(discovery,deviceList);var r=await deviceList;r.Ensure();Devices=Device.Parse(r.Output);
                var current=new HashSet<string>(Devices.Where(d=>d.State=="device").Select(d=>d.Serial+"/"+d.TransportId));
                foreach(string key in identities.Keys.Where(k=>!current.Contains(k)).ToArray())identities.Remove(key);
                bool connected=false;int count=0;
                foreach(var service in Services.Where(s=>!s.Pairing).ToArray()){
                    if(Devices.Any(d=>d.State=="device"&&service.Matches(d.Serial)))continue;
                    if(!peers.Any(p=>p.Services.Contains(service.Name))&&!(pairedHost==service.Host&&DateTime.UtcNow<pairUntil))continue;
                    DateTime last;if(attempts.TryGetValue(service.Address,out last)&&(DateTime.UtcNow-last).TotalSeconds<20)continue;
                    if(count++>=2)break;attempts[service.Address]=DateTime.UtcNow;
                    try{await ConnectCore(service.Address);connected=true;}catch(Exception){}
                }
                foreach(string endpoint in attempts.Where(p=>(DateTime.UtcNow-p.Value).TotalMinutes>5).Select(p=>p.Key).ToArray())attempts.Remove(endpoint);
                if(connected){r=await run(new[]{"devices","-l"},6000,null);r.Ensure();Devices=Device.Parse(r.Output);}
                bool changed=false;
                foreach(var device in Devices.Where(d=>d.State=="device"&&d.Wireless)){
                    string key=device.Serial+"/"+device.TransportId,id;
                    if(String.IsNullOrEmpty(device.TransportId)||!identities.TryGetValue(key,out id)){
                        try{var property=await run(new[]{"-s",device.Serial,"shell","getprop","ro.serialno"},6000,null);property.Ensure();id=property.Output.Trim();
                            if(!Regex.IsMatch(id,@"^[A-Za-z0-9_.-]{1,128}$"))continue;
                            identities[key]=id;
                        }catch(Exception){continue;}
                    }
                    device.HardwareId=id;
                    var peer=peers.FirstOrDefault(p=>p.Id==id);
                    if(peer==null){peer=new WirelessPeer{Id=id,Model=device.Model};peers.Add(peer);changed=true;}
                    if(peer.Model!=device.Model){peer.Model=device.Model;changed=true;}
                    foreach(var s in Services.Where(s=>!s.Pairing&&s.Matches(device.Serial)))if(peer.Services.Add(s.Name))changed=true;
                }
                peersDirty|=changed;
                if(peersDirty){try{SavePeers();peersDirty=false;}catch(Exception ex){DiscoveryError="设备已连接，但自动连接记录保存失败："+ex.Message;}}
            }finally{Publish();}
        }
        void SavePeers(){
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(peerFile)));
            var xml=new XElement("wirelessDevices",peers.Select(p=>new XElement("device",new XAttribute("id",p.Id),new XAttribute("model",p.Model),p.Services.OrderBy(s=>s).Select(s=>new XElement("service",s)))));
            string temp=peerFile+".tmp";xml.Save(temp);if(File.Exists(peerFile))File.Replace(temp,peerFile,null);else File.Move(temp,peerFile);
        }
        public List<NearbyPhone> Nearby(){
            return Services.GroupBy(s=>s.Host).Select(g=>{
                var connect=g.FirstOrDefault(s=>!s.Pairing);var pair=g.FirstOrDefault(s=>s.Pairing&&s.Name.StartsWith("adb-",StringComparison.Ordinal));
                var peer=peers.FirstOrDefault(p=>g.Any(s=>p.Services.Contains(s.Name)));
                var device=Devices.FirstOrDefault(d=>d.State=="device"&&g.Any(s=>s.Matches(d.Serial)));
                return new NearbyPhone{Host=g.Key,Name=device!=null?device.Model:peer!=null?peer.Model:(connect??pair??g.First()).Name,Pair=pair,Connect=connect,Connected=device!=null};
            }).OrderByDescending(p=>p.Connected).ThenBy(p=>p.Name,StringComparer.CurrentCulture).ToList();
        }
        async Task ConnectCore(string address){
            var r=await run(new[]{"connect",address},8000,null);r.Ensure();
            if(!Regex.IsMatch(r.Combined,@"(?im)^(?:already )?connected to "))throw new Exception(r.Combined.Trim());
        }
        public async Task<string> Execute(bool pair,string target,string code=null){
            if(Busy)throw new Exception("正在处理连接，请稍候。");
            if(!Commands.ValidAddress(target))throw new Exception("请输入有效的地址和端口。");
            if(pair&&!Regex.IsMatch(code??"",@"^[0-9]{6}$"))throw new Exception("请输入手机显示的六位配对码。");
            Busy=true;Publish();
            string message;
            try{
                if(refreshTask!=null&&!refreshTask.IsCompleted){try{await refreshTask;}catch(Exception){}}
                if(pair){
                    var r=await run(new[]{"pair",target},20000,code+"\n");r.Ensure();
                    if(!Regex.IsMatch(r.Combined,@"(?i)Successfully paired to "))throw new Exception("配对失败，请检查手机上的配对码和有效期。");
                    pairedHost=target.Substring(0,target.LastIndexOf(':'));pairUntil=DateTime.UtcNow.AddSeconds(60);
                    message="配对成功，正在查找连接服务；请保持无线调试开启。";
                }else{await ConnectCore(target);message="已连接，可以返回应用库打开投屏。";}
                refreshTask=RefreshCore();await refreshTask;
                if(pair&&Devices.Any(d=>d.State=="device"&&Services.Any(s=>!s.Pairing&&s.Host==pairedHost&&s.Matches(d.Serial))))message="配对并连接成功，可以返回应用库打开投屏。";
                if(!pair&&!Devices.Any(d=>d.State=="device"&&(d.Serial==target||Services.Any(s=>!s.Pairing&&s.Address==target&&s.Matches(d.Serial)))))message="ADB 已响应，但手机尚未就绪。请检查授权或稍后重试。";
                return message;
            }catch(Exception ex){throw new Exception(pair&&!String.IsNullOrEmpty(code)?ex.Message.Replace(code,"******"):ex.Message);}
            finally{Busy=false;Publish();}
        }
    }
}
