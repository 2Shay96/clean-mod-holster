using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using RDR2ModHolster.Safety;

// Tests for the behaviour introduced in v3.2.  Each one reproduces a situation that
// left v3.1 stuck, and asserts both the new outcome and that nothing was lost.
static class V32
{
    static string baseDir;

    public static void All(Action<string, Action> run, string root)
    {
        baseDir = root;
        run("v3.2 read-only files survive a holster and restore", ReadOnlyRoundTrip);
        run("v3.2 every restore blocker is reported at once", AllConflictsReported);
        run("v3.2 conflicting files move aside and the restore then finishes", MoveAsideUnblocksRestore);
        run("v3.2 move aside refuses when the backup itself is damaged", MoveAsideRefusesBackupDamage);
        run("v3.2 move aside never touches protected content", MoveAsideProtected);
        run("v3.2 quarantine session does not block a later holster", QuarantineDoesNotBlock);
        run("v3.2 stray folder in the store is reported, not fatal", StrayFolderTolerated);
        run("v3.2 damaged finished session is skipped", DamagedFinishedSessionSkipped);
        run("v3.2 moved game folder restores only with consent", MovedGameFolderRebind);
        run("v3.2 redirected Documents folder rebinds", MovedDocumentsRebind);
        run("v3.2 oversized manifest is refused before anything moves", ManifestLimitRefused);
        run("v3.2 free space shortfall is refused before anything moves", FreeSpaceRefused);
        run("v3.2 NLog.dll is a mod only when LML is present", NLogConditional);
        run("v3.2 v3.1 GUID sessions and schema 3.1 manifests still restore", LegacySchemaCompatibility);
        run("v3.2 session and staging paths stay short", ShortPaths);
        run("v3.2 progress is reported during a holster", ProgressReported);
    }

    // ---------- helpers ----------
    static string Fix(string n)
    {
        string p = Path.Combine(baseDir, n);
        Directory.CreateDirectory(Path.Combine(p, "game"));
        Directory.CreateDirectory(Path.Combine(p, "docs"));
        File.WriteAllText(Path.Combine(p, "game", "RDR2.exe"), "synthetic");
        return p;
    }
    static SafetyEngine E(string f) { return new SafetyEngine(Path.Combine(f, "game"), Path.Combine(f, "docs"), null); }
    static SafetyEngine E(string game, string docs) { return new SafetyEngine(game, docs, null); }
    static Candidate C(SafetyEngine e, string name)
    {
        Candidate x = e.Scan().Items.FirstOrDefault(z => z.Name == name);
        if (x == null) throw new Exception("candidate missing " + name);
        return x;
    }
    static void Yes(OperationResult r) { if (!r.Success) throw new Exception("unexpected failure: " + r.Message); }
    static void No(OperationResult r, string why) { if (r.Success) throw new Exception("expected refusal: " + why); }
    static void True(bool condition, string why) { if (!condition) throw new Exception(why); }
    static string Store(string f) { return Path.Combine(f, "game", "_RDR2_ModHolster31"); }
    static string ManifestPath(string f, string id) { return Path.Combine(Store(f), id, "manifest.xml"); }
    static string Game(string f, params string[] parts) { return Path.Combine(new[] { f, "game" }.Concat(parts).ToArray()); }

