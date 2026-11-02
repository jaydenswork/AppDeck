using System;
using System.Drawing;
using System.IO;
using System.Linq;

namespace AppDeck {
    internal static class DeviceCacheChecks {
        static void Check(bool ok,string name){if(!ok)throw new Exception("device cache: "+name);}
        public static void Run(){
            string tempBase=Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            string root=Path.Combine(tempBase,"appdeck-cache-check-"+Guid.NewGuid().ToString("N"));
            try{
                var wifi=new Device{Serial="192.168.1.10:32123",HardwareId="phone-a"};var changed=new Device{Serial="192.168.1.11:39999",HardwareId="phone-a"};var usb=new Device{Serial="phone-a",HardwareId=""};var other=new Device{Serial=wifi.Serial,HardwareId="phone-b"};
                Check(DeviceCache.Folder(wifi,root)==DeviceCache.Folder(changed,root)&&DeviceCache.Folder(wifi,root)==DeviceCache.Folder(usb,root),"USB, wireless and changed endpoints share a stable phone identity");
                var catalog=new[]{new AndroidApp{Name="阅读 & 测试",Package="io.legado.app.release"},new AndroidApp{Name="系统应用",Package="android.system",System=true},new AndroidApp{Name="无效",Package="../invalid"},new AndroidApp{Name="重复",Package="io.legado.app.release"}};
                Check(DeviceCache.SaveCatalog(wifi,catalog,root),"catalog saved");var loaded=DeviceCache.ReadCatalog(changed,root);Check(loaded.Count==1&&loaded[0].Name==catalog[0].Name,"persistent catalog round-trip and user-app filtering");
                Check(DeviceCache.ReadCatalog(other,root).Count==0,"phones do not share catalogs");
                string folder=DeviceCache.Folder(wifi,root),icon=Path.Combine(folder,loaded[0].Package+".png");using(var bitmap=new Bitmap(192,192))bitmap.Save(icon,System.Drawing.Imaging.ImageFormat.Png);
                Check(AppIcons.PendingPackages(wifi,loaded,root).Count==0,"fresh disk icons require no phone transfer after restart");
                File.SetLastWriteTimeUtc(icon,DateTime.UtcNow.AddDays(-8));Check(AppIcons.PendingPackages(wifi,loaded,root).Count==1,"only expired icons are refreshed");
                File.Delete(icon);Check(AppIcons.PendingPackages(wifi,loaded,root).Count==1,"missing icons are fetched");
                Check(DeviceCache.SaveCatalog(wifi,new AndroidApp[0],root)&&DeviceCache.ReadCatalog(wifi,root).Count==0,"sync removes uninstalled apps");
                File.WriteAllText(Path.Combine(folder,"catalog.xml"),"<broken");Check(DeviceCache.ReadCatalog(wifi,root).Count==0,"corrupt metadata falls back to a phone refresh");
            }finally{
                string target=Path.GetFullPath(root);if(target.StartsWith(tempBase,StringComparison.OrdinalIgnoreCase)&&Path.GetFileName(target).StartsWith("appdeck-cache-check-",StringComparison.Ordinal)&&Directory.Exists(target))Directory.Delete(target,true);
            }
        }
    }
}
