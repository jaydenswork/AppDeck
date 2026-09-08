using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;

namespace AppDeck {
    internal static class Paths {
        public static readonly string Root = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly bool Installed = File.Exists(Path.Combine(Root, "installed.flag"));
        public static readonly string Data = Installed
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AppDeck")
            : Path.Combine(Root, "data");
        public static readonly string Runtime = Path.Combine(Root, "runtime", "scrcpy");
        public static readonly string Scrcpy = Path.Combine(Runtime, "scrcpy-appdeck.exe");
        public static string Adb {
            get {
                string explicitPath = Environment.GetEnvironmentVariable("ADB");
                if (!String.IsNullOrEmpty(explicitPath) && File.Exists(explicitPath)) return explicitPath;
                foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')) {
                    try { var path = Path.Combine(dir.Trim('"'), "adb.exe"); if (File.Exists(path)) return path; } catch { }
                }
                return Path.Combine(Runtime, "adb.exe");
            }
        }
    }
    internal sealed class Result {
        public int Code; public string Output; public string Error;
        public string Combined { get { return Output + "\n" + Error; } }
        public void Ensure() { if (Code != 0) throw new Exception(Combined.Trim()); }
    }
    internal static class Commands {
        // Windows CommandLineToArgvW escaping; no shell is involved.
        public static string Quote(string value) {
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value) {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') b.Append('\\', slashes * 2 + 1).Append(c);
                else b.Append('\\', slashes).Append(c);
                slashes = 0;
            }
            return b.Append('\\', slashes * 2).Append('"').ToString();
        }
        public static ProcessStartInfo Info(string exe, IEnumerable<string> args) {
            var p = new ProcessStartInfo(exe, String.Join(" ", args.Select(Quote))) {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = Paths.Root
            };
            // Inherit the process environment. Enumerating ProcessStartInfo.EnvironmentVariables
            // can throw on Windows hosts that supply both Path and PATH. Program sets ADB
            // once for this process only, so scrcpy and adb inherit the same executable choice.
            return p;
        }
        public static async Task<Result> Run(string exe, IEnumerable<string> args, int timeout = 20000) {
            using (var p = new Process { StartInfo = Info(exe, args) }) {
                p.Start();
                var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
                var wait = Task.Run(() => p.WaitForExit());
                if (await Task.WhenAny(wait, Task.Delay(timeout)) != wait) {
                    try { p.Kill(); } catch { }
                    throw new TimeoutException("命令超时，请检查手机连接后重试。");
                }
                return new Result { Code = p.ExitCode, Output = await stdout, Error = await stderr };
            }
        }
        public static Task<Result> Adb(string serial, params string[] args) {
            var all = new List<string> { "-s", serial }; all.AddRange(args); return Run(Paths.Adb, all);
        }
        public static async Task Shell(string serial, params string[] args) {
            var all = new List<string> { "shell" }; all.AddRange(args);
            var r = await Adb(serial, all.ToArray()); r.Ensure();
            if (Regex.IsMatch(r.Combined, @"(?im)^(Error:|Unknown command:|Exception|Security exception|java\.)"))
                throw new Exception(r.Combined.Trim());
        }
        public static bool ValidPackage(string s) { return Regex.IsMatch(s ?? "", @"^[A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+$"); }
        public static bool ValidAddress(string s) {
            var m = Regex.Match(s ?? "", @"^(?:[A-Za-z0-9][A-Za-z0-9.\-]*|\[[0-9a-fA-F:]+\]):([0-9]{1,5})$");
            int port; return m.Success && Int32.TryParse(m.Groups[1].Value, out port) && port > 0 && port <= 65535;
        }
    }
    internal sealed class Device {
        public string Serial, Model, State;
        public override string ToString() { return Model + " · " + Serial + (State == "device" ? "" : " · " + State); }
        public static List<Device> Parse(string s) {
            var list = new List<Device>();
            foreach (string line in s.Split('\n')) {
                var m = Regex.Match(line.Trim(), @"^(\S+)\s+(device|unauthorized|offline)\b(.*)$");
                if (!m.Success) continue;
                var model = Regex.Match(m.Groups[3].Value, @"\bmodel:(\S+)");
                list.Add(new Device { Serial = m.Groups[1].Value, State = m.Groups[2].Value,
                    Model = model.Success ? model.Groups[1].Value.Replace('_',' ') : "Android" });
            }
            return list;
        }
    }
    internal sealed class AndroidApp {
        public string Name, Package; public bool System;
        public bool Reader { get { return Regex.IsMatch(Name + " " + Package, "小说|阅读|读书|番茄|七猫|书旗|起点|掌阅|reader|legado", RegexOptions.IgnoreCase); } }
        public static List<AndroidApp> Parse(string text) {
            var list = new List<AndroidApp>();
            foreach (string line in text.Split('\n')) {
                var m = Regex.Match(line.TrimEnd(), @"^\s*([*\-])\s+(.+?)\s+([A-Za-z0-9_]+(?:\.[A-Za-z0-9_]+)+)\s*$");
                if (m.Success) list.Add(new AndroidApp { Name = m.Groups[2].Value.Trim(), Package = m.Groups[3].Value, System = m.Groups[1].Value == "*" });
            }
            return list.GroupBy(a => a.Package).Select(g => g.First()).ToList();
        }
    }
    internal static class AppCatalog {
        // System apps stay hidden in every category, including search and favorites.
        public static List<AndroidApp> Select(IEnumerable<AndroidApp> apps, int category, string query, HashSet<string> favorites) {
            string q=(query??"").Trim();
            return apps.Where(a=>!a.System).Where(a=>category==1?a.Reader:category==2?favorites.Contains(a.Package):true)
                .Where(a=>q.Length==0||a.Name.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0||a.Package.IndexOf(q,StringComparison.OrdinalIgnoreCase)>=0)
                .OrderByDescending(a=>favorites.Contains(a.Package)).ThenBy(a=>a.Name,StringComparer.CurrentCulture).ToList();
        }
    }
    internal static class DisplayProfile {
        static readonly int[] widths={1080,1080,900,1440},heights={1920,2160,1600,1920},densities={420,440,360,360};
        static readonly string[] names={"标准","长页","轻量","平板"};
        public static Size Resolution(int preset,bool landscape){int i=Math.Max(0,Math.Min(3,preset));return landscape?new Size(heights[i],widths[i]):new Size(widths[i],heights[i]);}
        public static string Argument(int preset,bool landscape){var size=Resolution(preset,landscape);return size.Width+"x"+size.Height+"/"+densities[Math.Max(0,Math.Min(3,preset))];}
        public static string Description(int preset,bool landscape){var size=Resolution(preset,landscape);return names[Math.Max(0,Math.Min(3,preset))]+"  ·  "+size.Width+" × "+size.Height;}
    }
    internal sealed class Preferences {
        public string Serial = "", Package = "com.tencent.mm"; public int Preset = 0;
        public bool Flex, Audio, ScreenOff, Clipboard, Dark, Landscape; public int MainCloseAction;
        public HashSet<string> Favorites = new HashSet<string> { "com.tencent.mm" };
        public static int NormalizeMainCloseAction(int value) { return value>=0&&value<=2?value:0; }
        public static Preferences Load() {
            try {
                var x = XElement.Load(Path.Combine(Paths.Data, "settings.xml"));
                return new Preferences { Serial = (string)x.Element("serial") ?? "", Package = (string)x.Element("package") ?? "com.tencent.mm",
                    Preset = (int?)x.Element("preset") ?? 0, Flex = (bool?)x.Element("flex") ?? false,
                    Audio = (bool?)x.Element("audio") ?? false, ScreenOff = (bool?)x.Element("screenOff") ?? false,
                    Clipboard = (bool?)x.Element("clipboard") ?? false, Dark = (bool?)x.Element("dark") ?? false, Landscape = (bool?)x.Element("landscape") ?? false,
                    MainCloseAction = NormalizeMainCloseAction((int?)x.Element("mainCloseAction") ?? 0),
                    Favorites = new HashSet<string>(x.Elements("favorite").Select(e => e.Value)) };
            } catch { return new Preferences(); }
        }
        public void Save() {
            Directory.CreateDirectory(Paths.Data);
            var x = new XElement("settings", new XElement("serial", Serial), new XElement("package", Package), new XElement("preset", Preset),
                new XElement("flex", Flex), new XElement("audio", Audio), new XElement("screenOff", ScreenOff), new XElement("clipboard", Clipboard), new XElement("dark", Dark), new XElement("landscape", Landscape), new XElement("mainCloseAction", NormalizeMainCloseAction(MainCloseAction)),
                Favorites.Select(f => new XElement("favorite", f)));
            string temp = Path.Combine(Paths.Data, "settings.xml.tmp"), dest = Path.Combine(Paths.Data, "settings.xml");
            x.Save(temp); if (File.Exists(dest)) File.Replace(temp, dest, null); else File.Move(temp, dest);
        }
    }
}
