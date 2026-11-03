using System.Reflection;
using System.Windows.Forms;

namespace RDR2ModHolster;

internal static class Ext
{
	public static void SetStyle_Dbl(this Panel p)
	{
		typeof(Panel).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, true, null);
	}
}