    // ---------- tests ----------
    static void ReadOnlyRoundTrip()
    {
        string f = Fix("v32-readonly");
        string dir = Game(f, "scripts");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "locked.txt"), "payload");
        File.SetAttributes(Path.Combine(dir, "locked.txt"), File.GetAttributes(Path.Combine(dir, "locked.txt")) | FileAttributes.ReadOnly);
        File.WriteAllText(Game(f, "solo.asi"), "solo");
        File.SetAttributes(Game(f, "solo.asi"), File.GetAttributes(Game(f, "solo.asi")) | FileAttributes.ReadOnly);
        SafetyEngine e = E(f);
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "scripts"), C(e, "solo.asi") });
        Yes(h);
        True(!Directory.Exists(dir) && !File.Exists(Game(f, "solo.asi")), "read-only sources were not removed");
        string backup = Path.Combine(Store(f), h.SessionId, "Game", "solo.asi");
        True((File.GetAttributes(backup) & FileAttributes.ReadOnly) != 0, "read-only attribute was not preserved in the backup");
        Yes(e.Restore(h.SessionId));
        True(File.ReadAllText(Path.Combine(dir, "locked.txt")) == "payload" && File.ReadAllText(Game(f, "solo.asi")) == "solo", "read-only payload did not survive the round trip");
        True((File.GetAttributes(Game(f, "solo.asi")) & FileAttributes.ReadOnly) != 0, "read-only attribute was not restored");
        // And the backup copy, also read-only, can be permanently deleted.
        OperationResult h2 = e.Holster(ModGroup.Mods, new[] { C(e, "solo.asi") });
        Yes(h2); Yes(e.Purge(h2.SessionId));
    }

    static void AllConflictsReported()
    {
        string f = Fix("v32-allconflicts");
        foreach (string n in new[] { "a.asi", "b.asi", "c.asi" }) File.WriteAllText(Game(f, n), n);
        SafetyEngine e = E(f);
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "a.asi"), C(e, "b.asi"), C(e, "c.asi") });
        Yes(h);
        foreach (string n in new[] { "a.asi", "b.asi", "c.asi" }) File.WriteAllText(Game(f, n), "reinstalled");
        OperationResult r = e.Restore(h.SessionId);
        No(r, "three destinations are occupied");
        True(r.Conflicts.Count == 3, "expected 3 conflicts, got " + r.Conflicts.Count);
        foreach (string n in new[] { "a.asi", "b.asi", "c.asi" }) True(r.Message.Contains(n), "conflict message omits " + n);
        True(r.Conflicts.All(x => x.CanMoveAside), "occupied destinations should be movable");
        RestorePlan plan = e.InspectRestore(h.SessionId);
        True(!plan.Ready && plan.CanMoveAside && plan.Conflicts.Count == 3, "InspectRestore disagrees with Restore");
        foreach (string n in new[] { "a.asi", "b.asi", "c.asi" })
            True(File.ReadAllText(Game(f, n)) == "reinstalled" && File.Exists(Path.Combine(Store(f), h.SessionId, "Game", n)), "a blocked restore changed something");
    }

    // The reported real-world case: mods holstered for online play, LML reinstalled
    // while they were away, restore blocked.  v3.1 had no way out of this.
    static void MoveAsideUnblocksRestore()
    {
        string f = Fix("v32-moveaside");
        Directory.CreateDirectory(Game(f, "lml"));
        File.WriteAllText(Game(f, "lml", "mod.xml"), "original");
        File.WriteAllText(Game(f, "vfs.asi"), "original loader");
        File.WriteAllText(Game(f, "untouched.asi"), "still mine");
        SafetyEngine e = E(f);
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "lml"), C(e, "vfs.asi"), C(e, "untouched.asi") });
        Yes(h);
        // A fresh LML install recreates two of the three names.
        Directory.CreateDirectory(Game(f, "lml"));
        File.WriteAllText(Game(f, "lml", "mod.xml"), "freshly installed");
        File.WriteAllText(Game(f, "vfs.asi"), "fresh loader");
        No(e.Restore(h.SessionId), "two destinations are occupied");
        OperationResult aside = e.MoveConflictsAside(h.SessionId);
        Yes(aside);
        True(!Directory.Exists(Game(f, "lml")) && !File.Exists(Game(f, "vfs.asi")), "conflicting files were not moved aside");
        Yes(e.Restore(h.SessionId));
        True(File.ReadAllText(Game(f, "lml", "mod.xml")) == "original", "original mod content was not restored");
        True(File.ReadAllText(Game(f, "vfs.asi")) == "original loader", "original loader was not restored");
        True(File.ReadAllText(Game(f, "untouched.asi")) == "still mine", "unaffected item was not restored");
        // The displaced files are not lost: they are their own recoverable session.
        List<SessionInfo> quarantine = e.GetSessions(ModGroup.Mods).Where(x => x.Kind == SessionKind.Quarantine).ToList();
        True(quarantine.Count == 1 && quarantine[0].ItemCount == 2, "displaced files are not recoverable");
        string payload = Path.Combine(quarantine[0].Folder, "Game", "vfs.asi");
        True(File.ReadAllText(payload) == "fresh loader", "displaced file content was not preserved");
    }

    static void MoveAsideRefusesBackupDamage()
    {
        string f = Fix("v32-asidedamage");
        File.WriteAllText(Game(f, "a.asi"), "a");
        File.WriteAllText(Game(f, "b.asi"), "b");
        SafetyEngine e = E(f);
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "a.asi"), C(e, "b.asi") });
        Yes(h);
        File.WriteAllText(Game(f, "a.asi"), "in the way");           // fixable by moving aside
        File.WriteAllText(Path.Combine(Store(f), h.SessionId, "Game", "b.asi"), "corrupted backup"); // not
        OperationResult aside = e.MoveConflictsAside(h.SessionId);
        No(aside, "the backup itself is damaged");
        True(File.ReadAllText(Game(f, "a.asi")) == "in the way", "a refused repair moved a file anyway");
        True(aside.Conflicts.Any(x => x.Name == "b.asi" && !x.CanMoveAside), "the damaged backup was not identified");
    }

    static void MoveAsideProtected()
    {
        string f = Fix("v32-asideprotected");
        File.WriteAllText(Game(f, "a.asi"), "a");
        SafetyEngine e = E(f);
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "a.asi") });
        Yes(h);
        // Forge a manifest item name that is protected, then occupy that destination.
        string mp = ManifestPath(f, h.SessionId);
        File.WriteAllText(mp, File.ReadAllText(mp).Replace("name=\"a.asi\"", "name=\"steam_api64.dll\""));
        File.WriteAllText(Game(f, "steam_api64.dll"), "real launcher file");
        OperationResult aside = e.MoveConflictsAside(h.SessionId);
        No(aside, "manifest naming protected content must be rejected");
        True(File.ReadAllText(Game(f, "steam_api64.dll")) == "real launcher file", "protected content was moved");
    }

    static void QuarantineDoesNotBlock()
    {
        string f = Fix("v32-quarantine");
        File.WriteAllText(Game(f, "a.asi"), "a");
        SafetyEngine e = E(f);
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "a.asi") });
        Yes(h);
        File.WriteAllText(Game(f, "a.asi"), "in the way");
        Yes(e.MoveConflictsAside(h.SessionId));
        Yes(e.Restore(h.SessionId));
        // A leftover quarantine session must not stop the next holster.
        OperationResult again = e.Holster(ModGroup.Mods, new[] { C(e, "a.asi") });
        Yes(again);
        True(e.GetSessions(ModGroup.Mods).Count(x => x.Kind == SessionKind.Session) == 1, "unexpected session count");
        // A second holster while a real session is open is still refused.
        File.WriteAllText(Game(f, "b.asi"), "b");
        No(e.Holster(ModGroup.Mods, new[] { C(e, "b.asi") }), "an open session must still block a new holster");
    }

    static void StrayFolderTolerated()
    {
        string f = Fix("v32-stray");
        File.WriteAllText(Game(f, "a.asi"), "a");
        SafetyEngine e = E(f);
        Directory.CreateDirectory(Path.Combine(Store(f), "leftover-folder"));
        ScanResult scan = e.Scan();
        True(scan.Notes.Any(x => x.Contains("leftover-folder")), "stray folder was not reported");
        True(scan.Warnings.Count == 0, "a stray folder must not be a blocking warning");
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "a.asi") });
        Yes(h);
        Yes(e.Restore(h.SessionId));
        True(Directory.Exists(Path.Combine(Store(f), "leftover-folder")), "the stray folder was touched");
    }

    static void DamagedFinishedSessionSkipped()
    {
        string f = Fix("v32-damaged");
        File.WriteAllText(Game(f, "a.asi"), "a");
        SafetyEngine e = E(f);
        OperationResult first = e.Holster(ModGroup.Mods, new[] { C(e, "a.asi") });
        Yes(first); Yes(e.Restore(first.SessionId));
        File.WriteAllText(ManifestPath(f, first.SessionId), "<ModHolster schema=\"3.2\"><truncated");
        True(e.Scan().Notes.Any(x => x.Contains(first.SessionId)), "damaged session was not reported");
        OperationResult second = e.Holster(ModGroup.Mods, new[] { C(e, "a.asi") });
        Yes(second);
        Yes(e.Restore(second.SessionId));
        True(File.ReadAllText(Game(f, "a.asi")) == "a", "payload lost while a damaged session was present");
        // Acting on the damaged session itself is still an error.
        No(e.Restore(first.SessionId), "a damaged session cannot be restored");
    }

    static void MovedGameFolderRebind()
    {
        string f = Fix("v32-moved");
        Directory.CreateDirectory(Game(f, "lml"));
        File.WriteAllText(Game(f, "lml", "a.xml"), "payload");
        SafetyEngine e = E(f);
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "lml") });
        Yes(h);
        string moved = Path.Combine(f, "game-moved");
        Directory.Move(Path.Combine(f, "game"), moved);
        SafetyEngine e2 = E(moved, Path.Combine(f, "docs"));
        List<SessionInfo> sessions = e2.GetSessions(ModGroup.Mods);
        True(sessions.Count == 1 && sessions[0].NeedsRebind, "a moved game folder should list the session and flag it");
        OperationResult refused = e2.Restore(h.SessionId);
        No(refused, "restoring into a moved folder needs consent");
        True(refused.NeedsRebind && refused.Message.Contains(Path.Combine(f, "game")), "the recorded folder was not explained");
        Yes(e2.Restore(h.SessionId, true));
        True(File.ReadAllText(Path.Combine(moved, "lml", "a.xml")) == "payload", "payload did not follow the move");
        // The rebound record now names the current folder.
        True(!e2.GetSessions(ModGroup.Mods).Any(), "session should be finished");
    }

    static void MovedDocumentsRebind()
    {
        string f = Fix("v32-docsmoved");
        string docs = Path.Combine(f, "docs"), modconfig = Path.Combine(docs, "modconfig");
        Directory.CreateDirectory(modconfig);
        File.WriteAllText(Path.Combine(modconfig, "a.xml"), "cfg");
        File.WriteAllText(Game(f, "a.asi"), "a");
        SafetyEngine e = E(f);
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "Documents/modconfig"), C(e, "a.asi") });
        Yes(h);
        string redirected = Path.Combine(f, "docs-onedrive");
        Directory.Move(docs, redirected);
        SafetyEngine e2 = E(Path.Combine(f, "game"), redirected);
        No(e2.Restore(h.SessionId), "a redirected Documents folder needs consent");
        Yes(e2.Restore(h.SessionId, true));
        True(File.ReadAllText(Path.Combine(redirected, "modconfig", "a.xml")) == "cfg", "modconfig did not follow the redirection");
        True(File.ReadAllText(Game(f, "a.asi")) == "a", "game item was not restored");
    }

    static void ManifestLimitRefused()
    {
        string f = Fix("v32-manifestlimit");
        string dir = Game(f, "scripts");
        Directory.CreateDirectory(dir);
        for (int i = 0; i < 40; i++) File.WriteAllText(Path.Combine(dir, "file" + i + ".txt"), "x");
        SafetyEngine e = E(f);
        SafetyEngine.TestManifestLimit = 2048;
        OperationResult h;
        try { h = e.Holster(ModGroup.Mods, new[] { C(e, "scripts") }); }
        finally { SafetyEngine.TestManifestLimit = 0; }
        No(h, "a record too large to read back must not be written");
        True(Directory.Exists(dir) && Directory.GetFiles(dir).Length == 40, "a refused holster moved files");
        True(!Directory.GetDirectories(Store(f)).Any(x => !Path.GetFileName(x).Equals("logs")), "a refused holster left a session folder");
        // Without the test limit the same selection is fine.
        Yes(e.Holster(ModGroup.Mods, new[] { C(e, "scripts") }));
    }

    static void FreeSpaceRefused()
    {
        string f = Fix("v32-freespace");
        string dir = Game(f, "scripts");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "big.dat"), new string('x', 4096));
        SafetyEngine e = E(f);
        SafetyEngine.TestFreeSpaceBytes = 1024;
        OperationResult h;
        try { h = e.Holster(ModGroup.Mods, new[] { C(e, "scripts") }); }
        finally { SafetyEngine.TestFreeSpaceBytes = 0; }
        No(h, "a holster without room for the verified copy must be refused");
        True(h.Message.Contains("free space"), "the message does not explain the shortfall: " + h.Message);
        True(File.Exists(Path.Combine(dir, "big.dat")), "a refused holster moved files");
        True(!Directory.GetDirectories(Store(f)).Any(x => !Path.GetFileName(x).Equals("logs")), "a refused holster left a session folder");
    }

    static void NLogConditional()
    {
        string withLml = Fix("v32-nlog-lml");
        File.WriteAllText(Path.Combine(withLml, "game", "NLog.dll"), "nlog");
        File.WriteAllText(Path.Combine(withLml, "game", "vfs.asi"), "loader");
        True(C(E(withLml), "NLog.dll").Recommended, "NLog.dll should be recognised when LML is present");

        string without = Fix("v32-nlog-plain");
        File.WriteAllText(Path.Combine(without, "game", "NLog.dll"), "nlog");
        Candidate c = C(E(without), "NLog.dll");
        True(!c.Recommended && !c.Protected, "NLog.dll must stay unknown and untouched without LML markers");
    }

    // A player who upgrades mid-session must not be locked out of their v3.1 backup.
    static void LegacySchemaCompatibility()
    {
        string f = Fix("v32-schema31");
        File.WriteAllText(Game(f, "a.asi"), "a");
        SafetyEngine e = E(f);
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "a.asi") });
        Yes(h);
        string oldId = Guid.NewGuid().ToString("D");
        string from = Path.Combine(Store(f), h.SessionId), to = Path.Combine(Store(f), oldId);
        Directory.Move(from, to);
        XDocument doc = XDocument.Load(Path.Combine(to, "manifest.xml"));
        doc.Root.SetAttributeValue("schema", "3.1");
        doc.Root.SetAttributeValue("id", oldId);
        doc.Root.Attribute("kind").Remove();           // v3.1 manifests have no kind
        File.Delete(Path.Combine(to, "manifest.xml.previous"));
        doc.Save(Path.Combine(to, "manifest.xml"));
        List<SessionInfo> sessions = e.GetSessions(ModGroup.Mods);
        True(sessions.Count == 1 && sessions[0].Id == oldId && sessions[0].Kind == SessionKind.Session, "a v3.1 session was not readable");
        Yes(e.Restore(oldId));
        True(File.ReadAllText(Game(f, "a.asi")) == "a", "a v3.1 backup did not restore");
    }

    static void ShortPaths()
    {
        string f = Fix("v32-shortpaths");
        Directory.CreateDirectory(Game(f, "lml"));
        File.WriteAllText(Game(f, "lml", "a.xml"), "payload");
        SafetyEngine e = E(f);
        OperationResult h = e.Holster(ModGroup.Mods, new[] { C(e, "lml") });
        Yes(h);
        True(h.SessionId.Length == 16, "session id should be 16 characters, was " + h.SessionId.Length);
        // v3.1 used "<36-char guid>\.staging\backup\<32-char name>" = 78 characters of prefix.
        int prefix = h.SessionId.Length + 1 + 1 + 1 + 8;   // id \ s \ 8-char stage
        True(prefix <= 32, "staging prefix grew to " + prefix + " characters");
    }

    static void ProgressReported()
    {
        string f = Fix("v32-progress");
        string dir = Game(f, "scripts");
        Directory.CreateDirectory(dir);
        for (int i = 0; i < 8; i++) File.WriteAllBytes(Path.Combine(dir, "f" + i + ".dat"), new byte[512 * 1024]);
        SafetyEngine e = E(f);
        var phases = new List<string>();
        long maxDone = 0, seenTotal = 0;
        e.Progress = delegate(string phase, long done, long total)
        {
            if (!String.IsNullOrEmpty(phase) && (phases.Count == 0 || phases[phases.Count - 1] != phase)) phases.Add(phase);
            if (done > maxDone) maxDone = done;
            if (total > seenTotal) seenTotal = total;
        };
        Yes(e.Holster(ModGroup.Mods, new[] { C(e, "scripts") }));
        True(phases.Count >= 2, "expected at least two progress phases, saw " + phases.Count);
        True(seenTotal == 8L * 512 * 1024, "progress total should be the payload size, was " + seenTotal);
        True(maxDone > 0, "no progress was reported");
    }
}
