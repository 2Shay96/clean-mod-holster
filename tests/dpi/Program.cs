using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Linq;

internal static class Program
{
    static System.Collections.Generic.IEnumerable<Control> All(Control root){foreach(Control child in root.Controls){yield return child;foreach(var nested in All(child))yield return nested;}}
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
    [STAThread] static void Main(string[] args)
    {
        // Instrument the actual candidate assembly; no filesystem operation is invoked.
        string assembly=Path.GetFullPath(args[0]),output=Path.GetFullPath(args[1]);
        if(File.Exists(output))throw new IOException("Refusing to overwrite prior evidence.");
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var type=Assembly.LoadFrom(assembly).GetType("RDR2ModHolster.MainForm",true);
        using(var form=(Form)Activator.CreateInstance(type,true))
        using(var timer=new Timer {Interval=1000})
        {
            form.Text="CLEAN native DPI diagnostic — no operations";
            var rows=new XElement("nativeDpi",new XAttribute("assembly",assembly));string previous=null;
            timer.Tick+=delegate {
                var header=(Control)type.GetField("_titleBar",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                var sidebar=(Control)type.GetField("_sidebar",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                var scroll=(Panel)type.GetField("_sideScroll",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                var dashboard=(Panel)type.GetField("_dashPanel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                var scan=(Control)type.GetField("_emptyScanButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                var dpi=GetDpiForWindow(form.Handle);
                var scale=type.BaseType.GetProperty("UiScale",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form,null);
                var row=new XElement("sample",new XAttribute("utc",DateTime.UtcNow.ToString("o")),new XAttribute("dpi",dpi),new XAttribute("uiScale",scale),new XAttribute("bounds",form.Bounds),new XAttribute("client",form.ClientRectangle),new XAttribute("header",header.Bounds),new XAttribute("sidebar",sidebar.Bounds),new XAttribute("padding",form.Padding),new XAttribute("fontPoints",form.Font.SizeInPoints));
                row.Add(new XAttribute("scrollClient",scroll.ClientSize),new XAttribute("horizontal",scroll.HorizontalScroll.Visible),new XAttribute("scrollPosition",scroll.AutoScrollPosition),new XAttribute("scrollMin",scroll.AutoScrollMinSize),new XAttribute("childRight",scroll.Controls.Cast<Control>().Max(c=>c.Right)));
                row.Add(new XElement("dashboard",new XAttribute("client",dashboard.ClientRectangle),new XAttribute("display",dashboard.DisplayRectangle),new XAttribute("minimum",dashboard.AutoScrollMinSize),new XAttribute("horizontal",dashboard.HorizontalScroll.Visible),new XAttribute("scan",scan.Bounds)));
                foreach(Form open in Application.OpenForms)
                    foreach(var rich in All(open).OfType<RichTextBox>().Where(x=>x.Visible))
                        row.Add(new XElement("richText",new XAttribute("form",open.Text),new XAttribute("deviceDpi",rich.DeviceDpi),new XAttribute("windowDpi",GetDpiForWindow(rich.Handle)),new XAttribute("fontSize",rich.Font.Size),new XAttribute("fontUnit",rich.Font.Unit),new XAttribute("selectionFontSize",rich.SelectionFont==null?0:rich.SelectionFont.Size),new XAttribute("zoom",rich.ZoomFactor),new XAttribute("lineTwoY",rich.GetPositionFromCharIndex(Math.Max(0,rich.Text.IndexOf('\n')+1)).Y)));
                string key=dpi+"|"+scale+"|"+form.Bounds+"|"+header.Bounds+"|"+sidebar.Bounds+"|"+scroll.HorizontalScroll.Visible+"|"+scroll.ClientSize+"|"+String.Join("",row.Elements().Select(x=>x.ToString()));
                if(key!=previous){rows.Add(row);rows.Save(output);Console.WriteLine(row);previous=key;}
            };
            form.Shown+=delegate{timer.Start();if(args.Length>2 && args[2]=="--about")form.BeginInvoke((MethodInvoker)delegate{type.GetMethod("ShowAbout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,null);});};Application.Run(form);
        }
    }
}
