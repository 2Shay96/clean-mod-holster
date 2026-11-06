using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using RDR2ModHolster.Safety;

// Independent black-box tests.  Apart from fixture files, this program only uses the
// public SafetyEngine API and deliberately does not call private engine members.
internal static class Program
{
    sealed class Fixture : IDisposable
    {
        public readonly string Root, Game, Documents;
        public Fixture(string name, bool makeDocuments = true)
        {
            Root = Path.Combine(Base, name + "-" + Guid.NewGuid().ToString("N"));
            Game = Path.Combine(Root, "game");
            Documents = Path.Combine(Root, "documents");
            Directory.CreateDirectory(Game);
            if (makeDocuments) Directory.CreateDirectory(Documents);
            File.WriteAllText(Path.Combine(Game, "RDR2.exe"), "synthetic executable marker");
        }
        public SafetyEngine Engine(string documents = null) => new SafetyEngine(Game, documents ?? Documents, null);
        public void Dispose() { try { if (Directory.Exists(Root)) Directory.Delete(Root, true); } catch { } }
    }

    sealed class SkipTest : Exception { public SkipTest(string message) : base(message) { } }
    sealed class Outcome { public string Name { get; set; } public string Status { get; set; } public string Detail { get; set; } public long Milliseconds { get; set; } }
    static readonly string Base = Path.Combine(Path.GetTempPath(), "RDR2-Safety-Release-" + Guid.NewGuid().ToString("N"));
    static readonly List<Outcome> Outcomes = new List<Outcome>();

