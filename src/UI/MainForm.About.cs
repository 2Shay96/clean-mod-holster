using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace RDR2ModHolster;

internal sealed partial class MainForm
{
    // Public author-owned donation link already displayed on CLEAN's Nexus page.
    private const string SupportUrl = "https://www.paypal.com/donate/?hosted_button_id=G677UQMTPA5EL";
    private const string DevelopmentUrl = "https://www.nexusmods.com/reddeadredemption2/mods/8084?tab=articles";
    private const string Roadmap = "CLEAN for GTA VI is planned, targeting day-one support for the PC release, subject to compatibility testing. Follow development here.";

    private void ShowAbout()
    {
        using (var d = new ThemedDialog("About CLEAN - Mod Remover and Holster", "CLEAN - Mod Remover and Holster • v3.2 • by 2Shay", new Size(720, 520)))
        {
            d.AddText("Review, holster and restore your selected RDR2 mod files.\n\n" +
                "Backups and recovery journals: game folder\\_RDR2_ModHolster31\\\n\n" +
                "See the included validation report for tested environments and limitations. Holstering does not verify vanilla integrity.\n\n" +
                "Support is voluntary. Donations do not buy access, updates, assistance or promised features.\n\n" + Roadmap);
            var support = d.AddAction("Support CLEAN through PayPal", Th.Gold);
            support.Click += delegate { OpenPublicLink(SupportUrl); };
            var development = d.AddAction("Follow development", Th.T2);
            development.Click += delegate { OpenPublicLink(DevelopmentUrl); };
            var close = d.AddAction("Close", Th.T2, DialogResult.Cancel);
            d.FocusSafe(close); d.ShowDialog(this);
        }
    }

    private void OpenPublicLink(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { ShowInfo("Could not open your browser", "Open this address in your browser:\n\n" + url + "\n\n" + ex.Message); }
    }
}
