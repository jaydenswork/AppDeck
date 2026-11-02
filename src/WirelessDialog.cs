using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AppDeck {
    internal sealed class WirelessDialog:ModernForm {
        readonly WirelessMonitor monitor;
        readonly SelectBox phones=new SelectBox{Width=528,Height=40,EmptyText="正在查找手机…"};
        readonly Field address=new Field("连接地址，例如 192.168.1.10:37521"),pairAddress=new Field("配对地址，端口与连接地址不同"),code=new Field("六位配对码");
        readonly Label discovery=Theme.Hint("手机和电脑需在可互通的局域网内。",9),status=Theme.Hint("已配对手机会自动连接，无需记住地址和端口。",9);
        readonly FlowLayoutPanel content=new BufferedFlowPanel{Dock=DockStyle.Fill,Padding=new Padding(24,20,24,16),FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true};
        readonly Panel pairGroup=new Panel{Width=528,Height=140,Margin=new Padding(0,16,0,0)},manualGroup=new Panel{Width=528,Height=148,Visible=false,Margin=Padding.Empty};
        readonly ModernButton connect,pair,refresh,manualConnect,manualPair,manualToggle;
        bool updating,manualExpanded,previewState,arranging;int layoutState=-1;string phoneSignature="";NearbyPhone selectedPhone;
        public WirelessDialog(WirelessMonitor monitor=null){
            this.monitor=monitor;Text="无线连接";Size=new Size(608,430);MinimumSize=new Size(608,400);MaximumSize=new Size(608,Int16.MaxValue);MaximizeBox=false;Caption.SetWindowButtons(false,false);StartPosition=FormStartPosition.CenterParent;Body.Controls.Add(content);
            var heading=Theme.Label("连接你的手机",18,true);heading.Margin=new Padding(0,0,0,8);content.Controls.Add(heading);
            var hint=Theme.Hint("在手机开发者选项中开启“无线调试”。",10.5f);hint.Margin=new Padding(0,0,0,20);content.Controls.Add(hint);
            phones.Margin=new Padding(0,0,0,8);phones.AccessibleName="发现的无线调试手机";content.Controls.Add(phones);phones.SelectedIndexChanged+=(s,e)=>{if(!updating){selectedPhone=phones.SelectedItem as NearbyPhone;UpdateSelection();}};
            discovery.AutoSize=false;discovery.Size=new Size(528,44);discovery.Margin=new Padding(0,0,0,12);content.Controls.Add(discovery);
            var actions=Row();connect=new ModernButton{Text="连接所选手机",Primary=true,Width=144};connect.Click+=async(s,e)=>await Execute(false,false);refresh=new ModernButton{Text="刷新发现",Width=112,Margin=new Padding(8,0,0,0)};refresh.Click+=async(s,e)=>await RefreshDiscovery();actions.Controls.Add(connect);actions.Controls.Add(refresh);content.Controls.Add(actions);
            var first=Theme.Label("首次使用 · 配对这台手机",10.5f,true);first.Location=new Point(0,0);pairGroup.Controls.Add(first);
            var pairingHint=Theme.Hint("在手机点“使用配对码配对设备”，\n再输入手机显示的六位配对码。",9);pairingHint.Location=new Point(0,30);pairGroup.Controls.Add(pairingHint);
            code.SetBounds(0,84,216,40);code.Input.MaxLength=6;code.Input.UseSystemPasswordChar=true;code.AccessibleName="无线调试配对码";pairGroup.Controls.Add(code);
            pair=new ModernButton{Text="配对并连接",Primary=true,Location=new Point(224,84),Size=new Size(144,40)};pair.Click+=async(s,e)=>await Execute(true,false);pairGroup.Controls.Add(pair);content.Controls.Add(pairGroup);
            manualToggle=new ModernButton{Text="无法发现？手动填写地址",Quiet=true,AlignContentLeft=true,Glyph="chevron",Size=new Size(528,36),Margin=new Padding(0,12,0,8)};manualToggle.Click+=(s,e)=>{manualExpanded=!manualExpanded;UpdateSections();};content.Controls.Add(manualToggle);
            address.SetBounds(0,0,368,40);manualGroup.Controls.Add(address);manualConnect=new ModernButton{Text="手动连接",Location=new Point(376,0),Size=new Size(144,40)};manualConnect.Click+=async(s,e)=>await Execute(false,true);manualGroup.Controls.Add(manualConnect);
            pairAddress.SetBounds(0,48,368,40);manualGroup.Controls.Add(pairAddress);manualPair=new ModernButton{Text="手动配对",Location=new Point(376,48),Size=new Size(144,40)};manualPair.Click+=async(s,e)=>await Execute(true,true);manualGroup.Controls.Add(manualPair);
            var note=Theme.Hint("手动配对使用上方配对码。\n配对地址与连接地址的端口通常不同。",9);note.Location=new Point(0,100);manualGroup.Controls.Add(note);content.Controls.Add(manualGroup);
            status.AutoSize=false;status.Size=new Size(528,44);status.Margin=new Padding(0,8,0,0);content.Controls.Add(status);
            Theme.Watch(this,()=>content.BackColor=pairGroup.BackColor=manualGroup.BackColor=Theme.Paper);
            code.Input.KeyDown+=async(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Execute(true,manualExpanded&&!String.IsNullOrWhiteSpace(pairAddress.Input.Text));}};
            if(monitor!=null){monitor.Changed+=MonitorChanged;Disposed+=(s,e)=>monitor.Changed-=MonitorChanged;Shown+=async(s,e)=>await RefreshDiscovery();}
            UpdateSelection();
        }
        static FlowLayoutPanel Row(){return new FlowLayoutPanel{Width=528,Height=40,Margin=Padding.Empty,WrapContents=false};}
        void UpdateSections(){
            var phone=phones.SelectedItem as NearbyPhone??selectedPhone;bool needsPairing=phone!=null&&!phone.Connected&&phone.Pair!=null,showPair=manualExpanded||needsPairing;
            int next=DeviceDpi*8+(manualExpanded?1:0)+(needsPairing?2:0);if(arranging||next==layoutState)return;
            arranging=true;SuspendLayout();Body.SuspendLayout();content.SuspendLayout();
            try{
                pairGroup.Visible=showPair;manualGroup.Visible=manualExpanded;manualToggle.Text=manualExpanded?"收起手动连接":"无法发现？手动填写地址";
                connect.Visible=!needsPairing;refresh.Margin=needsPairing?Padding.Empty:new Padding(8,0,0,0);pair.Visible=needsPairing;pair.Primary=!manualExpanded;
                pairGroup.Controls[0].Text=needsPairing?"首次使用 · 配对这台手机":"手动配对 · 六位配对码";
                int height=content.Padding.Vertical+Caption.Height+Padding.Vertical;
                foreach(Control control in content.Controls)if(control!=pairGroup&&control!=manualGroup)height+=control.Height+control.Margin.Vertical;
                if(showPair)height+=pairGroup.Height+pairGroup.Margin.Vertical;if(manualExpanded)height+=manualGroup.Height+manualGroup.Margin.Vertical;
                int maxHeight=Math.Max(MinimumSize.Height,Screen.PrimaryScreen.WorkingArea.Height-48),target=Math.Min(maxHeight,Math.Max(MinimumSize.Height,height));
                // Use outer height for this custom frame; avoid client/non-client conversions
                // and repeated sizing work on discovery notifications.
                if(Height!=target)Height=target;layoutState=next;
            }finally{content.ResumeLayout(true);Body.ResumeLayout(true);ResumeLayout(true);arranging=false;}
            Invalidate(true);
        }
        protected override void OnLoad(EventArgs e){base.OnLoad(e);layoutState=-1;UpdateSections();}
        protected override void OnPaintBackground(PaintEventArgs e){e.Graphics.Clear(Theme.Surface);}
        protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(IsHandleCreated&&!arranging)Invalidate(true);}
        protected override void OnDpiChanged(DpiChangedEventArgs e){base.OnDpiChanged(e);if(IsHandleCreated)BeginInvoke((Action)(()=>{if(!IsDisposed){layoutState=-1;UpdateSections();}}));}
        void MonitorChanged(object sender,EventArgs e){if(!IsDisposed&&!Disposing)UpdatePhones();}
        void UpdatePhones(){
            var nearby=monitor.Nearby();string signature=String.Join("|",nearby.Select(p=>p.ToString()+"/"+(p.Pair==null?"":p.Pair.Name+"/"+p.Pair.Address)+"/"+(p.Connect==null?"":p.Connect.Name+"/"+p.Connect.Address)));
            if(signature!=phoneSignature){var previous=phones.SelectedItem as NearbyPhone??selectedPhone;string selected=previous==null?null:previous.Host;updating=true;phones.SelectedIndex=-1;phones.Items.Clear();phones.Items.AddRange(nearby.Cast<object>());phones.SelectedItem=previous==null?nearby.FirstOrDefault():nearby.FirstOrDefault(p=>p.Host==selected||previous.Connect!=null&&p.Connect!=null&&previous.Connect.Name==p.Connect.Name||previous.Pair!=null&&p.Pair!=null&&previous.Pair.Name==p.Pair.Name);selectedPhone=phones.SelectedItem as NearbyPhone??previous;updating=false;phoneSignature=signature;phones.Invalidate();}
            phones.EmptyText="尚未发现手机";
            discovery.Text=monitor.DiscoveryError.Length>0?monitor.DiscoveryError:phones.Items.Count==0?"尚未发现手机。请确认无线调试已开启，\n并检查局域网、VPN 或防火墙是否允许设备发现。":"已发现 "+phones.Items.Count+" 台手机。";
            UpdateSelection();
        }
        void UpdateSelection(){
            bool ready=previewState||monitor!=null&&!monitor.Busy;var phone=phones.SelectedItem as NearbyPhone;
            phones.Enabled=ready;refresh.Enabled=ready;code.Enabled=ready;address.Enabled=ready;pairAddress.Enabled=ready;
            connect.Enabled=ready&&phone!=null&&phone.Connect!=null&&!phone.Connected;connect.Text=phone!=null&&phone.Connected?"已连接":"连接所选手机";
            pair.Enabled=ready&&phone!=null&&phone.Pair!=null;manualConnect.Enabled=manualPair.Enabled=ready;
            if(phone!=null)discovery.Text=phone.Connected?"手机已连接。返回应用库即可打开应用。":phone.Pair!=null?"已找到配对服务，请输入手机显示的配对码。":"手机可连接；如尚未配对，请在手机打开配对码页面。";
            UpdateSections();
        }
        internal void Preview(bool pairing,bool manual){previewState=true;updating=true;var phone=new NearbyPhone{Host="192.168.1.10",Name="OnePlus 15",Connected=!pairing,Connect=new WirelessService{Name="preview",Type="_adb-tls-connect._tcp",Address="192.168.1.10:37521"},Pair=pairing?new WirelessService{Name="preview-pair",Type="_adb-tls-pairing._tcp",Address="192.168.1.10:38413"}:null};phones.Items.Add(phone);phones.SelectedIndex=0;manualExpanded=manual;updating=false;status.Text="界面预览 · 示例设备，不连接手机。";UpdateSelection();}
        async Task RefreshDiscovery(){try{if(monitor!=null)await monitor.Refresh();}catch(Exception ex){if(!IsDisposed)discovery.Text="发现失败："+ex.Message;}}
        async Task Execute(bool pairing,bool manual){
            if(monitor==null||monitor.Busy)return;var phone=phones.SelectedItem as NearbyPhone;var service=phone==null?null:pairing?phone.Pair:phone.Connect;
            if(!manual&&service==null){status.Text=pairing?"请先在手机打开配对码页面，再刷新并选择手机。":"请选择一台可连接的手机。";return;}
            string target=manual?(pairing?pairAddress.Input.Text:address.Input.Text).Trim():service.Address;string pairingCode=pairing?code.Input.Text.Trim():null;
            status.Text=pairing?"正在配对，请保持手机配对码页面打开…":"正在连接手机…";
            try{string message=await monitor.Execute(pairing,target,pairingCode);if(!IsDisposed)status.Text=message;}
            catch(Exception ex){if(!IsDisposed)status.Text=ex.Message;}
            finally{if(!IsDisposed){if(pairing)code.Input.Clear();UpdateSelection();}}
        }
    }
}
