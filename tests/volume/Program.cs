using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using RDR2ModHolster.Safety;

internal static class Program
{
    static string Hash(string p){using(var s=SHA256.Create())using(var f=File.OpenRead(p))return BitConverter.ToString(s.ComputeHash(f));}
    static void Require(bool value,string message){if(!value)throw new IOException(message);}
    static int Main(string[] args)
    {
        try{return Run(args);}
        catch(Exception ex){Console.Error.WriteLine("REFUSED/FAILED: "+ex.Message);return 1;}
    }
    static int Run(string[] args)
    {
        if(args.Length!=3){Console.WriteLine("Usage: NativeVolumeTests <disposable-volume-root> <new-workspace-evidence-folder> <workspace-root>");return 2;}
        string volume=Path.GetPathRoot(Path.GetFullPath(args[0])),output=Path.GetFullPath(args[1]),workspace=Path.GetFullPath(args[2]);
        Require(Path.GetFullPath(args[0]).TrimEnd('\\')==volume.TrimEnd('\\'),"Only an explicit volume root is accepted.");
        Require(volume!=Path.GetPathRoot(Environment.SystemDirectory) && volume!=Path.GetPathRoot(workspace),"Refusing system/workspace volume.");
        var drive=new DriveInfo(volume);
        Require(drive.IsReady && drive.DriveFormat=="NTFS" && drive.TotalSize>=96L*1024*1024 && drive.TotalSize<=256L*1024*1024,"Need a separate 96–256MiB NTFS disposable volume.");
        Require(drive.VolumeLabel=="CLEAN_DISPOSABLE" && File.ReadAllText(Path.Combine(volume,"CLEAN-DISPOSABLE-TEST-ONLY.txt")).Trim()=="CLEAN disposable volume: no user files", "Disposable label and exact sentinel required.");
        Require(!Directory.Exists(output) && output.StartsWith(workspace.TrimEnd('\\')+"\\scratch\\",StringComparison.OrdinalIgnoreCase),"Need a new workspace scratch evidence folder.");
        Directory.CreateDirectory(output);
        string id=Guid.NewGuid().ToString("N"),fixture=Path.Combine(volume,"CLEAN-fixture-"+id),game=Path.Combine(fixture,"game"),docs=Path.Combine(fixture,"docs");
        Directory.CreateDirectory(game);Directory.CreateDirectory(docs);File.WriteAllText(Path.Combine(game,"RDR2.exe"),"DISPOSABLE marker, not executable");
        string source=Path.Combine(game,"payload.asi");byte[] chunk=new byte[1024*1024];for(int i=0;i<chunk.Length;i++)chunk[i]=(byte)(i%251);
        using(var f=File.Create(source))for(int i=0;i<16;i++)f.Write(chunk,0,chunk.Length);
        string original=Hash(source),filler=Path.Combine(fixture,"space-pressure.bin");bool attempted=false,full=false;
        var engine=new SafetyEngine(game,docs,null);
        engine.Progress=delegate(string phase,long done,long total){
            if(attempted || phase!="Holstering" || done==0)return;
            attempted=true;
            using(var f=new FileStream(filler,FileMode.CreateNew,FileAccess.Write,FileShare.Read,4096,FileOptions.WriteThrough))
            {
                try {while(true){f.Write(chunk,0,chunk.Length);f.Flush();}}
                catch(IOException ex){int code=ex.HResult&0xffff;if(code!=112 && code!=39)throw;full=true;Console.WriteLine("Observed native disk-full error "+code);}
            }
        };
        var holster=engine.Holster(ModGroup.Mods,engine.Scan().Items.Where(x=>x.Name=="payload.asi"));
        Require(attempted && full,"Did not reach a real disk-full event; this is not a passed transfer fault test.");
        Require(!holster.Success && File.Exists(source) && Hash(source)==original,"Disk-full transfer did not fail with original intact.");
        Require(holster.Message.Contains("out of disk space") && !holster.Message.Contains("a verified backup is retained"),"Disk-full message must not promise an unverified/incomplete staged backup.");
        Console.WriteLine("Verified failure message: "+holster.Message);
        string retained=Path.Combine(workspace,"_to_delete","native-volume-test-"+id);Directory.CreateDirectory(retained);
        // Preserve filler bytes on the workspace volume while releasing only disposable-volume space.
        string retainedFiller=Path.Combine(retained,"space-pressure.bin");File.Copy(filler,retainedFiller,false);
        Require(Hash(filler)==Hash(retainedFiller),"Filler preservation mismatch.");
        File.Delete(filler); // Disposable test file only, independently preserved above.
        var restarted=new SafetyEngine(game,docs,null);
        var interrupted=restarted.GetSessions(ModGroup.Mods);Require(interrupted.Count==1,"Expected exactly one readable interrupted journal.");
        foreach(var session in interrupted)Require(restarted.Restore(session.Id).Success,"Disk-full session recovery failed.");
        Require(Hash(source)==original,"Recovery changed original payload.");
        string crossGame=Path.Combine(output,"cross-volume-game"),crossDocs=Path.Combine(fixture,"redirected-docs");Directory.CreateDirectory(crossGame);Directory.CreateDirectory(Path.Combine(crossDocs,"modconfig"));
        File.WriteAllText(Path.Combine(crossGame,"RDR2.exe"),"DISPOSABLE marker");string config=Path.Combine(crossDocs,"modconfig","設定-é.ini");File.WriteAllText(config,"CLEAN separate-volume configuration fixture");string configHash=Hash(config);
        var cross=new SafetyEngine(crossGame,crossDocs,null);var h=cross.Holster(ModGroup.Mods,cross.Scan().Items.Where(x=>x.Name=="Documents/modconfig"));Require(h.Success,h.Message);Require(!File.Exists(config),"Documents fixture stayed live after holster.");Require(cross.Restore(h.SessionId).Success,"Cross-volume Documents restore failed.");Require(Hash(config)==configHash,"Cross-volume Documents hash mismatch.");
        new XElement("nativeVolume",new XAttribute("utc",DateTime.UtcNow.ToString("o")),new XAttribute("result","PASS"),new XAttribute("volume",volume),new XAttribute("size",drive.TotalSize),new XAttribute("engineSHA256",Hash(typeof(SafetyEngine).Assembly.Location)),new XAttribute("originalSHA256",original),new XAttribute("documentsSHA256",configHash),new XAttribute("fillerPreserved",retainedFiller),"Actual ENOSPC during copy, unchanged original and restart recovery; separate-volume injected Documents root roundtrip. This does not change the real Windows Known Folder or test power loss.").Save(Path.Combine(output,"results.xml"));
        Console.WriteLine("PASS native disk-full transfer/recovery and separate-volume Documents fixture. Evidence: "+output);return 0;
    }
}
