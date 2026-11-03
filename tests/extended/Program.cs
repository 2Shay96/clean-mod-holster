using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Xml.Linq;
using RDR2ModHolster.Safety;

#if NET8_0
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
internal static class ExtendedTests
{
    static string Base;
    static string Filter;
    static readonly List<XElement> Results = new List<XElement>();
    sealed class Skip : Exception { public Skip(string reason) : base(reason) {} }
    sealed class Fixture
    {
        public readonly string Root, Game, Docs;
        public Fixture(string name)
        {
            Root = Path.Combine(Base, name + "-" + Guid.NewGuid().ToString("N").Substring(0,8));
            Game = Path.Combine(Root, "game"); Docs = Path.Combine(Root, "docs");
            Directory.CreateDirectory(Game); Directory.CreateDirectory(Docs);
            System.IO.File.WriteAllText(Path.Combine(Game,"RDR2.exe"), "DISPOSABLE fixture only");
        }
        public SafetyEngine Engine() { return new SafetyEngine(Game, Docs, null); }
        public string File(string name, string text = "original") { string p = Path.Combine(Game,name); Directory.CreateDirectory(Path.GetDirectoryName(p)); System.IO.File.WriteAllText(p,text); return p; }
        public string Manifest(string id) { return Path.Combine(Game,"_RDR2_ModHolster31", id, "manifest.xml"); }
        public string Backup(string id, string name) { return Path.Combine(Game,"_RDR2_ModHolster31", id, "Game",name); }
    }
    static int Main(string[] args)
    {
#if HOLSTER_TESTS
        SafetyEngine.TestManifestLimit = 0;
#endif
        if (args.Length > 0 && args[0] == "--restore")
        {
            var e = new SafetyEngine(args[1],args[2],null); var r = e.Restore(args[3]); Console.WriteLine(r.Message); return r.Success ? 0 : 1;
        }
        Base = Path.GetFullPath(args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(),"CLEAN-disposable-"+Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(Base);
        Filter = args.Length > 1 ? args[1] : null;
        Run("native readonly roundtrip, locked source and lock retry", Locks);
        Run("denied read access stops safely; restoring ACL permits retry", DeniedRead);
        Run("unreadable and missing manifests preserve backup", UnreadableManifest);
        Run("oversized 64MiB manifest rejected before restore", OversizedManifest);
        Run("case-aliased manifest hierarchy fails closed", AliasedManifest);
        Run("junction root and nested junction are refused", Junction);
        Run("source type changes between review and action", ChangedAfterScan);
        Run("separate process operation lock prevents overlap", SeparateProcessLock);
        Run("fresh application process restores persisted session", Restart);
        Run("LML reinstall conflicts and moved-aside content are both recoverable", LmlRecovery);
        Run("Unicode deep path over 260 characters: roundtrip or safe policy refusal", LongUnicode);
        Run("10 repeated cycles retain file SHA256 and empty directories", RepeatedCycles);
        Run("10000-file synthetic load roundtrip retains every hash", LargeSet);
#if HOLSTER_TESTS
        Run("corruption of staged bytes never removes original", StageCorruption);
        Run("source mutation during copy never removes changed source", SourceMutation);
        Run("interrupted restore journal recovers in fresh process", InterruptedRestart);
        Run("disk-space injection refuses before source mutation", DiskSpace);
#endif
        int failed = Results.Count(x => (string)x.Attribute("status") == "FAIL");
        var report = new XElement("validation", new XAttribute("utc",DateTime.UtcNow.ToString("o")),new XAttribute("os",Environment.OSVersion),new XAttribute("runtime",Environment.Version),new XAttribute("engine",typeof(SafetyEngine).Assembly.Location),new XAttribute("engineSHA256",Hash(typeof(SafetyEngine).Assembly.Location)),Results);
        report.Save(Path.Combine(Base,"results.xml"));
        Console.WriteLine("RESULT pass="+Results.Count(x=>(string)x.Attribute("status")=="PASS")+" skip="+Results.Count(x=>(string)x.Attribute("status")=="SKIP")+" fail="+failed+" evidence="+Base);
        return failed == 0 ? 0 : 1;
    }
    static void Run(string name, Action test)
    {
        if (Filter != null && !Filter.Split('|').Any(term => name.IndexOf(term,StringComparison.OrdinalIgnoreCase) >= 0)) return;
        var sw=Stopwatch.StartNew(); string status="PASS", detail="";
        try { test(); } catch(Skip ex) {status="SKIP";detail=ex.Message;} catch(Exception ex) {status="FAIL";detail=ex.ToString();}
        Console.WriteLine(status+" "+name+" ("+sw.ElapsedMilliseconds+" ms) "+detail);
        Results.Add(new XElement("test",new XAttribute("name",name),new XAttribute("status",status),new XAttribute("ms",sw.ElapsedMilliseconds),detail));
    }
    static Candidate Pick(SafetyEngine e,string n) {return e.Scan().Items.Single(x=>x.Name==n);}
    static OperationResult Yes(OperationResult r) {if(!r.Success)throw new Exception(r.Message);return r;}
    static void Check(bool v,string why) {if(!v)throw new Exception(why);}
    static string Hash(string p) {using(var sha=SHA256.Create())using(var s=System.IO.File.OpenRead(p))return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","");}
    static string Inventory(string root) {return String.Join("\n",Directory.GetFileSystemEntries(root,"*",SearchOption.AllDirectories).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>p.Substring(root.Length)+(Directory.Exists(p)?"/":":"+Hash(p))));}
    static void Locks()
    {
        var f=new Fixture("locks"); string p=f.File("x.asi"); var e=f.Engine();var c=Pick(e,"x.asi");
        using(var held=new FileStream(p,FileMode.Open,FileAccess.ReadWrite,FileShare.None))Check(!e.Holster(ModGroup.Mods,new[]{c}).Success,"locked source moved");
        Check(System.IO.File.ReadAllText(p)=="original","locked source changed");
        System.IO.File.SetAttributes(p,FileAttributes.ReadOnly);var h=Yes(e.Holster(ModGroup.Mods,new[]{c}));
        using(var held=new FileStream(f.Backup(h.SessionId,"x.asi"),FileMode.Open,FileAccess.Read,FileShare.None))Check(!e.Restore(h.SessionId).Success,"locked backup restored");
        Yes(e.Restore(h.SessionId));Check(System.IO.File.ReadAllText(p)=="original","readonly changed");Check((System.IO.File.GetAttributes(p)&FileAttributes.ReadOnly)!=0,"readonly lost");
    }
    static FileSecurity Deny(string p)
    {
        var file=new FileInfo(p);
#if NET8_0
        var original=FileSystemAclExtensions.GetAccessControl(file,AccessControlSections.Access);
#else
        var original=file.GetAccessControl(AccessControlSections.Access);
#endif
        var changed=new FileSecurity(); changed.SetSecurityDescriptorBinaryForm(original.GetSecurityDescriptorBinaryForm(),AccessControlSections.Access);
        changed.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.ReadData,AccessControlType.Deny));
        SetAcl(p,changed); return original;
    }
    static void SetAcl(string p,FileSecurity acl)
    {
        // SetAccessControl persists modified sections only. Mark the original DACL
        // modified too, otherwise restoring an untouched snapshot is a no-op.
        var writable = new FileSecurity();
        writable.SetSecurityDescriptorBinaryForm(acl.GetSecurityDescriptorBinaryForm(),AccessControlSections.Access);
#if NET8_0
        FileSystemAclExtensions.SetAccessControl(new FileInfo(p),writable);
#else
        new FileInfo(p).SetAccessControl(writable);
#endif
    }
    static void DeniedRead()
    {
        var f=new Fixture("acl");string p=f.File("denied.asi");var e=f.Engine();var c=Pick(e,"denied.asi");var acl=Deny(p);
        try {bool denied=false;try{System.IO.File.ReadAllText(p);}catch(UnauthorizedAccessException){denied=true;}Check(denied,"fixture did not actually deny access");Check(!e.Holster(ModGroup.Mods,new[]{c}).Success,"denied source moved");}
        finally{SetAcl(p,acl);}
        Check(System.IO.File.ReadAllText(p)=="original","denied source changed");var h=Yes(e.Holster(ModGroup.Mods,new[]{c}));Yes(e.Restore(h.SessionId));
    }
    static void UnreadableManifest()
    {
        var f=new Fixture("manifest-read");f.File("x.asi");var e=f.Engine();var h=Yes(e.Holster(ModGroup.Mods,new[]{Pick(e,"x.asi")}));string p=f.Manifest(h.SessionId);var acl=Deny(p);
        try{Check(!e.Restore(h.SessionId).Success,"unreadable record accepted");Check(System.IO.File.ReadAllText(f.Backup(h.SessionId,"x.asi"))=="original","backup changed");}finally{SetAcl(p,acl);}
        string retained=p+".retained";System.IO.File.Move(p,retained);Check(!e.Restore(h.SessionId).Success,"missing record accepted");System.IO.File.Move(retained,p);Yes(e.Restore(h.SessionId));
    }
    static void OversizedManifest()
    {
        var f=new Fixture("oversize");f.File("x.asi");var e=f.Engine();var h=Yes(e.Holster(ModGroup.Mods,new[]{Pick(e,"x.asi")}));string p=f.Manifest(h.SessionId);byte[] original=System.IO.File.ReadAllBytes(p);
        using(var file=new FileStream(p,FileMode.Create))file.SetLength(64L*1024*1024+1);
        Check(!e.Restore(h.SessionId).Success,"oversized record accepted");Check(System.IO.File.ReadAllText(f.Backup(h.SessionId,"x.asi"))=="original","oversize lost payload");System.IO.File.WriteAllBytes(p,original);Yes(e.Restore(h.SessionId));
    }
    static void AliasedManifest()
    {
        var f=new Fixture("case-alias");f.File("scripts/a/file.txt");var e=f.Engine();var h=Yes(e.Holster(ModGroup.Mods,new[]{Pick(e,"scripts")}));var doc=XDocument.Load(f.Manifest(h.SessionId));var item=doc.Root.Element("Item");var mark=new XElement(item.Elements("Mark").First(x=>(string)x.Attribute("rel")=="a/file.txt"));mark.SetAttributeValue("rel","A");item.Add(mark);doc.Save(f.Manifest(h.SessionId));Check(!e.Restore(h.SessionId).Success,"aliased file/directory accepted");Check(System.IO.File.Exists(f.Backup(h.SessionId,"scripts/a/file.txt")),"alias lost backup");
    }
    static void Junction()
    {
        var f=new Fixture("junction");string outside=Path.Combine(f.Root,"outside");Directory.CreateDirectory(outside);System.IO.File.WriteAllText(Path.Combine(outside,"keep.txt"),"external fixture");string link=Path.Combine(f.Game,"scripts");
        var psi=new ProcessStartInfo("cmd.exe","/d /c mklink /J \""+link+"\" \""+outside+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var p=Process.Start(psi)){p.StandardOutput.ReadToEnd();string err=p.StandardError.ReadToEnd();p.WaitForExit();if(p.ExitCode!=0)throw new Skip("junction creation unavailable: "+err);}
        try{bool refused=false;try{f.Engine().Scan();}catch(IOException){refused=true;}Check(refused,"junction scan accepted");Check(System.IO.File.ReadAllText(Path.Combine(outside,"keep.txt"))=="external fixture","junction target changed");}
        finally{Directory.Delete(link,false);}
        string rootLink=Path.Combine(f.Root,"root-link");psi.Arguments="/d /c mklink /J \""+rootLink+"\" \""+f.Game+"\"";
        using(var p=Process.Start(psi)){p.StandardOutput.ReadToEnd();p.StandardError.ReadToEnd();p.WaitForExit();Check(p.ExitCode==0,"root junction creation failed");}
        try{bool refused=false;try{new SafetyEngine(rootLink,f.Docs,null);}catch(IOException){refused=true;}Check(refused,"linked root accepted");}finally{Directory.Delete(rootLink,false);}
    }
    static void ChangedAfterScan()
    {
        var f=new Fixture("changed");string p=f.File("x.asi");var e=f.Engine();var c=Pick(e,"x.asi");System.IO.File.Move(p,p+".retained");Directory.CreateDirectory(p);System.IO.File.WriteAllText(Path.Combine(p,"new.txt"),"new data");Check(!e.Holster(ModGroup.Mods,new[]{c}).Success,"changed item type accepted");Check(System.IO.File.ReadAllText(Path.Combine(p,"new.txt"))=="new data","changed source lost");
    }
    static void SeparateProcessLock()
    {
        var f=new Fixture("multiprocess");f.File("x.asi");var e=f.Engine();var h=Yes(e.Holster(ModGroup.Mods,new[]{Pick(e,"x.asi")}));
        using(var held=new FileStream(Path.Combine(f.Game,"_RDR2_ModHolster31","operation.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None))Check(Child(f,h.SessionId)!=0,"other process ignored lock");
        Check(System.IO.File.ReadAllText(f.Backup(h.SessionId,"x.asi"))=="original","lock discarded backup");Yes(e.Restore(h.SessionId));
    }
    static int Child(Fixture f,string id)
    {
        string executable;using(var self=Process.GetCurrentProcess())executable=self.MainModule.FileName;
        string prefix=Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase)?"\""+Assembly.GetExecutingAssembly().Location+"\" ":"";
        var psi=new ProcessStartInfo(executable,prefix+"--restore \""+f.Game+"\" \""+f.Docs+"\" "+id){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        using(var p=Process.Start(psi)){string output=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();p.WaitForExit();Console.WriteLine("  child: "+output.Trim());return p.ExitCode;}
    }
    static void Restart(){var f=new Fixture("restart");f.File("x.asi");var e=f.Engine();var h=Yes(e.Holster(ModGroup.Mods,new[]{Pick(e,"x.asi")}));Check(Child(f,h.SessionId)==0,"fresh process restore failed");Check(System.IO.File.ReadAllText(Path.Combine(f.Game,"x.asi"))=="original","restart changed content");}
    static void LmlRecovery()
    {
        var f=new Fixture("lml");f.File("lml/mod.xml","original mod");f.File("vfs.asi","original loader");f.File("NLog.dll","original library");var e=f.Engine();var h=Yes(e.Holster(ModGroup.Mods,e.Scan().Items.Where(x=>x.Recommended)));
        f.File("lml/mod.xml","reinstalled mod");f.File("vfs.asi","reinstalled loader");f.File("NLog.dll","reinstalled library");Check(!e.Restore(h.SessionId).Success,"reinstall overwrote original");var q=Yes(e.MoveConflictsAside(h.SessionId));Yes(e.Restore(h.SessionId));Check(System.IO.File.ReadAllText(Path.Combine(f.Game,"vfs.asi"))=="original loader","original lost");
        var original=Yes(e.Holster(ModGroup.Mods,e.Scan().Items.Where(x=>x.Recommended)));Yes(e.Restore(q.SessionId));Check(System.IO.File.ReadAllText(Path.Combine(f.Game,"lml/mod.xml"))=="reinstalled mod","moved-aside mod lost");Check(System.IO.File.ReadAllText(Path.Combine(f.Game,"NLog.dll"))=="reinstalled library","moved-aside library lost");Check(System.IO.File.ReadAllText(f.Backup(original.SessionId,"vfs.asi"))=="original loader","original no longer independently recoverable");
    }
    static void LongUnicode()
    {
        var f=new Fixture("long");string relative="scripts/ユニコード-é/"+String.Join("/",Enumerable.Repeat("deep-directory-0123456789",11))+"/file.asi";string p=Path.Combine(f.Game,relative.Replace('/',Path.DirectorySeparatorChar));string device="\\\\?\\"+p;
        Directory.CreateDirectory(Path.GetDirectoryName(device));System.IO.File.WriteAllText(device,"long Unicode payload");Check(p.Length>260,"long path fixture too short");var e=f.Engine();var h=e.Holster(ModGroup.Mods,new[]{Pick(e,"scripts")});
        if(!h.Success)
        {
            Check(System.IO.File.ReadAllText(device)=="long Unicode payload","long path refusal changed original");
            Check(String.IsNullOrEmpty(h.SessionId),"long path refusal occurred after publishing a session; investigate staging/recovery");
            Console.WriteLine("  LIMITATION: long path was safely refused on this host: "+h.Message);
            return;
        }
        Yes(e.Restore(h.SessionId));Check(System.IO.File.ReadAllText(device)=="long Unicode payload","long path lost");
    }
    static void RepeatedCycles()
    {
        var f=new Fixture("cycles");f.File("scripts/ユニコード.asi");Directory.CreateDirectory(Path.Combine(f.Game,"scripts","empty"));string before=Inventory(Path.Combine(f.Game,"scripts"));for(int i=0;i<10;i++){var e=f.Engine();var h=Yes(e.Holster(ModGroup.Mods,new[]{Pick(e,"scripts")}));Yes(e.Restore(h.SessionId));Check(Inventory(Path.Combine(f.Game,"scripts"))==before,"cycle "+i+" changed files");}
    }
    static void LargeSet()
    {
        var f=new Fixture("load");for(int i=0;i<10000;i++)f.File("scripts/"+(i/100)+"/mod-"+i+".dat",new string((char)('a'+i%20),64));string before=Inventory(Path.Combine(f.Game,"scripts"));var e=f.Engine();var h=Yes(e.Holster(ModGroup.Mods,new[]{Pick(e,"scripts")}));Yes(e.Restore(h.SessionId));Check(Inventory(Path.Combine(f.Game,"scripts"))==before,"load lost bytes or paths");
    }
#if HOLSTER_TESTS
    static void StageCorruption()
    {
        var f=new Fixture("stage-corruption");string p=f.File("x.asi");var e=f.Engine();SafetyEngine.FaultInjector=point=>{if(point=="AfterStageCopy")foreach(string stage in Directory.GetFiles(Path.Combine(f.Game,"_RDR2_ModHolster31"),"x.asi",SearchOption.AllDirectories))System.IO.File.WriteAllText(stage,"tampered!");};
        try{Check(!e.Holster(ModGroup.Mods,new[]{Pick(e,"x.asi")}).Success,"corrupt stage committed");}finally{SafetyEngine.FaultInjector=null;}Check(System.IO.File.ReadAllText(p)=="original","corrupt stage lost original");
    }
    static void SourceMutation()
    {
        var f=new Fixture("source-mutation");string p=f.File("x.asi");var e=f.Engine();SafetyEngine.FaultInjector=point=>{if(point=="AfterStageCopy")System.IO.File.WriteAllText(p,"changed live content");};
        try{Check(!e.Holster(ModGroup.Mods,new[]{Pick(e,"x.asi")}).Success,"changed source removed");}finally{SafetyEngine.FaultInjector=null;}Check(System.IO.File.ReadAllText(p)=="changed live content","changed source lost");
    }
    static void InterruptedRestart()
    {
        var f=new Fixture("interrupt");f.File("x.asi");var e=f.Engine();var h=Yes(e.Holster(ModGroup.Mods,new[]{Pick(e,"x.asi")}));SafetyEngine.FaultInjector=point=>{if(point=="AfterRestoreJournal")throw new IOException("injected interruption");};
        try{Check(!e.Restore(h.SessionId).Success,"fault did not fire");}finally{SafetyEngine.FaultInjector=null;}Check(Child(f,h.SessionId)==0,"restart failed");Check(System.IO.File.ReadAllText(Path.Combine(f.Game,"x.asi"))=="original","restart lost bytes");
    }
    static void DiskSpace()
    {
        var f=new Fixture("disk-space");string p=f.File("x.asi");var e=f.Engine();SafetyEngine.TestFreeSpaceBytes=1;
        try{Check(!e.Holster(ModGroup.Mods,new[]{Pick(e,"x.asi")}).Success,"shortfall accepted");}finally{SafetyEngine.TestFreeSpaceBytes=0;}Check(System.IO.File.ReadAllText(p)=="original","shortfall changed source");
    }
#endif
}
