using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
namespace AppDeck {
    internal static class Program {
        [STAThread] static int Main(string[] args) {
            Directory.CreateDirectory(Paths.Data);
            Environment.SetEnvironmentVariable("ADB",Paths.Adb,EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("APPDECK_INPUT_CONFIG",InputSettings.FilePath,EnvironmentVariableTarget.Process);
            if(args.Contains("--self-test"))return SelfTest();
            if(args.Length==2&&args[0]=="--check-catalog")return CheckCatalog(args[1]);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==2&&args[0]=="--render-ui")return UIPreview.Render(args[1]);
            Theme.Set(Preferences.Load().Dark);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException+=(s,e)=>Theme.Error(null,e.Exception);
            if(args.Contains("--preview-cast")){Theme.Set(args.Contains("--dark"));Application.Run(new CastForm("preview",new AndroidApp{Name="阅读",Package="io.legado.app.release"},"阅读",0,false,false,false,false,true,args.Contains("--landscape")));return 0;}
            Application.Run(new MainForm(args)); return 0;
        }
        static int SelfTest() {
            try {
                var apps=AndroidApp.Parse("[server] INFO: List of apps:\n * 设置                       com.android.settings\n - 阅读                       io.legado.app.release\n - 微信                       com.tencent.mm\n - Fake app                  not-a-package\n");
                if(apps.Count!=3 || !apps[0].System || apps[1].Package!="io.legado.app.release" || !apps[1].Reader)throw new Exception("app parser");
                var favorites=new System.Collections.Generic.HashSet<string>{"com.android.settings","com.tencent.mm"};
                if(AppCatalog.Select(apps,0,"",favorites).Count!=2)throw new Exception("system app exclusion");
                if(AppCatalog.Select(apps,0,"com.android.settings",favorites).Count!=0||AppCatalog.Select(apps,2,"",favorites).Count!=1)throw new Exception("system app search/favorite exclusion");
                if(AppCatalog.Select(apps,0,"COM.TENCENT.MM",favorites).Single().Name!="微信"||AppCatalog.Select(apps,1,"",favorites).Single().Package!="io.legado.app.release")throw new Exception("catalog search/categories");
                var devices=Device.Parse("List of devices attached\nABC device product:x model:OnePlus_15\n192.168.1.1:5555 offline\nXYZ unauthorized\n");
                if(devices.Count!=3 || devices[0].Model!="OnePlus 15" || devices[2].State!="unauthorized")throw new Exception("device parser");
                if(Commands.Quote("a b")!="\"a b\"" || Commands.Quote("a\"b")!="\"a\\\"b\"" || Commands.Quote("C:\\end\\")!="\"C:\\end\\\\\"")throw new Exception("argument escaping");
                if(Commands.ValidAddress("host:0") || Commands.ValidAddress("127.0.0.1:65536") || Commands.ValidAddress("x:123;whoami") || !Commands.ValidAddress("[::1]:5555"))throw new Exception("wireless endpoint validation");
                if(Commands.ValidPackage("com.tencent.mm;id") || !Commands.ValidPackage("com.qidian.QDReader"))throw new Exception("package validation");
                string[] portrait={"1080x1920/420","1080x2160/440","900x1600/360","1440x1920/360"};
                string[] landscape={"1920x1080/420","2160x1080/440","1600x900/360","1920x1440/360"};
                for(int i=0;i<4;i++)if(DisplayProfile.Argument(i,false)!=portrait[i]||DisplayProfile.Argument(i,true)!=landscape[i])throw new Exception("display orientation/preset mapping");
                string stacks="RootTask id=1 bounds=[0,0][1272,2772] displayId=0 userId=0\n  taskId=2: home bounds=[0,0][500,500] visible=true\nRootTask id=10 bounds=[0,0][900,900] displayId=23 userId=0\n  taskId=10: com.tencent.mm/a bounds=[0,0][900,900] visible=true\n  taskId=11: app/a bounds=[0,0][1920,1080] visible=true\n  taskId=12: app/b bounds=[0,0][300,300] visible=false\nRootTask id=99 bounds=[0,0][300,300] displayId=24 userId=0\n  taskId=99: app/c bounds=[0,0][300,300] visible=true";
                if(!DisplayTasks.NeedingResize(stacks,23,new System.Drawing.Size(1920,1080)).SequenceEqual(new[]{10})||DisplayTasks.NeedingResize(stacks,0,new System.Drawing.Size(1920,1080)).Any())throw new Exception("virtual-display task isolation");
                if(!File.Exists(Paths.Scrcpy)||!File.Exists(Paths.Adb)||!File.Exists(Path.Combine(Paths.Runtime,"appdeck-layout.jar")))throw new Exception("runtime missing");
                File.WriteAllText(Path.Combine(Paths.Data,"self-test.txt"),"PASS: app/device parsing, system app exclusion (all/search/favorites), catalog search/categories, argument escaping, endpoint validation, package validation, 8 display size/orientation combinations, virtual-display task isolation (main/other/hidden/already-sized tasks excluded), bundled runtime\r\n",Encoding.UTF8);
                return 0;
            } catch(Exception ex) {File.WriteAllText(Path.Combine(Paths.Data,"self-test.txt"),ex.ToString()); return 1;}
        }
        static int CheckCatalog(string serial){
            try{
                var r=Commands.Run(Paths.Scrcpy,new[]{"-s",serial,"--list-apps"},45000).GetAwaiter().GetResult();r.Ensure();
                var apps=AndroidApp.Parse(r.Combined);if(apps.Count==0)throw new Exception("No launchable apps returned");
                var favorites=new System.Collections.Generic.HashSet<string>{"com.tencent.mm"};
                var all=AppCatalog.Select(apps,0,"",favorites);
                if(all.Count!=apps.Count(a=>!a.System)||all.Any(a=>a.System))throw new Exception("System filtering mismatch");
                var wx=AppCatalog.Select(apps,0,"com.tencent.mm",favorites);if(wx.Count!=1||wx[0].Name!="微信")throw new Exception("WeChat search mismatch");
                string report="PASS: live device catalog\r\nLaunchable packages: "+apps.Count+"\r\nVisible user apps: "+all.Count+"\r\nExcluded system apps: "+apps.Count(a=>a.System)+"\r\nWeChat package search: "+wx.Count+"\r\nReading apps: "+AppCatalog.Select(apps,1,"",favorites).Count+"\r\nSystem package search: "+AppCatalog.Select(apps,0,"com.android.settings",favorites).Count+"\r\n";
                File.WriteAllText(Path.Combine(Paths.Data,"catalog-check.txt"),report,Encoding.UTF8);return 0;
            }catch(Exception ex){File.WriteAllText(Path.Combine(Paths.Data,"catalog-check.txt"),ex.ToString());return 1;}
        }
    }
}
