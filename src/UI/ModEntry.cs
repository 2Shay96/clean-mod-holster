using System.Drawing;
using System.IO;

namespace RDR2ModHolster;

internal class ModEntry
{
    public string Reason;
	public string Type;

	public string Name;

	public string Size;

	public string Status;

	public string FullPath;

	public bool IsDir;

	public static string DetectType(string name, bool isDir)
	{
		if (isDir)
		{
			return "DIR";
		}
		string text = Path.GetExtension(name).ToLowerInvariant();
		return text switch
		{
			".dll" => "DLL", 
			".asi" => "ASI", 
			".log" => "LOG", 
			".ini" => "CFG", 
			".cfg" => "CFG", 
			".xml" => "XML", 
			".txt" => "TXT", 
			".lua" => "LUA", 
			".fx" => "FX", 
			_ => (text.Length <= 1) ? "FILE" : text.Substring(1).ToUpperInvariant(), 
		};
	}

	public static Color TypeColor(string t)
	{
		return t switch
		{
			"DLL" => Th.Crimson, 
			"ASI" => Color.FromArgb(220, 120, 0), 
			"LOG" => Color.FromArgb(170, 140, 30), 
			"CFG" => Color.FromArgb(90, 155, 55), 
			"DIR" => Color.FromArgb(55, 125, 175), 
			"FX" => Color.FromArgb(150, 80, 180), 
			_ => Th.T3, 
		};
	}
}
