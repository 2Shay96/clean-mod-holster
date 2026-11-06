using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Xml.Linq;
using RDR2ModHolster.Safety;

internal static class Program
{
    static string Hash(string p) { using(var sha=SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(sha.ComputeHash(f)); }
    static int Main(string[] args)
    {
        if(args.Length==0)throw new ArgumentException("A new disposable fixture folder is required.");
        string root=Path.GetFullPath(args[0]),game=Path.Combine(root,"game"),docs=Path.Combine(root,"docs"),signal=Path.Combine(root,"copy-started");
        if(args.Length>1 && args[1]=="--worker")
        {
            var engine=new SafetyEngine(game,docs,null);
            engine.Progress=(phase,done,total)=> {
                if(phase=="Holstering" && done>0) { File.WriteAllText(signal,"copy has written data; process may now be killed"); Thread.Sleep(30000); }
            };
            var result=engine.Holster(ModGroup.Mods,engine.Scan().Items.Where(x=>x.Name=="scripts"));
            Console.WriteLine(result.Message);return result.Success?0:1;
        }
        if(Directory.Exists(root))throw new IOException("Refusing to reuse a fixture folder.");
        Directory.CreateDirectory(Path.Combine(game,"scripts"));Directory.CreateDirectory(docs);File.WriteAllText(Path.Combine(game,"RDR2.exe"),"disposable marker");
        byte[] bytes=new byte[8*1024*1024];for(int i=0;i<bytes.Length;i++)bytes[i]=(byte)(i%251);
        string payload=Path.Combine(game,"scripts","payload.dat");File.WriteAllBytes(payload,bytes);string before=Hash(payload);
        string executable=Assembly.GetExecutingAssembly().Location;
        var psi=new ProcessStartInfo(executable,"\""+root+"\" --worker"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var worker=Process.Start(psi))
        {
            var timeout=Stopwatch.StartNew();while(!File.Exists(signal) && !worker.HasExited && timeout.ElapsedMilliseconds<15000)Thread.Sleep(50);
            if(!File.Exists(signal)) { if(!worker.HasExited)worker.Kill();throw new Exception("Worker did not reach copy boundary: "+worker.StandardOutput.ReadToEnd()+worker.StandardError.ReadToEnd()); }
            worker.Kill();worker.WaitForExit();
        }
        if(Hash(payload)!=before)throw new Exception("Abrupt kill changed the original.");
        string store=Path.Combine(game,"_RDR2_ModHolster");string manifest=Directory.GetFiles(store,"manifest.xml",SearchOption.AllDirectories).Single();
        var recorded=XDocument.Load(manifest);string id=(string)recorded.Root.Attribute("id");
        if((string)recorded.Root.Attribute("state")!="Holstering")throw new Exception("No interrupted journal state.");
        var restarted=new SafetyEngine(game,docs,null);var recovery=restarted.Restore(id);if(!recovery.Success)throw new Exception(recovery.Message);
        if(Hash(payload)!=before)throw new Exception("Restart recovery changed the original.");
        new XElement("abruptRecovery",new XAttribute("utc",DateTime.UtcNow.ToString("o")),new XAttribute("result","PASS"),new XAttribute("engineSHA256",Hash(typeof(SafetyEngine).Assembly.Location)),new XAttribute("session",id),new XAttribute("payloadSHA256",before),new XAttribute("bytes",bytes.Length),new XAttribute("stagesRetained",Directory.GetDirectories(store,"s",SearchOption.AllDirectories).Length),"Killed the disposable worker process during the first staged copy; original survived and a fresh engine recovered the journal. Not a power-loss or every-boundary guarantee.").Save(Path.Combine(root,"results.xml"));
        Console.WriteLine("PASS abrupt copy-process kill and restart recovery; original hash retained. Fixture: "+root);return 0;
    }
}
