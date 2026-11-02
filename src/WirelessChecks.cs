using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace AppDeck {
    internal static class WirelessChecks {
        static void Check(bool condition,string name){if(!condition)throw new Exception("wireless: "+name);}
        public static void Run(){RunAsync().GetAwaiter().GetResult();}
        static async Task RunAsync(){
            string file=Path.Combine(Paths.Data,"wireless-check-"+Guid.NewGuid().ToString("N")+".xml");
            try{
                var adb=new FakeAdb();var monitor=new WirelessMonitor(file,adb.Run);
                await monitor.Refresh();var phones=monitor.Nearby();
                Check(phones.Count==1&&phones[0].Pair.Address==adb.PairAddress&&phones[0].Connect.Address==adb.Address,"separate pairing and connection ports");
                Check(adb.ConnectCalls==0,"unknown devices are not connected automatically");
                string result=await monitor.Execute(true,adb.PairAddress,"123456");
                Check(result.Contains("配对并连接成功")&&adb.ConnectCalls==1,"pair then connect current endpoint");
                Check(adb.PairArguments.SequenceEqual(new[]{"pair",adb.PairAddress})&&adb.PairInput=="123456\n","pairing code uses stdin");
                Check(File.Exists(file),"peer record saved: "+monitor.DiscoveryError);
                Check(monitor.Devices.Single().HardwareId=="PHONE"&&!File.ReadAllText(file).Contains("123456"),"verified identity saved without code");
                Check(!monitor.Busy,"operation releases busy state");
                adb.Connected=false;adb.Address="192.168.1.20:42222";adb.Transport="2";
                var restarted=new WirelessMonitor(file,adb.Run);await restarted.Refresh();
                Check(adb.ConnectCalls==2&&adb.LastConnection==adb.Address,"remembered service follows changed IP and port");
                var device=restarted.Devices.Single();
                Check(device.SamePhone("192.168.1.10:41111","PHONE")&&!device.SamePhone(device.Serial,"OTHER"),"session matching uses verified hardware identity");
                adb.Connected=false;adb.FailConnect=true;await restarted.Refresh();int attempts=adb.ConnectCalls;
                await restarted.Refresh();Check(adb.ConnectCalls==attempts,"failed automatic reconnect is throttled");
                bool failed=false;try{await restarted.Execute(false,adb.Address);}catch(Exception){failed=true;}
                Check(failed&&!restarted.Busy,"exit-code-zero connection failure is rejected");
                adb.FailPair=true;failed=false;try{await restarted.Execute(true,adb.PairAddress,"123456");}catch(Exception ex){failed=!ex.Message.Contains("123456");}
                Check(failed,"exit-code-zero pairing failure is rejected and code redacted");
                adb.FailPair=false;adb.FailConnect=false;adb.DiscoveryFails=true;await restarted.Refresh();
                Check(restarted.Services.Count==0&&restarted.DiscoveryError.Length>0,"failed discovery discards stale endpoints");
                var lease=new XElement("lease",new XAttribute("serial","192.168.1.10:41111"),new XAttribute("deviceId","PHONE"));
                Check(TaskLease.MatchesDevice(lease,adb.Address,"PHONE")&&!TaskLease.MatchesDevice(lease,adb.Address,"OTHER")&&!TaskLease.MatchesDevice(lease,adb.Address,""),"journal recovery follows identity and rejects unrelated devices");
                var legacy=new XElement("lease",new XAttribute("serial","USB"));
                Check(TaskLease.MatchesDevice(legacy,"USB","")&&!TaskLease.MatchesDevice(legacy,"OTHER","USB"),"legacy journals retain exact transport matching");
                string lines="List of discovered mdns services\nadb-A _adb-tls-connect._tcp. [::1]:41234\nadb-A _adb-tls-connect._tcp. [::1]:41234\nadb-B _adb-tls-pairing._tcp 1.2.3.4:0\nadb-C _adb._tcp 1.2.3.4:5555\nbad input\n";
                var services=WirelessService.Parse(lines);Check(services.Count==1&&services[0].Host=="[::1]"&&services[0].Matches("adb-A._adb-tls-connect._tcp."),"IPv6, service serials, duplicate and malformed advertisements");
                adb.DiscoveryFails=false;adb.DeviceFails=true;var recovered=new WirelessMonitor(file,adb.Run);
                try{await recovered.Refresh();}catch(Exception){}
                adb.DeviceFails=false;await recovered.Execute(false,adb.Address);
                Check(recovered.Devices.Count==1,"manual connection can recover after a failed background poll");
                var gate=new TaskCompletionSource<Result>();int scans=0;
                var coalesced=new WirelessMonitor(file,(args,timeout,input)=>{if(args[0]=="mdns"){scans++;return gate.Task;}return Task.FromResult(new Result{Code=0,Output="List of devices attached\n",Error=""});});
                var first=coalesced.Refresh();var second=coalesced.Refresh();Check(Object.ReferenceEquals(first,second)&&scans==1,"overlapping discovery requests are coalesced");
                gate.SetResult(new Result{Code=0,Output="List of discovered mdns services\n",Error=""});await first;
            }finally{if(File.Exists(file))File.Delete(file);if(File.Exists(file+".tmp"))File.Delete(file+".tmp");}
        }
        sealed class FakeAdb {
            public string Address="192.168.1.10:41111",PairAddress="192.168.1.10:51111",Transport="1",LastConnection,PairInput;
            public string[] PairArguments;
            public bool Connected,FailConnect,FailPair,DiscoveryFails,DeviceFails;
            public int ConnectCalls;
            public Task<Result> Run(string[] args,int timeout,string input){
                string output="";int code=0;
                if(args[0]=="mdns"){
                    if(DiscoveryFails){code=1;output="mDNS unavailable";}
                    else output="List of discovered mdns services\nadb-PHONE-guid _adb-tls-connect._tcp "+Address+"\nadb-PHONE-pair _adb-tls-pairing._tcp "+PairAddress+"\n";
                }else if(args[0]=="devices"){if(DeviceFails){code=1;output="ADB server unavailable";}else output="List of devices attached\n"+(Connected?Address+" device model:OnePlus_15 transport_id:"+Transport+"\n":"");}
                else if(args[0]=="pair"){PairArguments=args;PairInput=input;output=FailPair?"Failed to pair 123456":"Successfully paired to "+args[1];}
                else if(args[0]=="connect"){
                    ConnectCalls++;LastConnection=args[1];
                    Check(args[1]==Address,"connection command never uses pairing port");
                    if(FailConnect)output="failed to connect to "+args[1];else{Connected=true;output="connected to "+args[1];}
                }else if(args[0]=="-s")output="PHONE\n";
                else throw new Exception("Unexpected mock ADB command");
                return Task.FromResult(new Result{Code=code,Output=output,Error=""});
            }
        }
    }
}
