using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Linq;
using RDR2ModHolster.Safety;
// UI-only manual exercise. No SafetyEngine instance or file-operation method is called.
// Only the result XML is written; displayed candidate names are synthetic strings.
internal static class Program {
 [STAThread] static void Main(string[] args) {
  bool deleteOnly=args.Length==1 && args[0]=="--delete-only";
  if(args.Length>0 && !deleteOnly)throw new ArgumentException("Only --delete-only is supported");
  string output=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"manual-results.xml");
  if(File.Exists(output))throw new IOException("Do not overwrite prior manual evidence");
  Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
  Type type=typeof(Candidate).Assembly.GetType("RDR2ModHolster.MainForm",true);
  using(var form=(Form)Activator.CreateInstance(type,true)) {
   form.Shown+=delegate { form.BeginInvoke(new Action(()=>{
    var report=new XElement("manualDialogResults",new XAttribute("utc",DateTime.UtcNow.ToString("o")),new XAttribute("mode","UI-only; no file operations"),new XAttribute("deleteOnly",deleteOnly));
    try {
     if(!deleteOnly) {
     var scan=new ScanResult();
     scan.Items.Add(new Candidate {Name="sample-mod.asi",FullPath="SYNTHETIC ONLY",Reason="Keyboard fixture: recommended sample",Recommended=true,Group=ModGroup.Mods});
     scan.Items.Add(new Candidate {Name="unknown-sample.dll",FullPath="SYNTHETIC ONLY",Reason="Keyboard fixture: unknown unchecked",Group=ModGroup.Mods});
     scan.Items.Add(new Candidate {Name="protected-sample.rpf",FullPath="SYNTHETIC ONLY",Reason="Keyboard fixture: protected, cannot select",Protected=true,Group=ModGroup.Mods});
     var selected=Call(type,form,"ReviewCandidates",ModGroup.Mods,scan,"Keyboard check - review only");
     report.Add(new XElement("review",new XAttribute("cancelled",selected==null)));
     var confirm=(bool)Call(type,form,"ShowTextDialog","Keyboard check - confirmation","UI-only check. No file operation follows. With Cancel focused, press Enter; it should cancel this dialog.",true,false);
     report.Add(new XElement("confirmation",new XAttribute("cancelled",!confirm)));
     }
     var deletion=(bool)Call(type,form,"RequireDeleteText");
     report.Add(new XElement("deletePrompt",new XAttribute("cancelled",!deletion)));
     report.Add(new XElement("limitation","Return values do not prove which physical keys were used. Record the author's report separately."));
    } catch(Exception e) {report.Add(new XElement("error",e.ToString()));}
    new XDocument(report).Save(output);form.Close();
   }));};
   Application.Run(form);
  }
 }
 static object Call(Type type,Form owner,string name,params object[] args) {return type.GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(owner,args);}
}
