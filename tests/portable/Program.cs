using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml.Linq;
using RDR2ModHolster.Safety;

// Run only from a fresh disposable game fixture containing the production EXE/config.
internal static class Program
{
    static string Root = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
    static int Count;
    static void Check(bool ok, string detail) { if (!ok) throw new Exception(detail); Count++; Console.WriteLine("PASS " + detail); }
    static string Hash(string path) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))); }
    static void Yes(OperationResult r) { if (!r.Success) throw new Exception(r.Message); }
    static Candidate Pick(SafetyEngine e, string n) { return e.Scan().Items.Single(x => x.Name == n); }
    [STAThread] static int Main()
    {
        try
        {
            if (!File.Exists(Path.Combine(Root, "DISPOSABLE-PORTABLE-TEST.txt"))) throw new Exception("Disposable sentinel required");
            string exe = Path.Combine(Root, "CLEAN_v3.2.exe"), config = exe + ".config";
            string beforeExe = Hash(exe), beforeConfig = Hash(config);
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            var type = typeof(SafetyEngine).Assembly.GetType("RDR2ModHolster.MainForm", true);
            using (var form = (Form)Activator.CreateInstance(type, true))
                Check((string)type.GetField("_game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form) == Root, "UI detects adjacent RDR2.exe without Browse");
            using (var process = Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetTempPath(), UseShellExecute = false }))
            {
                try
                {
                    Check(process.WaitForInputIdle(15000), "Production EXE reaches native Windows message loop");
                    var wait = Stopwatch.StartNew();
                    do { System.Threading.Thread.Sleep(100); process.Refresh(); }
                    while (!process.HasExited && process.MainWindowHandle == IntPtr.Zero && wait.ElapsedMilliseconds < 15000);
                    Check(!process.HasExited && process.MainWindowHandle != IntPtr.Zero, "Production main window opens from unrelated working directory");
                    Check(process.CloseMainWindow() && process.WaitForExit(10000), "Production window closes normally");
                }
                finally { if (!process.HasExited) process.Kill(); }
            }
            string docs = Root + "-documents"; Directory.CreateDirectory(docs);
            var e = new SafetyEngine(Root, docs, null);
            Check(Pick(e, Path.GetFileName(exe)).Protected && Pick(e, Path.GetFileName(config)).Protected, "Portable executable and config are protected");
            Check(Pick(e, "PortableTests.exe.config").Protected, "Running process companion config is protected");
            foreach (string n in new[] { Path.GetFileName(exe), Path.GetFileName(config), "PortableTests.exe.config" })
                Check(!e.Holster(ModGroup.Mods, new[] { new Candidate { Name = n, FullPath = Path.Combine(Root, n), Group = ModGroup.Mods } }).Success, "Forged selection refused: " + n);
            File.WriteAllText(Path.Combine(Root, "plugin.asi"), "independent original mod bytes");
            File.WriteAllText(Path.Combine(Root, "my-mod.config"), "independent config bytes");
            string modHash = Hash(Path.Combine(Root, "plugin.asi"));
            Check(!Pick(e, "my-mod.config").Protected, "Ordinary mod config remains selectable");
            for (int cycle = 0; cycle < 3; cycle++)
            {
                var h = e.Holster(ModGroup.Mods, new[] { Pick(e, "plugin.asi"), Pick(e, "my-mod.config") }); Yes(h);
                e = new SafetyEngine(Root, docs, null); Yes(e.Restore(h.SessionId));
                Check(Hash(Path.Combine(Root, "plugin.asi")) == modHash && File.ReadAllText(Path.Combine(Root, "my-mod.config")) == "independent config bytes", "Restarted engine restores original bytes, cycle " + cycle);
            }
            var purge = e.Holster(ModGroup.Mods, new[] { Pick(e, "plugin.asi") }); Yes(purge); Yes(e.Purge(purge.SessionId));
            Check(Hash(exe) == beforeExe && Hash(config) == beforeConfig, "Holster, restore and permanent fixture deletion leave app pair byte-identical");
            File.WriteAllText(Path.Combine(Root, "plugin.asi"), "manifest test");
            var unsafeSession = e.Holster(ModGroup.Mods, new[] { Pick(e, "plugin.asi") }); Yes(unsafeSession);
            string manifest = Path.Combine(unsafeSession.SessionPath, "manifest.xml");
            var xml = XDocument.Load(manifest); xml.Descendants("Item").Single().SetAttributeValue("name", Path.GetFileName(config)); xml.Save(manifest);
            Check(!e.Restore(unsafeSession.SessionId).Success, "Manifest cannot restore over runtime config");
            Check(!e.MoveConflictsAside(unsafeSession.SessionId).Success, "Conflict recovery cannot move runtime config aside");
            Check(!e.Purge(unsafeSession.SessionId).Success && Hash(config) == beforeConfig, "Unsafe manifest purge refused, config intact");
            Console.WriteLine("RESULT pass=" + Count + " fail=0"); return 0;
        }
        catch (Exception ex) { Console.WriteLine("FAIL " + ex); return 1; }
    }
}
