using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppDeck {
    internal sealed class WirelessDialog:ModernForm {
        readonly WirelessMonitor monitor;
        readonly SelectBox phones=new SelectBox{Width=552,Height=40,EmptyText="尚未发现手机"};
        readonly Field address=new Field("连接地址，例如 192.168.1.10:37521"),pairAddress=new Field("配对地址，端口与连接地址不同"),code=new Field("六位配对码");
        readonly Label discovery=Theme.Hint("正在查找局域网内开启无线调试的手机…",9),status=Theme.Hint("配对有效时将自动连接，地址和端口变化后会重新发现。",9);
        readonly Button connect,pair,refresh,manualConnect,manualPair;
        bool updating;string phoneSignature="";NearbyPhone selectedPhone;
        public WirelessDialog(WirelessMonitor monitor=null){
            this.monitor=monitor;Text="无线连接";ClientSize=new Size(610,730);MinimumSize=MaximumSize=Size;MaximizeBox=false;StartPosition=FormStartPosition.CenterParent;
            var p=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(26,20,26,12),FlowDirection=FlowDirection.TopDown,WrapContents=false};Body.Controls.Add(p);
            p.Controls.Add(Theme.Label("发现你的手机",20,true));
            var hint=Theme.Hint("手机和电脑需在可互通的局域网内。\n在手机开发者选项中开启“无线调试”。",9);hint.Margin=new Padding(0,8,0,18);p.Controls.Add(hint);
            p.Controls.Add(Theme.Label("附近的手机",10,true));phones.Margin=new Padding(0,8,0,8);phones.AccessibleName="发现的无线调试手机";p.Controls.Add(phones);phones.SelectedIndexChanged+=(s,e)=>{if(!updating){selectedPhone=phones.SelectedItem as NearbyPhone;UpdateSelection();}};
            discovery.AutoSize=false;discovery.Size=new Size(552,56);p.Controls.Add(discovery);
            var actions=Row();connect=Theme.Button("连接所选手机",async(s,e)=>await Execute(false,false),true);refresh=Theme.Button("刷新发现",async(s,e)=>await RefreshDiscovery());actions.Controls.Add(connect);actions.Controls.Add(refresh);p.Controls.Add(actions);
            var first=Theme.Label("首次使用 · 输入手机上的配对码",10,true);first.Margin=new Padding(0,20,0,8);p.Controls.Add(first);
            var pairingHint=Theme.Hint("在手机点“使用配对码配对设备”，再选择上方对应手机。",9);pairingHint.Margin=new Padding(0,0,0,8);p.Controls.Add(pairingHint);
            var pairRow=Row();code.Width=200;code.Input.MaxLength=6;code.Input.UseSystemPasswordChar=true;code.AccessibleName="无线调试配对码";pairRow.Controls.Add(code);pair=Theme.Button("配对并连接",async(s,e)=>await Execute(true,false),true);pairRow.Controls.Add(pair);p.Controls.Add(pairRow);
            var manual=Theme.Label("无法发现？手动填写地址",10,true);manual.Margin=new Padding(0,22,0,10);p.Controls.Add(manual);
            var connectRow=Row();address.Width=392;connectRow.Controls.Add(address);manualConnect=Theme.Button("手动连接",async(s,e)=>await Execute(false,true));connectRow.Controls.Add(manualConnect);p.Controls.Add(connectRow);
            var manualRow=Row();pairAddress.Width=392;manualRow.Controls.Add(pairAddress);manualPair=Theme.Button("手动配对",async(s,e)=>await Execute(true,true));manualRow.Controls.Add(manualPair);p.Controls.Add(manualRow);
            var note=Theme.Hint("手动配对使用上方六位配对码；配对后会尝试自动连接。",8);note.Margin=new Padding(0,6,0,12);p.Controls.Add(note);
            status.AutoSize=false;status.Size=new Size(552,72);p.Controls.Add(status);
            code.Input.KeyDown+=async(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Execute(true,false);}};
            if(monitor!=null){monitor.Changed+=MonitorChanged;Disposed+=(s,e)=>monitor.Changed-=MonitorChanged;Shown+=async(s,e)=>await RefreshDiscovery();}
            UpdateSelection();
        }
        static FlowLayoutPanel Row(){return new FlowLayoutPanel{Width=552,Height=46,Margin=new Padding(0,0,0,6),WrapContents=false};}
        void MonitorChanged(object sender,EventArgs e){if(!IsDisposed&&!Disposing)UpdatePhones();}
        void UpdatePhones(){
            var nearby=monitor.Nearby();string signature=String.Join("|",nearby.Select(p=>p.ToString()+"/"+(p.Pair==null?"":p.Pair.Name+"/"+p.Pair.Address)+"/"+(p.Connect==null?"":p.Connect.Name+"/"+p.Connect.Address)));
            if(signature!=phoneSignature){
                var previous=phones.SelectedItem as NearbyPhone??selectedPhone;string selected=previous==null?null:previous.Host;
                updating=true;phones.SelectedIndex=-1;phones.Items.Clear();phones.Items.AddRange(nearby.Cast<object>());
                phones.SelectedItem=previous==null?nearby.FirstOrDefault():nearby.FirstOrDefault(p=>p.Host==selected||previous.Connect!=null&&p.Connect!=null&&previous.Connect.Name==p.Connect.Name||previous.Pair!=null&&p.Pair!=null&&previous.Pair.Name==p.Pair.Name);
                selectedPhone=phones.SelectedItem as NearbyPhone??previous;updating=false;phoneSignature=signature;phones.Invalidate();
            }
            discovery.Text=monitor.DiscoveryError.Length>0?monitor.DiscoveryError:phones.Items.Count==0?"尚未发现手机。请确认无线调试已开启；公司网络、VPN 或防火墙可能阻止局域网发现。":"已发现 "+phones.Items.Count+" 台手机。首次配对需在手机打开配对码页面。";
            UpdateSelection();
        }
        void UpdateSelection(){
            bool ready=monitor!=null&&!monitor.Busy;var phone=phones.SelectedItem as NearbyPhone;
            phones.Enabled=ready;refresh.Enabled=ready;code.Enabled=ready;address.Enabled=ready;pairAddress.Enabled=ready;
            connect.Enabled=ready&&phone!=null&&phone.Connect!=null;pair.Enabled=ready&&phone!=null&&phone.Pair!=null;
            manualConnect.Enabled=manualPair.Enabled=ready;
        }
        async Task RefreshDiscovery(){try{if(monitor!=null)await monitor.Refresh();}catch(Exception ex){if(!IsDisposed)discovery.Text="发现失败："+ex.Message;}}
        async Task Execute(bool pairing,bool manual){
            if(monitor==null||monitor.Busy)return;
            var phone=phones.SelectedItem as NearbyPhone;var service=phone==null?null:pairing?phone.Pair:phone.Connect;
            if(!manual&&service==null){status.Text=pairing?"请先在手机打开配对码页面，再刷新并选择手机。":"请选择一台可连接的手机。";return;}
            string target=manual?(pairing?pairAddress.Input.Text:address.Input.Text).Trim():service.Address;
            string pairingCode=pairing?code.Input.Text.Trim():null;
            status.Text=pairing?"正在配对，请保持手机配对码页面打开…":"正在连接手机…";
            try{string message=await monitor.Execute(pairing,target,pairingCode);if(!IsDisposed)status.Text=message;}
            catch(Exception ex){if(!IsDisposed)status.Text=ex.Message;}
            finally{if(!IsDisposed){if(pairing)code.Input.Clear();UpdateSelection();}}
        }
    }
}
