using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace AppDeck {
    internal sealed class MainForm:ModernForm {
        readonly Preferences prefs=Preferences.Load();
        readonly SelectBox devices=new SelectBox(),preset=new SelectBox(),orientation=new SelectBox();
        readonly Field searchField=new Field("搜索应用名称或包名",true),titleField=new Field("窗口标题");
        TextBox search{get{return searchField.Input;}}TextBox title{get{return titleField.Input;}}
        readonly ToggleRow flex,audio,screenOff,clipboard;
        readonly CatalogView appList=new CatalogView();
        readonly Label status=Theme.Hint("正在查找手机…",8),deviceInfo=Theme.Hint("USB / 无线 ADB",8),catalogCount=Theme.Hint("等待读取应用",8),pageTitle=Theme.Label("应用库",24,true);
        readonly Label selectedName=Theme.Label("选择一个应用",13,true),selectedPackage=Theme.Hint("独立窗口，自在阅读",8);
        readonly AppBadge badge=new AppBadge();
        readonly System.Windows.Forms.Timer deviceWatch=new System.Windows.Forms.Timer{Interval=3000};
        readonly List<CastForm> sessions=new List<CastForm>();readonly List<ModernButton> nav=new List<ModernButton>();
        readonly HashSet<string> openingSessions=new HashSet<string>();
        readonly string[] startupArgs;readonly bool preview;
        List<AndroidApp> apps=new List<AndroidApp>();Button refresh,launch;IconButton favorite,themeButton;int category,loadGeneration;
        bool refreshing,closing,closeRequested,appsLoading;string deviceSignature="";
        public MainForm(string[] args){
            startupArgs=args;preview=args.Contains("--preview");Text="AppDeck";ClientSize=new Size(1220,850);MinimumSize=new Size(1050,740);StartPosition=FormStartPosition.CenterScreen;KeyPreview=true;
            themeButton=Caption.AddAction(Theme.Dark?"sun":"moon","切换亮色 / 暗色",(s,e)=>ToggleTheme());
            Theme.Watch(this,()=>{themeButton.Glyph=Theme.Dark?"sun":"moon";themeButton.Hint(Theme.Dark?"切换到亮色":"切换到暗色");themeButton.Invalidate();});
            var shell=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,174));shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));Body.Controls.Add(shell);
            var sidebar=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(16,24,16,16)};Theme.Watch(sidebar,()=>sidebar.BackColor=Theme.Surface);shell.Controls.Add(sidebar,0,0);
            var navArea=new FlowLayoutPanel{Dock=DockStyle.Top,Height=230,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};sidebar.Controls.Add(navArea);
            string[] labels={"全部应用","小说阅读","我的收藏"},glyphs={"grid","book","star"};for(int i=0;i<3;i++){int index=i;var b=new ModernButton{Text=labels[i],Glyph=glyphs[i],Quiet=true,AlignContentLeft=true,Selected=i==0,Size=new Size(142,44),Margin=new Padding(0,0,0,5)};b.Click+=(s,e)=>SelectCategory(index);nav.Add(b);navArea.Controls.Add(b);}
            var sidebarBottom=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=111,FlowDirection=FlowDirection.TopDown,WrapContents=false};sidebar.Controls.Add(sidebarBottom);
            var help=new ModernButton{Text="使用指南",Glyph="help",Quiet=true,AlignContentLeft=true,Width=142};help.Click+=(s,e)=>ProcessFile("https://github.com/jaydenswork/AppDeck#readme");sidebarBottom.Controls.Add(help);
            var version=Theme.Hint("APPDECK  /  "+AppInfo.Version+"\n手机应用的桌面空间",8);version.Margin=new Padding(14,18,0,0);sidebarBottom.Controls.Add(version);
            var content=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Padding=new Padding(28,24,26,12),Margin=Padding.Empty};
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,74));content.RowStyles.Add(new RowStyle(SizeType.Absolute,68));content.RowStyles.Add(new RowStyle(SizeType.Absolute,58));content.RowStyles.Add(new RowStyle(SizeType.Percent,100));content.RowStyles.Add(new RowStyle(SizeType.Absolute,28));shell.Controls.Add(content,1,0);
            var heading=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};pageTitle.Location=new Point(0,0);heading.Controls.Add(pageTitle);var subtitle=Theme.Hint("让手机应用，拥有自己的窗口。",10);subtitle.Location=new Point(2,47);heading.Controls.Add(subtitle);content.Controls.Add(heading,0,0);
            var connection=new SurfacePanel{Dock=DockStyle.Fill,Margin=new Padding(0,0,0,12),Padding=new Padding(12,10,12,8)};
            var connectionGrid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Margin=Padding.Empty};connectionGrid.RowStyles.Add(new RowStyle(SizeType.Percent,100));connectionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,35));connectionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));connectionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,40));connectionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,104));connection.Controls.Add(connectionGrid);
            var phoneGlyph=new IconButton("phone","连接的 Android 设备"){Dock=DockStyle.Fill,Enabled=false};connectionGrid.Controls.Add(phoneGlyph,0,0);
            devices.Dock=DockStyle.Fill;devices.AccessibleName="选择已连接手机";devices.Margin=new Padding(2,0,12,0);devices.SelectedIndexChanged+=async(s,e)=>{if(!refreshing&&!preview)await LoadApps();};connectionGrid.Controls.Add(devices,1,0);
            refresh=new IconButton("refresh","刷新连接设备"){Dock=DockStyle.Fill};refresh.Click+=async(s,e)=>{if(!preview)await RefreshDevices();};connectionGrid.Controls.Add(refresh,2,0);
            var wireless=new ModernButton{Text="无线连接",Quiet=true,Dock=DockStyle.Fill};wireless.Click+=async(s,e)=>{using(var d=new WirelessDialog())d.ShowDialog(this);if(!preview)await RefreshDevices();};connectionGrid.Controls.Add(wireless,3,0);content.Controls.Add(connection,0,1);
            var searchRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));searchField.Dock=DockStyle.Top;searchField.Height=42;searchRow.Controls.Add(searchField,0,0);catalogCount.Dock=DockStyle.Fill;catalogCount.TextAlign=ContentAlignment.MiddleRight;catalogCount.Margin=new Padding(8,0,0,18);searchRow.Controls.Add(catalogCount,1,0);content.Controls.Add(searchRow,0,2);
            search.TextChanged+=(s,e)=>RenderApps();search.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;LaunchSelected();}else if(e.KeyCode==Keys.Down){e.SuppressKeyPress=true;appList.Focus();}};
            var workspace=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));workspace.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,280));content.Controls.Add(workspace,0,3);
            appList.Dock=DockStyle.Fill;appList.Margin=new Padding(0,0,18,0);appList.SelectionChanged+=(s,e)=>SelectionChanged();appList.OpenRequested+=(s,e)=>LaunchSelected();appList.FavoriteRequested+=(s,e)=>ToggleFavorite();workspace.Controls.Add(appList,0,0);
            var detail=new SurfacePanel{Dock=DockStyle.Fill,Margin=Padding.Empty,Padding=new Padding(16)};workspace.Controls.Add(detail,1,0);
            var detailLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=Padding.Empty};detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,68));detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));detail.Controls.Add(detailLayout);
            var selectedHeader=new Panel{Dock=DockStyle.Fill,Margin=Padding.Empty};badge.Location=new Point(0,4);badge.Size=new Size(43,43);selectedHeader.Controls.Add(badge);selectedName.AutoSize=false;selectedName.SetBounds(55,3,157,25);selectedPackage.AutoSize=false;selectedPackage.SetBounds(55,31,187,29);selectedPackage.AutoEllipsis=true;selectedHeader.Controls.Add(selectedName);selectedHeader.Controls.Add(selectedPackage);favorite=new IconButton("star","收藏 / 取消收藏"){Size=new Size(29,29),Location=new Point(218,0)};favorite.Click+=(s,e)=>ToggleFavorite();selectedHeader.Controls.Add(favorite);detailLayout.Controls.Add(selectedHeader,0,0);
            var optionsScroll=new ScrollArea{Dock=DockStyle.Fill,Margin=Padding.Empty};detailLayout.Controls.Add(optionsScroll,0,1);
            var options=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=Padding.Empty};optionsScroll.Content=options;optionsScroll.Controls.Add(options);
            options.Controls.Add(Theme.Hint("窗口标题",8));titleField.Width=228;titleField.Height=39;title.MaxLength=100;titleField.Margin=new Padding(0,0,0,15);options.Controls.Add(titleField);
            options.Controls.Add(Theme.Hint("显示方向",8));orientation.Items.AddRange(new object[]{"竖屏","横屏"});orientation.SelectedIndex=prefs.Landscape?1:0;orientation.Width=228;orientation.Margin=new Padding(0,0,0,12);options.Controls.Add(orientation);orientation.SelectedIndexChanged+=(s,e)=>UpdatePresets();
            options.Controls.Add(Theme.Hint("画面尺寸",8));preset.Width=228;UpdatePresets();preset.SelectedIndex=Math.Max(0,Math.Min(3,prefs.Preset));preset.Margin=new Padding(0,0,0,13);options.Controls.Add(preset);
            flex=new ToggleRow("自适应排版","开启后跟随窗口，覆盖预设尺寸",prefs.Flex);screenOff=new ToggleRow("手机灭屏","投屏时关闭手机实体屏",prefs.ScreenOff);audio=new ToggleRow("传输音频","同时播放手机媒体声音",prefs.Audio);clipboard=new ToggleRow("剪贴板同步","支持复制和中文粘贴",prefs.Clipboard);
            foreach(var toggle in new[]{flex,screenOff,audio,clipboard}){toggle.Width=228;toggle.Margin=Padding.Empty;options.Controls.Add(toggle);}
            var actions=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=1,ColumnCount=1,Margin=Padding.Empty,Padding=new Padding(0,14,0,0)};actions.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            launch=new ModernButton{Text="打开独立窗口",Font=Theme.Font(10,true),Primary=true,Dock=DockStyle.Fill};launch.Click+=(s,e)=>LaunchSelected();actions.Controls.Add(launch,0,0);detailLayout.Controls.Add(actions,0,2);
            var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,245));status.Dock=DockStyle.Fill;status.AutoSize=false;status.AutoEllipsis=true;status.TextAlign=ContentAlignment.BottomLeft;deviceInfo.Dock=DockStyle.Fill;deviceInfo.AutoSize=false;deviceInfo.AutoEllipsis=true;deviceInfo.TextAlign=ContentAlignment.BottomRight;footer.Controls.Add(status,0,0);footer.Controls.Add(deviceInfo,1,0);content.Controls.Add(footer,0,4);
            Theme.Watch(this,()=>{shell.BackColor=content.BackColor=workspace.BackColor=heading.BackColor=footer.BackColor=Theme.Paper;selectedHeader.BackColor=optionsScroll.BackColor=options.BackColor=detailLayout.BackColor=actions.BackColor=connectionGrid.BackColor=Theme.Surface;});
            KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape&&search.Focused){e.SuppressKeyPress=true;search.Clear();}};
            deviceWatch.Tick+=async(s,e)=>await RefreshDevices(true);
            Shown+=async(s,e)=>{if(preview)return;await RefreshDevices();if(closeRequested||IsDisposed)return;deviceWatch.Start();int ix=Array.IndexOf(startupArgs,"--app");if(ix>=0&&ix+1<startupArgs.Length){var a=apps.FirstOrDefault(x=>x.Package==startupArgs[ix+1]);if(a!=null)Launch(a);}};
            WindowTitles.Changed+=SavedTitleChanged;Disposed+=(s,e)=>WindowTitles.Changed-=SavedTitleChanged;FormClosing+=OnClosing;FormClosed+=(s,e)=>{if(!preview)SaveOptions();};UpdateLaunchButtons();if(preview)LoadPreview();
        }
        void ToggleTheme(){Theme.Set(!Theme.Dark);prefs.Dark=Theme.Dark;if(!preview)Save();}
        void SelectCategory(int value){category=value;string[] names={"应用库","小说阅读","我的收藏"};pageTitle.Text=names[value];for(int i=0;i<nav.Count;i++){nav[i].Selected=i==value;nav[i].Invalidate();}RenderApps();}
        void SelectionChanged(){var a=SelectedApp;badge.App=a;badge.Invalidate();selectedName.Text=a==null?"选择一个应用":a.Name;selectedPackage.Text=a==null?"独立窗口，自在阅读":a.Package;if(a!=null)title.Text=WindowTitles.Get(a.Package,a.Name);if(favorite!=null){favorite.Active=a!=null&&prefs.Favorites.Contains(a.Package);favorite.Enabled=a!=null;favorite.Invalidate();}UpdateLaunchButtons();}
        void SavedTitleChanged(string package){if(!IsDisposed&&SelectedApp!=null&&SelectedApp.Package==package&&!title.Focused)title.Text=WindowTitles.Get(package,SelectedApp.Name);}
        void ToggleFavorite(){var a=SelectedApp;if(a==null)return;if(!prefs.Favorites.Add(a.Package))prefs.Favorites.Remove(a.Package);if(!preview)Save();RenderApps();}
        void UpdatePresets(){int choice=Math.Max(0,preset.SelectedIndex);preset.Items.Clear();for(int i=0;i<4;i++)preset.Items.Add(DisplayProfile.Description(i,orientation.SelectedIndex==1));preset.SelectedIndex=choice;preset.Invalidate();}
        void SaveOptions(){prefs.Preset=preset.SelectedIndex;prefs.Landscape=orientation.SelectedIndex==1;prefs.Flex=flex.Checked;prefs.Audio=audio.Checked;prefs.ScreenOff=screenOff.Checked;prefs.Clipboard=clipboard.Checked;prefs.Dark=Theme.Dark;Save();}
        void LoadPreview(){refreshing=true;devices.Items.Add(new Device{Model="OnePlus 15",Serial="UI PREVIEW",State="device"});devices.SelectedIndex=0;refreshing=false;string[] names={"微信","阅读","QQ 阅读","起点读书","番茄免费小说","书旗小说","七猫免费小说","知乎","哔哩哔哩","网易云音乐","豆瓣","小红书","支付宝","美团","京东","百度地图"};string[] packages={"com.tencent.mm","io.legado.app.release","com.qq.reader","com.qidian.QDReader","com.dragon.read","com.shuqi.controller","com.kmxs.reader","com.zhihu.android","tv.danmaku.bili","com.netease.cloudmusic","com.douban.frodo","com.xingin.xhs","com.eg.android.AlipayGphone","com.sankuai.meituan","com.jingdong.app.mall","com.baidu.BaiduMap"};for(int i=0;i<names.Length;i++)apps.Add(new AndroidApp{Name=names[i],Package=packages[i]});prefs.Favorites.Add("io.legado.app.release");RenderApps();status.Text="界面预览 · 示例应用，不连接手机";deviceInfo.Text="Android 16  ·  USB";}
        internal static void ProcessFile(string path){System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path){UseShellExecute=true});}
        Device SelectedDevice{get{return devices.SelectedItem as Device;}}AndroidApp SelectedApp{get{return appList.SelectedApp;}}
        void Save(){try{prefs.Dark=Theme.Dark;prefs.Save();}catch(Exception ex){status.Text="设置保存失败："+ex.Message;}}
        async Task RefreshDevices(bool automatic=false){
            if(refreshing||closeRequested)return;refreshing=true;refresh.Enabled=false;if(!automatic)status.Text="正在查找设备…";
            try{string selected=SelectedDevice==null?prefs.Serial:SelectedDevice.Serial;var r=await Commands.Run(Paths.Adb,new[]{"devices","-l"});r.Ensure();var list=Device.Parse(r.Output);if(closeRequested||IsDisposed)return;string signature=String.Join("|",list.Select(d=>d.Serial+":"+d.State+":"+d.Model));if(automatic&&signature==deviceSignature)return;deviceSignature=signature;loadGeneration++;devices.Items.Clear();devices.Items.AddRange(list.ToArray());devices.SelectedItem=list.FirstOrDefault(d=>d.Serial==selected)??list.FirstOrDefault(d=>d.State=="device")??list.FirstOrDefault();if(list.Count==0){appsLoading=false;apps.Clear();RenderApps();deviceInfo.Text="等待 USB / 无线 ADB";status.Text="等待手机连接 · 自动检测中";}}
            catch(Exception ex){if(!closeRequested&&!automatic)status.Text="连接检查失败："+ex.Message;}
            finally{refreshing=false;if(!IsDisposed)refresh.Enabled=true;}
            if(closeRequested||IsDisposed)return;if(SelectedDevice!=null)await LoadApps();
        }
        async Task LoadApps(){
            if(closeRequested)return;int generation=++loadGeneration;var device=SelectedDevice;appsLoading=device!=null&&device.State=="device";apps.Clear();RenderApps();if(device==null)return;if(device.State!="device"){deviceInfo.Text=device.State=="unauthorized"?"请在手机上允许 USB 调试。":"设备离线，请重新连接。";status.Text=deviceInfo.Text;return;}
            status.Text="正在读取手机应用…";launch.Enabled=false;
            try{await TaskLease.Recover(device.Serial);var appTask=Commands.Run(Paths.Scrcpy,new[]{"-s",device.Serial,"--list-apps"},45000);var modelTask=Commands.Adb(device.Serial,"shell","getprop","ro.product.model");var verTask=Commands.Adb(device.Serial,"shell","getprop","ro.build.version.release");await Task.WhenAll(appTask,modelTask,verTask);var r=await appTask;r.Ensure();var model=await modelTask;var version=await verTask;if(generation!=loadGeneration||IsDisposed||closeRequested)return;appsLoading=false;apps=AndroidApp.Parse(r.Combined).Where(a=>!a.System).ToList();prefs.Serial=device.Serial;deviceInfo.Text=model.Output.Trim()+"  ·  Android "+version.Output.Trim();RenderApps();status.Text="双击应用卡片打开窗口";}
            catch(Exception ex){if(generation==loadGeneration&&!closeRequested)status.Text="读取失败："+ex.Message;}
            finally{if(generation==loadGeneration&&!IsDisposed&&!closeRequested){appsLoading=false;RenderApps();}}
        }
        void RenderApps(){string selected=SelectedApp==null?prefs.Package:SelectedApp.Package;var view=AppCatalog.Select(apps,category,search.Text,prefs.Favorites);appList.EmptyTitle=appsLoading?"正在读取应用":apps.Count==0?"等待手机连接":"没有找到应用";appList.EmptyDetail=appsLoading?"正在为你的应用准备桌面空间。":apps.Count==0?"连接 USB 或无线 ADB 后，应用会自动出现在这里。":"试试其他关键词，或切换到全部应用。";appList.SetItems(view,prefs.Favorites,selected);catalogCount.Text=appsLoading?"正在读取…":String.Format("{0} / {1} 个应用",view.Count,apps.Count);UpdateLaunchButtons();}
        void UpdateLaunchButtons(){var d=SelectedDevice;bool connected=d!=null&&d.State=="device"&&!appsLoading;if(launch!=null)launch.Enabled=connected&&SelectedApp!=null;}
        void LaunchSelected(){var a=SelectedApp;if(a!=null)Launch(a);}
        async void Launch(AndroidApp app,string captionOverride=null){
            if(preview){status.Text="界面预览模式，不连接手机。";return;}var d=SelectedDevice;if(d==null||d.State!="device")return;string openingKey=d.Serial+"/"+app.Package;if(!openingSessions.Add(openingKey))return;try{string caption=captionOverride??(SelectedApp!=null&&SelectedApp.Package==app.Package&&!String.IsNullOrWhiteSpace(title.Text)?title.Text.Trim():WindowTitles.Get(app.Package,app.Name));WindowTitles.Save(app.Package,caption);var existing=sessions.FirstOrDefault(f=>!f.IsDisposed&&f.Serial==d.Serial&&f.Package==app.Package);if(existing!=null){if(existing.HasDisplayProfile(preset.SelectedIndex,orientation.SelectedIndex==1)){existing.UpdateTitle(caption,false);existing.RestoreFromTray();return;}await existing.StopAndClose();if(!existing.IsDisposed){status.Text="原任务尚未归还，请先恢复手机连接。";return;}if(IsDisposed||closeRequested)return;}if(!Commands.ValidPackage(app.Package)){status.Text="无效应用包名";return;}
            prefs.Package=app.Package;SaveOptions();var form=new CastForm(d.Serial,app,caption,prefs.Preset,flex.Checked,audio.Checked,screenOff.Checked,clipboard.Checked,landscape:prefs.Landscape);sessions.Add(form);form.FormClosed+=(s,e)=>sessions.Remove(form);form.Show();status.Text="已打开 "+app.Name+" · 可继续选择其他应用";
            }catch(Exception ex){if(!IsDisposed)status.Text="打开失败："+ex.Message;}finally{openingSessions.Remove(openingKey);}
        }
        async void OnClosing(object sender,FormClosingEventArgs e){if(closing)return;e.Cancel=true;if(closeRequested)return;closeRequested=true;deviceWatch.Stop();loadGeneration++;Enabled=false;await Task.Yield();foreach(var f in sessions.ToArray()){await f.StopAndClose();if(!f.IsDisposed){closeRequested=false;Enabled=true;deviceWatch.Start();status.Text="投屏任务尚未归还，暂未退出。";return;}}deviceWatch.Dispose();closing=true;Close();}
    }
    internal static class NativeHint {
        [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr h,uint m,IntPtr w,string l);
        public static void Set(TextBox box,string text){box.HandleCreated+=(s,e)=>SendMessage(box.Handle,0x1501,new IntPtr(1),text);}
    }
    internal sealed class WirelessDialog:ModernForm {
        readonly Field address=new Field("例如 192.168.1.10:37521"),pairAddress=new Field("配对地址（端口与连接地址通常不同）"),code=new Field("六位配对码");readonly Label status=Theme.Hint("手机与电脑需位于同一网络。",9);
        public WirelessDialog(){Text="无线连接";ClientSize=new Size(510,560);MinimumSize=new Size(510,560);StartPosition=FormStartPosition.CenterParent;var p=new FlowLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(26),FlowDirection=FlowDirection.TopDown,WrapContents=false};Body.Controls.Add(p);p.Controls.Add(Theme.Label("摆脱连接线",20,true));p.Controls.Add(Theme.Hint("在手机开发者选项中打开无线调试。"));p.Controls.Add(Theme.Label("连接地址",9));address.Width=438;p.Controls.Add(address);var connect=Theme.Button("连接手机",async(s,e)=>await Execute(false),true);connect.Margin=new Padding(0,10,0,24);p.Controls.Add(connect);p.Controls.Add(Theme.Label("首次使用 · 先通过配对码配对",10,true));pairAddress.Width=438;pairAddress.Margin=new Padding(0,0,0,10);p.Controls.Add(pairAddress);code.Width=180;code.Input.UseSystemPasswordChar=true;code.Input.MaxLength=6;p.Controls.Add(code);var pair=Theme.Button("配对",async(s,e)=>await Execute(true));pair.Margin=new Padding(0,10,0,12);p.Controls.Add(pair);status.MaximumSize=new Size(438,0);p.Controls.Add(status);}
        async Task Execute(bool pair){string target=(pair?pairAddress.Input.Text:address.Input.Text).Trim();if(!Commands.ValidAddress(target)){status.Text="请输入有效的 地址:端口。";return;}if(pair&&!System.Text.RegularExpressions.Regex.IsMatch(code.Input.Text,@"^\d{6}$")){status.Text="请输入六位配对码。";return;}Enabled=false;try{var r=await Commands.Run(Paths.Adb,pair?new[]{"pair",target,code.Input.Text}:new[]{"connect",target},20000);status.Text=r.Combined.Trim();if(pair)code.Input.Clear();}catch(Exception ex){status.Text=ex.Message;}finally{Enabled=true;}}
    }
}