    static void Main()
    {
        Directory.CreateDirectory(Base);
        var tests = new (string, Action)[]
        {
            ("70-file hash roundtrip, empty directories, metadata names", RoundTrip),
            ("unknown future DLL and .egstore are not recommended or moved", ProtectionAndUnknown),
            ("ReShade and ENB groups/proxy companion evidence are separate", GroupAndProxyEvidence),
            ("unknown explicitly selected item roundtrips", ExplicitUnknownAndUnicode),
            ("fresh scan rejects forged protected Candidate", ForgedProtectedCandidate),
            ("portable CLEAN config protected without excluding mod configs", PortableConfig),
            ("restore collision preflight leaves every prior item untouched", RestoreCollisionPreflight),
            ("repeated holster refuses and preserves prior backup", RepeatedHolster),
            ("nested link failure leaves no orphan session and clean retry works", NestedLinkNoOrphan),
            ("absent Documents root does not block game operation", AbsentDocuments),
            ("different-filesystem Documents/modconfig roundtrip", CrossFilesystemDocuments),
            ("external operation lock refuses mutation", OperationLock),
            ("corrupt/traversal/Windows-alias manifests fail closed", ManifestValidation),
            ("tampered backup content blocks purge", TamperedBackupBlocksPurge),
            ("purge deletes only verified selected payload", SuccessfulPurgeScope),
            ("legacy restore is safe, conflicts preserve both sides", LegacySafeRestore),
            ("legacy .egstore restores only to a missing destination", LegacyEgstore),
            ("successful legacy restore and empty legacy dirs do not block holster", LegacyCompletionAndEmptyDirs),
            ("legacy steam executable is refused", LegacySteamExecutable),
            ("partial restore source deletion resumes without overwrite", PartialRestoreSourceDeletion),
            ("partial backup deletion resumes safely", PartialBackupDeletion),
            ("intact source after interrupted precommit remains recoverable", InterruptedPrecommit),
            ("Purging state refuses restore", PurgingState),
            ("Windows readonly/sharing behavior", WindowsOnlyFilesystemBehavior)
        };

        foreach (var test in tests)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                test.Item2();
                sw.Stop(); Outcomes.Add(new Outcome { Name = test.Item1, Status = "PASS", Detail = "", Milliseconds = sw.ElapsedMilliseconds });
                Console.WriteLine("PASS " + test.Item1);
            }
            catch (SkipTest ex)
            {
                sw.Stop(); Outcomes.Add(new Outcome { Name = test.Item1, Status = "SKIP", Detail = ex.Message, Milliseconds = sw.ElapsedMilliseconds });
                Console.WriteLine("SKIP " + test.Item1 + ": " + ex.Message);
            }
            catch (Exception ex)
            {
                sw.Stop(); Outcomes.Add(new Outcome { Name = test.Item1, Status = "FAIL", Detail = ex.ToString(), Milliseconds = sw.ElapsedMilliseconds });
                Console.Error.WriteLine("FAIL " + test.Item1 + ": " + ex);
            }
        }

        var report = new { generatedUtc = DateTime.UtcNow, os = Environment.OSVersion.ToString(), framework = Environment.Version.ToString(), outcomes = Outcomes };
        string reportPath = Path.Combine(Directory.GetCurrentDirectory(), "release-test-report.json");
        File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        int passed = Outcomes.Count(x => x.Status == "PASS"), skipped = Outcomes.Count(x => x.Status == "SKIP"), failed = Outcomes.Count(x => x.Status == "FAIL");
        Console.WriteLine("RESULT pass=" + passed + " skip=" + skipped + " fail=" + failed + " report=" + reportPath);
        try { Directory.Delete(Base, true); } catch { }
        Environment.ExitCode = failed == 0 ? 0 : 1;
    }

    static void PortableConfig()
    {
        using (var f = new Fixture("portable-config"))
        {
            string config = Path.Combine(f.Game, "CLEAN_v3.2.exe.config");
            File.WriteAllText(config, "runtime settings");
            File.WriteAllText(Path.Combine(f.Game, "my-mod.config"), "mod settings");
            var e = f.Engine();
            Assert(Pick(e, "CLEAN_v3.2.exe.config").Protected, "CLEAN config is selectable");
            Assert(!Pick(e, "my-mod.config").Protected, "unrelated mod config excluded");
            var result = e.Holster(ModGroup.Mods, new[] { new Candidate { Name = "CLEAN_v3.2.exe.config", FullPath = config, Group = ModGroup.Mods } });
            Assert(!result.Success && File.ReadAllText(config) == "runtime settings", "forged config selection moved runtime settings");
        }
    }

    static void RoundTrip()
    {
        using (var f = new Fixture("roundtrip"))
        {
            string scripts = Path.Combine(f.Game, "scripts"); Directory.CreateDirectory(Path.Combine(scripts, "empty", "nested"));
            for (int n = 0; n < 70; n++) File.WriteAllText(Path.Combine(scripts, "mod-" + n + ".asi"), new string((char)('a' + n % 23), n + 1));
            string before = Snapshot(scripts);
            var e = f.Engine(); var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "scripts") }));
            Assert(!Directory.Exists(scripts), "active source directory was not removed");
            Require(e.Restore(h.SessionId));
            Assert(Snapshot(scripts) == before, "70-file/empty-directory roundtrip changed content");
        }
    }

    static void ProtectionAndUnknown()
    {
        using (var f = new Fixture("protection"))
        {
            Directory.CreateDirectory(Path.Combine(f.Game, ".egstore")); File.WriteAllText(Path.Combine(f.Game, ".egstore", "manifest"), "launcher");
            File.WriteAllText(Path.Combine(f.Game, "future-dlss-v99.dll"), "future"); File.WriteAllText(Path.Combine(f.Game, "plugin.asi"), "mod");
            var e = f.Engine(); var store = Pick(e, ".egstore"); var future = Pick(e, "future-dlss-v99.dll");
            Assert(store.Protected, ".egstore is not protected"); Assert(!future.Recommended && !future.Protected, "future DLL was silently classified");
            Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "plugin.asi") }));
            Assert(Directory.Exists(Path.Combine(f.Game, ".egstore")) && File.Exists(Path.Combine(f.Game, "future-dlss-v99.dll")), "unselected launcher/future content moved");
        }
    }

    static void GroupAndProxyEvidence()
    {
        using (var f = new Fixture("groups"))
        {
            File.WriteAllText(Path.Combine(f.Game, "dxgi.dll"), "proxy"); File.WriteAllText(Path.Combine(f.Game, "d3d11.dll"), "proxy"); File.WriteAllText(Path.Combine(f.Game, "dinput8.dll"), "proxy");
            var before = f.Engine().Scan().Items;
            Assert(!Find(before, "dxgi.dll").Recommended && !Find(before, "d3d11.dll").Recommended && !Find(before, "dinput8.dll").Recommended, "ambiguous proxies lack companion evidence");
            File.WriteAllText(Path.Combine(f.Game, "ReShade.ini"), "reshade"); File.WriteAllText(Path.Combine(f.Game, "ReShade64.dll"), "reshade");
            File.WriteAllText(Path.Combine(f.Game, "enbseries.ini"), "enb"); File.WriteAllText(Path.Combine(f.Game, "enbseries"), "not a directory");
            File.WriteAllText(Path.Combine(f.Game, "plugin.asi"), "asi");
            var e = f.Engine(); var all = e.Scan().Items;
            Assert(Find(all, "ReShade.ini").Recommended && Find(all, "ReShade.ini").Group == ModGroup.ReShade, "ReShade evidence not grouped");
            Assert(Find(all, "dxgi.dll").Recommended && Find(all, "dxgi.dll").Group == ModGroup.ReShade, "ReShade proxy evidence not grouped");
            Assert(Find(all, "d3d11.dll").Recommended && Find(all, "d3d11.dll").Group == ModGroup.ENB, "ENB proxy evidence not grouped");
            Assert(Find(all, "dinput8.dll").Recommended && Find(all, "dinput8.dll").Group == ModGroup.Mods, "ASI proxy evidence not grouped");
            Require(e.Holster(ModGroup.ReShade, new[] { Pick(e, "ReShade.ini"), Pick(e, "ReShade64.dll"), Pick(e, "dxgi.dll") }));
            Assert(File.Exists(Path.Combine(f.Game, "enbseries.ini")) && File.Exists(Path.Combine(f.Game, "d3d11.dll")), "ReShade holster touched ENB files");
            // The groups have independent active-session slots and can be operated independently.
            Require(e.Restore(e.GetSessions(ModGroup.ReShade).Single().Id));
            Require(e.Holster(ModGroup.ENB, new[] { Pick(e, "enbseries.ini"), Pick(e, "d3d11.dll") }));
            Require(e.Restore(e.GetSessions(ModGroup.ENB).Single().Id));
        }
    }

    static void ExplicitUnknownAndUnicode()
    {
        using (var f = new Fixture("explicit-unknown"))
        {
            string name = "Mystery!ユニコード.future"; File.WriteAllText(Path.Combine(f.Game, name), "explicit unknown payload");
            var e = f.Engine(); var c = Pick(e, name); Assert(!c.Recommended && !c.Protected, "unknown explicit item was auto-selected");
            string before = File.ReadAllText(c.FullPath); var h = Require(e.Holster(ModGroup.Mods, new[] { c }));
            Assert(!File.Exists(c.FullPath), "explicit unknown item was not holstered"); Require(e.Restore(h.SessionId));
            Assert(File.Exists(c.FullPath) && File.ReadAllText(c.FullPath) == before, "Unicode/exclamation item did not roundtrip");
        }
    }

    static void ForgedProtectedCandidate()
    {
        using (var f = new Fixture("forged"))
        {
            Directory.CreateDirectory(Path.Combine(f.Game, ".egstore")); File.WriteAllText(Path.Combine(f.Game, ".egstore", "x"), "x");
            var e = f.Engine(); var actual = Pick(e, ".egstore");
            var forged = new Candidate { Name = "pretend.asi", FullPath = actual.FullPath, IsDirectory = actual.IsDirectory, Group = ModGroup.Mods, Recommended = true, Protected = false };
            var r = e.Holster(ModGroup.Mods, new[] { forged });
            Assert(!r.Success, "forged protected candidate was accepted"); Assert(Directory.Exists(actual.FullPath), "forged candidate moved protected content");
        }
    }

    static void RestoreCollisionPreflight()
    {
        using (var f = new Fixture("collision"))
        {
            File.WriteAllText(Path.Combine(f.Game, "a.asi"), "a"); string dir = Path.Combine(f.Game, "scripts"); Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "s.asi"), "s");
            var e = f.Engine(); var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "a.asi"), Pick(e, "scripts") }));
            File.WriteAllText(Path.Combine(f.Game, "a.asi"), "a"); // even identical collision is not a new restore authorization.
            var r = e.Restore(h.SessionId);
            Assert(!r.Success, "identical destination collision was accepted"); Assert(File.ReadAllText(Path.Combine(f.Game, "a.asi")) == "a", "collision file changed");
            Assert(!Directory.Exists(dir), "preflight did not preserve all prior sources as backup-only");
            string backup = SessionItem(f, h.SessionId, "Game", "scripts"); Assert(Directory.Exists(backup), "preflight deleted an unconflicted backup");
        }
    }

    static void RepeatedHolster()
    {
        using (var f = new Fixture("repeat"))
        {
            File.WriteAllText(Path.Combine(f.Game, "one.asi"), "one"); var e = f.Engine(); var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "one.asi") }));
            File.WriteAllText(Path.Combine(f.Game, "two.asi"), "two"); var r = e.Holster(ModGroup.Mods, new[] { Pick(e, "two.asi") });
            Assert(!r.Success && File.Exists(Path.Combine(f.Game, "two.asi")), "second holster overwrote active content");
            Assert(File.Exists(SessionItem(f, h.SessionId, "Game", "one.asi")), "first backup was not preserved");
        }
    }

    static void NestedLinkNoOrphan()
    {
        if (OperatingSystem.IsWindows()) throw new SkipTest("symlink fixture is intentionally Linux-only in this suite");
        using (var f = new Fixture("link-orphan"))
        {
            string target = Path.Combine(f.Root, "outside"); Directory.CreateDirectory(target); File.WriteAllText(Path.Combine(target, "x.asi"), "x");
            string link = Path.Combine(f.Game, "scripts"); Directory.CreateSymbolicLink(link, target); var e = f.Engine();
            var r = e.Holster(ModGroup.Mods, new[] { new Candidate { Name = "scripts", FullPath = link, IsDirectory = true, Group = ModGroup.Mods } });
            Assert(!r.Success, "nested link holstered"); Assert(!SessionDirectories(f).Any(), "failed preflight orphaned a session folder");
            Directory.Delete(link); Directory.CreateDirectory(link); File.WriteAllText(Path.Combine(link, "clean.asi"), "clean");
            Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "scripts") }));
        }
    }

    static void AbsentDocuments()
    {
        using (var f = new Fixture("absent-documents", false))
        {
            var e = f.Engine(); Assert(!e.Scan().Items.Any(x => x.Name == "Documents/modconfig"), "missing Documents was invented by Scan");
            File.WriteAllText(Path.Combine(f.Game, "game.asi"), "game"); Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "game.asi") }));
        }
    }

    static void CrossFilesystemDocuments()
    {
        if (!Directory.Exists("/dev/shm")) throw new SkipTest("/dev/shm is unavailable");
        string probeA = Path.Combine(Path.GetTempPath(), "safety-device-a-" + Guid.NewGuid().ToString("N")); string probeB = Path.Combine("/dev/shm", "safety-device-b-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(probeA); Directory.CreateDirectory(probeB);
            if (Device(probeA) == Device(probeB)) throw new SkipTest("/dev/shm is not a different filesystem here");
            using (var f = new Fixture("cross-volume"))
            {
                string docs = Path.Combine(probeB, "documents"); Directory.CreateDirectory(docs); string mod = Path.Combine(docs, "modconfig"); Directory.CreateDirectory(Path.Combine(mod, "empty"));
                File.WriteAllText(Path.Combine(mod, "config!ユニコード.ini"), "settings"); var e = f.Engine(docs); string before = Snapshot(mod);
                var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "Documents/modconfig") })); Assert(!Directory.Exists(mod), "cross-volume source remains after verified transfer");
                Require(e.Restore(h.SessionId)); Assert(Snapshot(mod) == before, "cross-volume Documents roundtrip changed data");
            }
        }
        finally { try { Directory.Delete(probeA, true); } catch { } try { Directory.Delete(probeB, true); } catch { } }
    }

    static void OperationLock()
    {
        using (var f = new Fixture("lock"))
        {
            File.WriteAllText(Path.Combine(f.Game, "held.asi"), "held"); var e = f.Engine(); string lockPath = Path.Combine(f.Game, "_RDR2_ModHolster", "operation.lock");
            using (var held = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var r = e.Holster(ModGroup.Mods, new[] { Pick(e, "held.asi") }); Assert(!r.Success, "external operation lock was ignored");
            }
            Assert(File.Exists(Path.Combine(f.Game, "held.asi")), "lock failure changed active source");
        }
    }

    static void ManifestValidation()
    {
        string[] edits = { "../escape", "CON.asi", "x.asi.", "x:asi", "unknown-record" };
        foreach (string edit in edits)
        {
            using (var f = new Fixture("manifest-" + Math.Abs(edit.GetHashCode())))
            {
                File.WriteAllText(Path.Combine(f.Game, "x.asi"), "x"); var e = f.Engine(); var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "x.asi") }));
                string path = Path.Combine(f.Game, "_RDR2_ModHolster", h.SessionId, "manifest.xml"); var doc = XDocument.Load(path); var root = doc.Root;
                if (edit == "unknown-record") root.Add(new XElement("Unknown", new XAttribute("x", "1")));
                else root.Elements("Item").Single().SetAttributeValue("name", edit);
                doc.Save(path); var r = e.Restore(h.SessionId);
                Assert(!r.Success, "unsafe manifest edit was accepted: " + edit);
                Assert(!File.Exists(Path.Combine(f.Game, "x.asi")), "unsafe manifest edit mutated live source: " + edit);
            }
        }
    }

    static void TamperedBackupBlocksPurge()
    {
        using (var f = new Fixture("tampered-purge"))
        {
            File.WriteAllText(Path.Combine(f.Game, "x.asi"), "original"); var e = f.Engine(); var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "x.asi") }));
            string backup = SessionItem(f, h.SessionId, "Game", "x.asi"); File.WriteAllText(backup, "tampered"); var r = e.Purge(h.SessionId);
            Assert(!r.Success, "tampered backup was purged"); Assert(File.Exists(backup), "tampered evidence was deleted"); Assert(e.GetSessions(ModGroup.Mods).Any(s => s.Id == h.SessionId), "failed purge ceased to be recoverable");
        }
    }

    static void SuccessfulPurgeScope()
    {
        using (var f = new Fixture("purge-scope"))
        {
            File.WriteAllText(Path.Combine(f.Game, "selected.asi"), "selected"); File.WriteAllText(Path.Combine(f.Game, "future.dll"), "future"); Directory.CreateDirectory(Path.Combine(f.Game, ".egstore"));
            File.WriteAllText(Path.Combine(f.Game, ".egstore", "launcher"), "launcher"); var e = f.Engine(); var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "selected.asi") }));
            Require(e.Purge(h.SessionId)); Assert(!File.Exists(SessionItem(f, h.SessionId, "Game", "selected.asi")), "purge retained selected payload");
            Assert(File.Exists(Path.Combine(f.Game, "future.dll")) && File.Exists(Path.Combine(f.Game, ".egstore", "launcher")), "purge deleted unselected content");
            Assert(!e.GetSessions(ModGroup.Mods).Any(s => s.Id == h.SessionId), "purged session is still recoverable");
        }
    }

    static void LegacySafeRestore()
    {
        using (var f = new Fixture("legacy-safe"))
        {
            string old = Path.Combine(f.Game, "_RDR2_Mods_Disabled"); Directory.CreateDirectory(old); File.WriteAllText(Path.Combine(old, "old.asi"), "old");
            var e = f.Engine(); Require(e.RestoreLegacy(ModGroup.Mods)); Assert(File.Exists(Path.Combine(f.Game, "old.asi")), "legacy item was not restored"); Assert(!e.HasLegacy(ModGroup.Mods), "empty legacy root still blocks UI");
        }
        using (var f = new Fixture("legacy-conflict"))
        {
            string old = Path.Combine(f.Game, "_RDR2_Mods_Disabled"); Directory.CreateDirectory(old); File.WriteAllText(Path.Combine(old, "same.asi"), "old"); File.WriteAllText(Path.Combine(f.Game, "same.asi"), "active");
            var r = f.Engine().RestoreLegacy(ModGroup.Mods); Assert(!r.Success, "legacy collision overwrote active item"); Assert(File.ReadAllText(Path.Combine(old, "same.asi")) == "old" && File.ReadAllText(Path.Combine(f.Game, "same.asi")) == "active", "legacy collision changed either side");
        }
    }

    static void LegacyEgstore()
    {
        using (var f = new Fixture("legacy-egstore"))
        {
            string old = Path.Combine(f.Game, "_RDR2_Mods_Disabled", ".egstore"); Directory.CreateDirectory(old); File.WriteAllText(Path.Combine(old, "data"), "launcher data");
            var e = f.Engine(); Require(e.RestoreLegacy(ModGroup.Mods)); Assert(File.Exists(Path.Combine(f.Game, ".egstore", "data")), "legacy .egstore was not restored");
        }
        using (var f = new Fixture("legacy-egstore-conflict"))
        {
            string old = Path.Combine(f.Game, "_RDR2_Mods_Disabled", ".egstore"); Directory.CreateDirectory(old); File.WriteAllText(Path.Combine(old, "data"), "old");
            Directory.CreateDirectory(Path.Combine(f.Game, ".egstore")); File.WriteAllText(Path.Combine(f.Game, ".egstore", "data"), "active");
            var r = f.Engine().RestoreLegacy(ModGroup.Mods); Assert(!r.Success, "legacy .egstore overwrite was accepted"); Assert(File.ReadAllText(Path.Combine(old, "data")) == "old" && File.ReadAllText(Path.Combine(f.Game, ".egstore", "data")) == "active", "legacy .egstore conflict changed data");
        }
    }

    static void LegacyCompletionAndEmptyDirs()
    {
        using (var f = new Fixture("legacy-empty"))
        {
            string root = Path.Combine(f.Game, "_RDR2_Mods_Disabled"); Directory.CreateDirectory(Path.Combine(root, "Documents"));
            var e = f.Engine(); Assert(!e.HasLegacy(ModGroup.Mods), "empty legacy Documents blocked operation"); File.WriteAllText(Path.Combine(f.Game, "new.asi"), "new"); Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "new.asi") }));
        }
        using (var f = new Fixture("legacy-then-holster"))
        {
            string root = Path.Combine(f.Game, "_RDR2_Mods_Disabled"); Directory.CreateDirectory(root); File.WriteAllText(Path.Combine(root, "old.asi"), "old");
            var e = f.Engine(); Require(e.RestoreLegacy(ModGroup.Mods)); Assert(!e.HasLegacy(ModGroup.Mods), "restored legacy directory blocked new operation"); Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "old.asi") }));
        }
    }

    static void LegacySteamExecutable()
    {
        using (var f = new Fixture("legacy-steam-exe"))
        {
            string old = Path.Combine(f.Game, "_RDR2_Mods_Disabled"); Directory.CreateDirectory(old); File.WriteAllText(Path.Combine(old, "steam.exe"), "not launcher");
            var r = f.Engine().RestoreLegacy(ModGroup.Mods); Assert(!r.Success, "legacy steam executable was accepted"); Assert(File.Exists(Path.Combine(old, "steam.exe")), "legacy steam executable was deleted");
        }
    }

    static void PartialRestoreSourceDeletion()
    {
        using (var f = new Fixture("partial-source"))
        {
            string d = Path.Combine(f.Game, "scripts"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "a.asi"), "a"); File.WriteAllText(Path.Combine(d, "b.asi"), "b");
            var e = f.Engine(); var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "scripts") })); string session = Path.Combine(f.Game, "_RDR2_ModHolster", h.SessionId);
            Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "a.asi"), "a"); SetManifestState(Path.Combine(session, "manifest.xml"), "Restoring", "Restoring");
            Require(e.Restore(h.SessionId)); Assert(File.Exists(Path.Combine(d, "a.asi")) && File.Exists(Path.Combine(d, "b.asi")), "partial active source was not reconstructed");
        }
    }

    static void PartialBackupDeletion()
    {
        using (var f = new Fixture("partial-backup"))
        {
            string d = Path.Combine(f.Game, "scripts"); Directory.CreateDirectory(d); File.WriteAllText(Path.Combine(d, "a.asi"), "a"); File.WriteAllText(Path.Combine(d, "b.asi"), "b");
            var e = f.Engine(); var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "scripts") })); string backup = SessionItem(f, h.SessionId, "Game", "scripts");
            Directory.CreateDirectory(d); File.Copy(Path.Combine(backup, "a.asi"), Path.Combine(d, "a.asi")); File.Copy(Path.Combine(backup, "b.asi"), Path.Combine(d, "b.asi")); File.Delete(Path.Combine(backup, "a.asi"));
            SetManifestState(Path.Combine(f.Game, "_RDR2_ModHolster", h.SessionId, "manifest.xml"), "Restoring", "Restoring"); Require(e.Restore(h.SessionId)); Assert(!Directory.Exists(backup), "verified partial backup was not completed");
        }
    }

    static void InterruptedPrecommit()
    {
        using (var f = new Fixture("precommit"))
        {
            File.WriteAllText(Path.Combine(f.Game, "x.asi"), "x"); var e = f.Engine(); var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "x.asi") }));
            string backup = SessionItem(f, h.SessionId, "Game", "x.asi"); File.Copy(backup, Path.Combine(f.Game, "x.asi")); File.Delete(backup);
            SetManifestState(Path.Combine(f.Game, "_RDR2_ModHolster", h.SessionId, "manifest.xml"), "Holstering", "Copying");
            var r = e.Restore(h.SessionId); Assert(r.Success, "intact active source after precommit interruption was not recoverable: " + r.Message); Assert(File.Exists(Path.Combine(f.Game, "x.asi")), "intact source was lost");
        }
    }

    static void PurgingState()
    {
        using (var f = new Fixture("purging"))
        {
            File.WriteAllText(Path.Combine(f.Game, "x.asi"), "x"); var e = f.Engine(); var h = Require(e.Holster(ModGroup.Mods, new[] { Pick(e, "x.asi") }));
            string manifest = Path.Combine(f.Game, "_RDR2_ModHolster", h.SessionId, "manifest.xml"); SetManifestState(manifest, "Purging", "BackedUp");
            Assert(!e.Restore(h.SessionId).Success, "Purging session was restorable"); var r = e.Purge(h.SessionId); Assert(r.Success, "verified retry of Purging did not complete: " + r.Message);
        }
    }

    static void WindowsOnlyFilesystemBehavior()
    {
        if (!OperatingSystem.IsWindows()) throw new SkipTest("readonly/sharing semantics are Windows-only; Linux result is intentionally SKIP");
        using (var f = new Fixture("windows-sharing"))
        {
            string file = Path.Combine(f.Game, "locked.asi"); File.WriteAllText(file, "locked fixture");
            var e = f.Engine(); var candidate = Pick(e, "locked.asi");
            using (var held = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Assert(!e.Holster(ModGroup.Mods, new[] { candidate }).Success, "sharing violation was ignored");
            Assert(File.ReadAllText(file) == "locked fixture", "locked original was changed");
            Assert(e.GetSessions(ModGroup.Mods).Count == 0, "failed inventory left an orphan session");
        }
        using (var f = new Fixture("windows-readonly"))
        {
            string file = Path.Combine(f.Game, "readonly.asi"); File.WriteAllText(file, "readonly fixture");
            File.SetAttributes(file, FileAttributes.ReadOnly);
            try
            {
                var e = f.Engine(); var h = e.Holster(ModGroup.Mods, new[] { Pick(e, "readonly.asi") });
                Require(h);
                Assert(!File.Exists(file), "readonly source remained active after holster");
                Assert(!String.IsNullOrEmpty(h.SessionId), "backup session was not retained");
                string backup = SessionItem(f, h.SessionId, "Game", "readonly.asi");
                Assert(File.ReadAllText(backup) == "readonly fixture", "readonly backup changed");
                Assert((File.GetAttributes(backup) & FileAttributes.ReadOnly) != 0, "backup readonly attribute was not preserved");
                Require(e.Restore(h.SessionId));
                Assert(File.ReadAllText(file) == "readonly fixture", "readonly roundtrip changed content");
                Assert((File.GetAttributes(file) & FileAttributes.ReadOnly) != 0, "restored readonly attribute was not preserved");
            }
            finally
            {
                // Only synthetic fixtures: remove attributes so test cleanup can run.
                foreach (string item in Directory.GetFiles(f.Game, "*", SearchOption.AllDirectories))
                    File.SetAttributes(item, FileAttributes.Normal);
            }
        }
    }

    // ---- Small black-box helpers ----
    static Candidate Pick(SafetyEngine e, string name)
    {
        var item = e.Scan().Items.FirstOrDefault(x => x.Name == name);
        if (item == null) throw new Exception("candidate not found: " + name);
        return item;
    }
    static Candidate Find(List<Candidate> items, string name) => items.FirstOrDefault(x => x.Name == name) ?? throw new Exception("candidate not found: " + name);
    static OperationResult Require(OperationResult r) { if (!r.Success) throw new Exception(r.Message); return r; }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    static string SessionItem(Fixture f, string id, string ns, string name) => Path.Combine(f.Game, "_RDR2_ModHolster", id, ns, name.Replace('/', Path.DirectorySeparatorChar));
    static IEnumerable<string> SessionDirectories(Fixture f)
    {
        string store = Path.Combine(f.Game, "_RDR2_ModHolster");
        return Directory.Exists(store) ? Directory.GetDirectories(store).Where(x => !new[] { "logs", ".staging" }.Contains(Path.GetFileName(x), StringComparer.OrdinalIgnoreCase)) : Enumerable.Empty<string>();
    }
    static void SetManifestState(string path, string sessionState, string itemState)
    {
        var doc = XDocument.Load(path); doc.Root.SetAttributeValue("state", sessionState); foreach (var item in doc.Root.Elements("Item")) item.SetAttributeValue("state", itemState); doc.Save(path);
    }
    static string Snapshot(string root)
    {
        var all = new List<string>();
        if (!Directory.Exists(root)) return "<missing>";
        foreach (string d in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal)) all.Add("D:" + Relative(root, d));
        foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal)) all.Add("F:" + Relative(root, file) + ":" + Convert.ToBase64String(File.ReadAllBytes(file)));
        return string.Join("\n", all);
    }
    static string Relative(string root, string path) => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
    static string Device(string path)
    {
        var psi = new ProcessStartInfo("stat") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("-c"); psi.ArgumentList.Add("%d"); psi.ArgumentList.Add(path);
        using (var p = Process.Start(psi)) { string output = p.StandardOutput.ReadToEnd().Trim(); p.WaitForExit(); if (p.ExitCode != 0) throw new SkipTest("stat could not identify filesystem devices"); return output; }
    }
}
