using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace RDR2ModHolster.Safety
{
    public enum ModGroup { Mods, ReShade, ENB }
    public enum SessionKind { Session, Quarantine }
    public class Candidate { public string Name, FullPath, Reason; public bool IsDirectory, Recommended, Protected; public ModGroup Group; }
    public class ScanResult { public List<Candidate> Items = new List<Candidate>(); public List<string> Warnings = new List<string>(); public List<string> Notes = new List<string>(); }
    public class SessionInfo { public string Id, State; public ModGroup Group; public int ItemCount; public string Folder; public bool IsLegacy; public SessionKind Kind; public bool NeedsRebind; public string RecordedGame, RecordedDocuments; public long PayloadBytes; }
    public class OperationResult { public bool Success; public string Message; public string SessionId; public string SessionPath; public List<string> Errors = new List<string>(); public List<string> Notes = new List<string>(); public List<RestoreConflict> Conflicts = new List<RestoreConflict>(); public bool NeedsRebind; }
    public class RestoreConflict { public string Name, Path, Reason; public bool CanMoveAside; }
    public class RestorePlan
    {
        public string SessionId; public bool NeedsRebind; public string RecordedGame, RecordedDocuments;
        public List<RestoreConflict> Conflicts = new List<RestoreConflict>();
        public bool Ready { get { return Conflicts.Count == 0; } }
        public bool CanMoveAside { get { return Conflicts.Count > 0 && Conflicts.All(x => x.CanMoveAside); } }
    }
    public class ConflictException : IOException
    {
        public readonly List<RestoreConflict> Conflicts;
        public ConflictException(string message, List<RestoreConflict> conflicts) : base(message) { Conflicts = conflicts ?? new List<RestoreConflict>(); }
    }

    // This deliberately uses the .NET Framework 4.8 BCL.  Transfers are copy-to-private-stage,
    // verify, then same-volume no-overwrite moves; a failed stage is deliberately retained.
    //
    // v3.2 changes, all of which keep the v3.1 no-overwrite / journal-first guarantees:
    //   * read-only entries are cleared before deletion (Windows refuses to delete them otherwise)
    //   * every restore blocker is reported together, and destinations in the way can be moved aside
    //   * an unreadable session folder is reported, not treated as a reason to disable the tool
    //   * a session whose recorded roots have moved can be rebound with explicit consent
    //   * free space is checked up front, progress is reported, and redundant hash passes are gone
    //   * shorter session/staging paths, and a manifest size guard on write as well as read
    public sealed class SafetyEngine
    {
        public const string RecoveryFolderName = "_RDR2_ModHolster";
        const string ToolFolder = RecoveryFolderName, PreviousToolFolder = "_RDR2_ModHolster31", Schema = "3.2";
        static readonly string[] ReadableSchemas = { "3.1", "3.2" };
        const long MaxManifestBytes = 64 * 1024 * 1024;
        static long ManifestLimit
        {
            get
            {
#if HOLSTER_TESTS
                if (TestManifestLimit > 0) return TestManifestLimit;
#endif
                return MaxManifestBytes;
            }
        }
        readonly string _documents;
        readonly Action<string> _log;
        public string GameRoot { get; private set; }
        public string BackupRoot { get; private set; }
        // phase text, bytes done, bytes total.  Purely informational; never affects a decision.
        public Action<string, long, long> Progress;
        public List<string> StoreNotes = new List<string>();

        sealed class FileMark { public string Rel; public bool Dir; public long Size; public string Hash; public long Ticks; public int Attributes; }
        sealed class Item { public string Ns, Name, State; public bool Dir; public List<FileMark> Marks = new List<FileMark>(); public long Bytes { get { return Marks.Where(x => !x.Dir).Sum(x => x.Size); } } }
        sealed class Manifest
        {
            public string Id, State, Game, Documents; public ModGroup Group; public bool Legacy; public SessionKind Kind;
            public List<Item> Items = new List<Item>();
            public bool GameMoved, DocumentsMoved;
            public bool NeedsRebind { get { return GameMoved || (DocumentsMoved && Items.Any(x => x.Ns == "Documents")); } }
            public long Bytes { get { return Items.Sum(x => x.Bytes); } }
        }
        sealed class Entry { public string Name, Path; public bool IsDirectory; }
        // One inventory pass answers both "is this a subset of the record" and "is this the whole record".
        sealed class Probe { public bool Exists, SubsetOk, Exact; public string Reason; }
        enum Check { Proceed, DestinationInTheWay, BackupProblem }
        sealed class ItemCheck { public Item Item; public Check Status; public string Reason; public string Path; }

        public SafetyEngine(string gameRoot, string documentsGameRoot, Action<string> log)
        {
            _log = log ?? delegate(string s) { };
            GameRoot = CanonicalExistingRoot(gameRoot, "game folder");
            string exe = Path.Combine(GameRoot, "RDR2.exe");
            if (!ExistsAny(exe) || Directory.Exists(exe)) throw new InvalidOperationException("The selected game folder does not contain RDR2.exe.");
            RejectLink(exe);
            _documents = CanonicalOptionalRoot(documentsGameRoot, "Documents game folder");
            if (IsNestedOrSame(GameRoot, _documents) || IsNestedOrSame(_documents, GameRoot))
                throw new ArgumentException("Game and Documents game folders must be separate, non-overlapping folders.");
            BackupRoot = Path.Combine(GameRoot, ToolFolder);
            if (ExistsAny(BackupRoot)) RejectLink(BackupRoot); else Directory.CreateDirectory(BackupRoot);
            RejectLinkAncestors(BackupRoot);
            try { Directory.CreateDirectory(Path.Combine(BackupRoot, "logs")); Log("Safety engine opened."); }
            catch (Exception ex) { throw new IOException("Cannot create the safety journal/log folder. No changes were made.", ex); }
        }

        public ScanResult Scan()
        {
            CheckRoots(); var r = new ScanResult();
            try
            {
                var entries = DirectEntries(GameRoot).Where(x => !Eq(x.Name, ToolFolder) && !Eq(x.Name, PreviousToolFolder)).ToList();
                bool asi = entries.Any(x => !x.IsDirectory && (Ends(x.Name, ".asi") || Eq(x.Name, "ScriptHookRDR2.dll")));
                bool reshade = entries.Any(x => Eq(x.Name, "ReShade.ini") || Eq(x.Name, "ReShade64.dll") || Eq(x.Name, "reshade-shaders"));
                bool enb = entries.Any(x => Eq(x.Name, "enbseries.ini") || Eq(x.Name, "enblocal.ini") || Eq(x.Name, "enbseries"));
                // Larry's Mod Loader marker set; used only to recognise its own support files.
                bool lml = entries.Any(x => (x.IsDirectory && Eq(x.Name, "lml")) || (!x.IsDirectory && (Eq(x.Name, "lml.ini") || Eq(x.Name, "vfs.asi") || Eq(x.Name, "ModManager.Core.dll") || Eq(x.Name, "ModManager.NativeInterop.dll"))));
                foreach (Entry e in entries) { RejectLink(e.Path); r.Items.Add(Classify(e, asi, reshade, enb, lml)); }
                string modconfig = Path.Combine(_documents, "modconfig");
                if (ExistsAny(modconfig))
                {
                    RejectLinkTree(modconfig);
                    r.Items.Add(new Candidate { Name = "Documents/modconfig", FullPath = modconfig, IsDirectory = Directory.Exists(modconfig), Recommended = true, Group = ModGroup.Mods, Reason = "Mod configuration" });
                }
            }
            catch (Exception ex) { throw new IOException("Scan stopped without making changes because the folder could not be safely enumerated: " + ExplainError(ex), ex); }
            GetSessions(ModGroup.Mods); // refreshes StoreNotes without blocking on anything it cannot read
            r.Notes.AddRange(StoreNotes);
            return r;
        }

        // Unreadable or unexpected entries in the store are reported, never a reason to refuse
        // every operation.  Only acting on a specific damaged session is an error.
        public List<SessionInfo> GetSessions(ModGroup group)
        {
            CheckRoots(); var result = new List<SessionInfo>(); StoreNotes = new List<string>();
            var folders = StoreRoots().SelectMany(Directory.GetDirectories).OrderBy(x => x, StringComparer.Ordinal).ToList();
            if (folders.Where(x => ValidSessionId(Path.GetFileName(x))).GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1))
                throw new IOException("The same session ID exists in both recovery folders. Keep both copies and resolve the duplicate before continuing; neither copy was changed.");
            foreach (string d in folders)
            {
                string name = Path.GetFileName(d);
                if (Eq(name, "logs")) continue;
                string manifest = Path.Combine(d, "manifest.xml");
                if (!File.Exists(manifest)) { StoreNotes.Add("Folder without a session record, left untouched: " + name); continue; }
                Manifest m;
                try { m = LoadManifest(manifest, d); }
                catch (Exception ex) { StoreNotes.Add("Session " + name + " could not be read and was skipped: " + ExplainError(ex)); continue; }
                if (m.Group == group && m.State != "Restored" && m.State != "Purged") result.Add(Info(m, d));
            }
            return result.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        }

        public OperationResult Holster(ModGroup group, IEnumerable<Candidate> selected)
        {
            return HolsterCore(group, selected, SessionKind.Session, null);
        }

        OperationResult HolsterCore(ModGroup group, IEnumerable<Candidate> selected, SessionKind kind, string note)
        {
            string id = null, folder = null; bool published = false;
            try
            {
                CheckRoots(); using (AcquireLock())
                {
                    CheckRunning();
                    if (kind == SessionKind.Session && GetSessions(group).Any(x => x.Kind == SessionKind.Session))
                        return Fail(null, "An unfinished " + group + " session exists. Restore it before holstering again.");
                    ScanResult fresh = Scan(); var requested = (selected ?? Enumerable.Empty<Candidate>()).ToList();
                    if (requested.Count == 0) return Fail(null, "Choose at least one safe item to holster.");
                    var chosen = new List<Candidate>();
                    foreach (Candidate input in requested)
                    {
                        Candidate actual = fresh.Items.FirstOrDefault(x => input != null && PathEq(x.FullPath, input.FullPath));
                        if (actual == null || actual.IsDirectory != input.IsDirectory || actual.Group != group)
                            return Fail(null, "A selected item no longer matches a fresh scan. Scan again before continuing.");
                        if (actual.Protected) return Fail(null, "Protected game or launcher content cannot be holstered: " + actual.Name);
                        if (chosen.Any(x => PathEq(x.FullPath, actual.FullPath))) return Fail(null, "The same item was selected more than once.");
                        chosen.Add(actual);
                    }
                    RejectOverlaps(chosen.Select(x => x.FullPath));
                    // All expensive and fallible input checks happen before a session folder is created.
                    var m = new Manifest { Id = NewSessionId(), State = "Holstering", Group = group, Game = GameRoot, Documents = _documents, Kind = kind };
                    SetPhase("Reading and checksumming selected items", 0);
                    foreach (Candidate c in chosen)
                    {
                        string ns = PathEq(c.FullPath, Path.Combine(_documents, "modconfig")) ? "Documents" : "Game";
                        string name = ns == "Documents" ? "modconfig" : c.Name;
                        ValidateTop(name); var item = new Item { Ns = ns, Name = name, Dir = c.IsDirectory, State = "Source" };
                        item.Marks = Inventory(c.FullPath, c.IsDirectory, true); m.Items.Add(item);
                    }
                    RejectOverlaps(m.Items.Select(TargetPath));
                    foreach (Item i in m.Items) RejectLinkAncestors(Path.GetDirectoryName(BackupPath(Path.Combine(BackupRoot, m.Id), i)));
                    RequireFreeSpace(BackupRoot, m.Bytes, "a verified backup copy of the selected items");
                    id = m.Id; folder = Path.Combine(BackupRoot, id); Directory.CreateDirectory(folder);
                    SaveManifest(m, folder); published = true; // journal precedes every source mutation
                    SetPhase(kind == SessionKind.Quarantine ? "Moving conflicting files aside" : "Holstering", m.Bytes);
                    foreach (Item i in m.Items) TransferToBackup(m, folder, i);
                    m.State = "Holstered"; SaveManifest(m, folder); Log("Holstered " + m.Items.Count + " items in " + id);
                    Done();
                    return Ok(id, (note ?? "Items safely holstered.") + " Backup session: " + id, StoreNotes);
                }
            }
            catch (Exception ex)
            {
                if (!published && folder != null) TryRemoveEmpty(folder);
                Done();
                return ExceptionResult(id, ex);
            }
        }

        // Reports every blocker for a restore without changing anything.
        public RestorePlan InspectRestore(string sessionId)
        {
            CheckRoots(); string folder = SessionFolder(sessionId); Manifest m = LoadManifest(Path.Combine(folder, "manifest.xml"), folder);
            var plan = new RestorePlan { SessionId = m.Id, NeedsRebind = m.NeedsRebind, RecordedGame = m.Game, RecordedDocuments = m.Documents };
            if (m.State == "Purged" || m.State == "Purging" || m.State == "Restored") return plan;
            SetPhase("Checking backup and game folder", m.Bytes);
            foreach (ItemCheck c in CheckRestore(m, folder).Where(x => x.Status != Check.Proceed))
                plan.Conflicts.Add(new RestoreConflict { Name = c.Item.Name, Path = c.Path, Reason = c.Reason, CanMoveAside = c.Status == Check.DestinationInTheWay });
            Done();
            return plan;
        }

        public OperationResult Restore(string sessionId) { return Restore(sessionId, false); }

        public OperationResult Restore(string sessionId, bool allowRebind)
        {
            try
            {
                CheckRoots(); using (AcquireLock())
                {
                    CheckRunning(); string folder = SessionFolder(sessionId); Manifest m = LoadManifest(Path.Combine(folder, "manifest.xml"), folder);
                    if (m.State == "Purged") return Fail(m.Id, "This session was permanently purged and cannot be restored.");
                    if (m.State == "Purging") return Fail(m.Id, "This session is being or was partially purged; restoration is unsafe because backup deletion may be irreversible.");
                    if (m.State == "Restored") return Ok(m.Id, "This session is already restored.", null);
                    if (m.NeedsRebind && !allowRebind) return NeedsRebindResult(m);
                    SetPhase("Checking backup and game folder", m.Bytes);
                    PreflightRestore(m, folder); // specifically before changing Holstered to Restoring
                    if (m.NeedsRebind) { m.Game = GameRoot; m.Documents = _documents; m.GameMoved = m.DocumentsMoved = false; Log("Session " + m.Id + " rebound to the current folders with explicit consent."); }
                    // Undoing an interrupted holster must publish recoverable per-item states
                    // in the same manifest as the overall transition. A second interruption
                    // must not strand unstarted Source/Copying/RemovingSource entries.
                    if (m.State == "Holstering")
                        foreach (Item pending in m.Items) pending.State = "Restoring";
                    m.State = "Restoring"; SaveManifest(m, folder);
                    TestFault("AfterRestoreJournal");
                    RequireFreeSpace(GameRoot, m.Items.Where(x => x.Ns == "Game").Sum(x => x.Bytes), "restoring the game folder items");
                    RequireFreeSpace(_documents, m.Items.Where(x => x.Ns == "Documents").Sum(x => x.Bytes), "restoring the Documents items");
                    SetPhase("Restoring", m.Bytes);
                    foreach (Item i in m.Items) RestoreItem(m, folder, i);
                    m.State = "Restored"; SaveManifest(m, folder); Log("Restored session " + m.Id);
                    Done();
                    return Ok(m.Id, "Session restored without overwriting existing files.", StoreNotes);
                }
            }
            catch (Exception ex) { Done(); return ExceptionResult(sessionId, ex); }
        }

        // Moves the live files that are blocking a restore into their own recoverable session.
        public OperationResult MoveConflictsAside(string sessionId) { return MoveConflictsAside(sessionId, false); }

        public OperationResult MoveConflictsAside(string sessionId, bool allowRebind)
        {
            try
            {
                CheckRoots();
                string folder = SessionFolder(sessionId); Manifest m = LoadManifest(Path.Combine(folder, "manifest.xml"), folder);
                if (m.State == "Purged" || m.State == "Purging") return Fail(m.Id, "A purged or partially purged session cannot be repaired this way.");
                if (m.State == "Restored") return Ok(m.Id, "This session is already restored; nothing is in the way.", null);
                if (m.NeedsRebind && !allowRebind) return NeedsRebindResult(m);
                SetPhase("Checking backup and game folder", m.Bytes);
                List<ItemCheck> checks = CheckRestore(m, folder);
                Done();
                var blocked = checks.Where(x => x.Status == Check.BackupProblem).ToList();
                if (blocked.Count > 0)
                {
                    var result = Fail(m.Id, "Moving files aside cannot fix this session: the backup itself is incomplete or was changed. Nothing was moved.");
                    foreach (ItemCheck c in blocked) result.Conflicts.Add(new RestoreConflict { Name = c.Item.Name, Path = c.Path, Reason = c.Reason, CanMoveAside = false });
                    return result;
                }
                var inTheWay = checks.Where(x => x.Status == Check.DestinationInTheWay).ToList();
                if (inTheWay.Count == 0) return Ok(m.Id, "Nothing is in the way of this restore.", null);
                var live = new List<Candidate>();
                foreach (ItemCheck c in inTheWay)
                {
                    string path = TargetPath(c.Item);
                    if (!ExistsAny(path)) continue;
                    bool dir = Directory.Exists(path);
                    if (c.Item.Ns == "Game" && ProtectedName(Path.GetFileName(path), dir)) return Fail(m.Id, "Protected game or launcher content is in the way and will not be moved: " + c.Item.Name);
                    live.Add(new Candidate { Name = c.Item.Ns == "Documents" ? "Documents/modconfig" : c.Item.Name, FullPath = path, IsDirectory = dir, Group = m.Group });
                }
                if (live.Count == 0) return Ok(m.Id, "Nothing is in the way of this restore.", null);
                return HolsterCore(m.Group, live, SessionKind.Quarantine, live.Count + " conflicting item(s) moved aside into their own recoverable session; you can now restore " + m.Id + ".");
            }
            catch (Exception ex) { Done(); return ExceptionResult(sessionId, ex); }
        }

        public OperationResult Purge(string sessionId)
        {
            try
            {
                CheckRoots(); using (AcquireLock())
                {
                    CheckRunning(); string folder = SessionFolder(sessionId); Manifest m = LoadManifest(Path.Combine(folder, "manifest.xml"), folder);
                    if (m.Legacy) return Fail(m.Id, "Legacy payload is not purged automatically.");
                    if (m.State != "Holstered" && m.State != "Purging") return Fail(m.Id, "Only a fully Holstered session may be permanently purged.");
                    SetPhase("Verifying the backup before permanent deletion", m.Bytes);
                    VerifyPurgePayload(m, folder, m.State == "Purging");
                    if (m.State == "Holstered") { m.State = "Purging"; SaveManifest(m, folder); Log("Purge started " + m.Id); }
                    SetPhase("Permanently deleting the verified backup", m.Bytes);
                    foreach (Item i in m.Items)
                    {
                        string p = BackupPath(folder, i);
                        if (ExistsAny(p)) { VerifySubset(p, i); Log("Purging " + i.Name); DeleteVerifiedSubset(p, i, false); Log("Purged payload " + i.Name); }
                    }
                    VerifyPurgePayload(m, folder, true);
                    m.State = "Purged"; SaveManifest(m, folder); Log("Purged verified payload for " + m.Id);
                    Done();
                    return Ok(m.Id, "Verified backup payload permanently purged.", null);
                }
            }
            catch (Exception ex) { Done(); return ExceptionResult(sessionId, ex); }
        }

        public bool HasLegacy(ModGroup group)
        {
            CheckRoots(); string root = LegacyRoot(group); if (!Directory.Exists(root)) return false; RejectLinkTree(root);
            foreach (Entry e in DirectEntries(root))
            {
                if (Eq(e.Name, "Documents"))
                {
                    if (!e.IsDirectory) throw new InvalidDataException("Legacy Documents is not a folder.");
                    var children = DirectEntries(e.Path).ToList();
                    if (children.Count == 0) continue;
                    if (children.Count != 1 || !Eq(children[0].Name, "modconfig")) throw new InvalidDataException("Legacy Documents contains entries other than modconfig; refusing to guess their destination.");
                    return true;
                }
                return true;
            }
            return false;
        }

        public OperationResult RestoreLegacy(ModGroup group)
        {
            string id = null;
            try
            {
                CheckRoots(); using (AcquireLock())
                {
                    CheckRunning();
                    if (GetSessions(group).Any(x => x.Kind == SessionKind.Session)) return Fail(null, "An unfinished session already exists for " + group + ".");
                    string root = LegacyRoot(group); if (!Directory.Exists(root) || !HasLegacy(group)) return Fail(null, "No legacy " + group + " backup payload was found.");
                    var m = new Manifest { Id = NewSessionId(), State = "Holstered", Group = group, Game = GameRoot, Documents = _documents, Legacy = true, Kind = SessionKind.Session };
                    SetPhase("Reading and checksumming the legacy backup", 0);
                    foreach (Entry listed in DirectEntries(root))
                    {
                        Entry e = listed; string name = e.Name, ns = "Game";
                        if (Eq(name, "Documents"))
                        {
                            var kids = DirectEntries(e.Path).ToList(); if (kids.Count == 0) continue;
                            if (kids.Count != 1 || !Eq(kids[0].Name, "modconfig")) throw new InvalidDataException("Legacy Documents contains entries other than modconfig; refusing to guess their destination.");
                            e = kids[0]; name = "modconfig"; ns = "Documents";
                        }
                        ValidateTop(name); RejectLinkTree(e.Path);
                        if (ns == "Game" && !LegacyAllowedProtected(name, e.IsDirectory) && ProtectedName(name, e.IsDirectory))
                            throw new InvalidDataException("Suspicious protected legacy entry is not restored: " + name);
                        m.Items.Add(new Item { Ns = ns, Name = name, Dir = e.IsDirectory, State = "BackedUp", Marks = Inventory(e.Path, e.IsDirectory, true) });
                    }
                    if (m.Items.Count == 0) return Fail(null, "Legacy backup folder is empty.");
                    RejectOverlaps(m.Items.Select(TargetPath));
                    var occupied = m.Items.Where(x => ExistsAny(TargetPath(x))).ToList();
                    if (occupied.Count > 0)
                    {
                        var conflict = Fail(null, "Restore destinations already exist, so the legacy backup was left untouched: " + String.Join(", ", occupied.Select(x => x.Name)));
                        foreach (Item i in occupied) conflict.Conflicts.Add(new RestoreConflict { Name = i.Name, Path = TargetPath(i), Reason = "A file or folder with this name already exists in the game folder.", CanMoveAside = false });
                        return conflict;
                    }
                    RequireFreeSpace(GameRoot, m.Items.Where(x => x.Ns == "Game").Sum(x => x.Bytes), "restoring the legacy backup");
                    RequireFreeSpace(_documents, m.Items.Where(x => x.Ns == "Documents").Sum(x => x.Bytes), "restoring the legacy Documents items");
                    id = m.Id; string folder = Path.Combine(BackupRoot, id); Directory.CreateDirectory(folder); SaveManifest(m, folder);
                    // Preflight has completed before the journal state changes to Restoring.
                    m.State = "Restoring"; SaveManifest(m, folder);
                    SetPhase("Restoring legacy backup", m.Bytes);
                    foreach (Item i in m.Items) RestoreItem(m, folder, i);
                    m.State = "Restored"; SaveManifest(m, folder);
                    TryRemoveEmpty(Path.Combine(root, "Documents")); TryRemoveEmpty(root);
                    Done();
                    return Ok(id, "Legacy backup restored as recoverable session " + id + ".", StoreNotes);
                }
            }
            catch (Exception ex) { Done(); return ExceptionResult(id, ex); }
        }

        public static string ExplainError(Exception ex)
        {
            if (ex is ConflictException) return ex.Message;
            if (ex is PathTooLongException)
                return "A file path is too long for this Windows configuration. Stop other file changes and inspect the backup session and log before retrying Restore. Moving the game to a shorter folder may help; recovery may ask you to confirm the new location.";
            if (ex is UnauthorizedAccessException || ex is SecurityException)
                return "Access was denied. The most common cause is a read-only file or folder; after that, permissions, Controlled Folder Access/Protection History, or a security product blocking the app. Adding an antivirus exclusion is not a fix and is not recommended.";
            if (ex is IOException)
            {
                int hr = System.Runtime.InteropServices.Marshal.GetHRForException(ex) & 0xffff;
                if (hr == 32 || hr == 33) return "A file is in use by another process. Close the game, launcher, and mod manager, then retry.";
            if (hr == 112) return "The drive is out of disk space. Keep the session folder and remaining files. Free space, then use Restore to recover any interrupted operation.";
                return "File system error: " + ex.Message;
            }
            return ex.Message;
        }

        // ===== Journalling transfers =====
        void TransferToBackup(Manifest m, string folder, Item i)
        {
            string source = TargetPath(i), destination = BackupPath(folder, i);
            bool s = ExistsAny(source), d = ExistsAny(destination);
            bool resuming = i.State != "Source";
            if (s && resuming) VerifySubset(source, i);
            if (d) VerifyExact(destination, i);
            if (d && !s) { i.State = "BackedUp"; SaveManifest(m, folder); return; }
            if (s && d) // recover interrupted source deletion, but only from known session state
            {
                if (i.State != "RemovingSource" && i.State != "Copying") throw new IOException("Both source and backup exist outside a recorded transfer: " + i.Name);
                i.State = "RemovingSource"; SaveManifest(m, folder); DeleteVerifiedSubset(source, i, true);
            }
            else if (s)
            {
                if (i.State != "Source" && i.State != "Copying") throw new IOException("Unexpected source state for " + i.Name);
                i.State = "Copying"; SaveManifest(m, folder); Log("Staging backup " + i.Name);
                StageCopyAndCommit(source, destination, i, folder, "backup");
                VerifyStructure(destination, i); i.State = "RemovingSource"; SaveManifest(m, folder); Log("Removing active source " + i.Name);
                // The live source is re-checksummed before it is deleted: that is the one
                // step that is irreversible from the player's point of view.
                DeleteVerifiedSubset(source, i, true);
            }
            if (ExistsAny(source) || !ExistsAny(destination)) throw new IOException("Could not safely complete backup transfer for " + i.Name);
            i.State = "BackedUp"; SaveManifest(m, folder); Log("Backed up " + i.Name);
        }

        void RestoreItem(Manifest m, string folder, Item i)
        {
            string source = m.Legacy ? LegacyPayloadPath(m.Group, i) : BackupPath(folder, i), destination = TargetPath(i);
            bool s = ExistsAny(source), d = ExistsAny(destination);
            if (s) VerifySubset(source, i); if (d) VerifySubset(destination, i);
            i.State = "Restoring"; SaveManifest(m, folder); Log("Restoring " + i.Name);
            if (d && !s)
            {
                VerifyExact(destination, i); i.State = "Restored"; SaveManifest(m, folder); return;
            }
            if (!s) throw new IOException("No recoverable source exists for " + i.Name);
            if (!d)
            {
                StageCopyAndCommit(source, destination, i, folder, "restore");
                VerifyStructure(destination, i);
            }
            else if (!IsExact(destination, i))
            {
                // An interruption during source deletion left an unchanged subset in the live target.
                RestoreMissingFromFullSource(source, destination, i, folder);
                VerifyExact(destination, i);
            }
            else VerifyExact(destination, i);
            // A full destination and a full or verified subset source can only be a recorded restore/undo.
            // The payload now exists and was checksummed on the way in, so the copy being removed
            // here is provably redundant and does not need another full pass.
            if (ExistsAny(source)) { Log("Removing restored backup/source " + i.Name); VerifyExact(destination, i); DeleteVerifiedSubset(source, i, true); }
            if (ExistsAny(source) || !ExistsAny(destination)) throw new IOException("Could not safely complete restore for " + i.Name);
            i.State = "Restored"; SaveManifest(m, folder); Log("Restored " + i.Name);
        }

        void PreflightRestore(Manifest m, string folder)
        {
            var problems = CheckRestore(m, folder).Where(x => x.Status != Check.Proceed).ToList();
            if (problems.Count == 0) return;
            var conflicts = problems.Select(x => new RestoreConflict { Name = x.Item.Name, Path = x.Path, Reason = x.Reason, CanMoveAside = x.Status == Check.DestinationInTheWay }).ToList();
            bool allMovable = conflicts.All(x => x.CanMoveAside);
            string headline = conflicts.Count == 1
                ? "1 item is blocking this restore, so nothing was changed:"
                : conflicts.Count + " items are blocking this restore, so nothing was changed:";
            string body = String.Join("\n", conflicts.Select(x => "  • " + x.Name + " — " + x.Reason));
            string advice = allMovable
                ? "\n\nThese are files in the game folder standing where the backup needs to go. They can be moved aside into their own recoverable session, after which this restore can finish."
                : "\n\nSome of these are problems with the backup itself, so they cannot be fixed by moving game files aside. The backup was left untouched.";
            throw new ConflictException(headline + "\n" + body + advice, conflicts);
        }

        // Classifies every item without changing anything.  Semantics match v3.1 exactly;
        // the only difference is that all items are reported instead of the first failure.
        List<ItemCheck> CheckRestore(Manifest m, string folder)
        {
            var list = new List<ItemCheck>();
            foreach (Item i in m.Items)
            {
                string source = m.Legacy ? LegacyPayloadPath(m.Group, i) : BackupPath(folder, i), destination = TargetPath(i);
                Probe from = Examine(source, i), to = Examine(destination, i);
                list.Add(Classify(m, i, from, to, source, destination));
            }
            return list;
        }

        ItemCheck Classify(Manifest m, Item i, Probe from, Probe to, string source, string destination)
        {
            Func<Check, string, ItemCheck> make = delegate(Check status, string reason) { return new ItemCheck { Item = i, Status = status, Reason = reason, Path = status == Check.DestinationInTheWay ? destination : source }; };
            if (from.Exists && !from.SubsetOk) return make(Check.BackupProblem, "The backup copy changed or holds unexpected files" + Because(from.Reason) + ".");
            if (to.Exists && !to.SubsetOk) return make(Check.DestinationInTheWay, "Different content of this name is in the game folder" + Because(to.Reason) + ".");
            bool sourceFull = from.Exact, destinationFull = to.Exact;
            if (m.State == "Holstered")
            {
                // A completed holster never treats even an identical new live file as resumable.
                if (!sourceFull) return make(Check.BackupProblem, "The backup copy is incomplete.");
                if (to.Exists) return make(Check.DestinationInTheWay, "A file or folder of this name is already in the game folder.");
                return make(Check.Proceed, null);
            }
            if (m.State == "Holstering")
            {
                // Only a persisted per-item progress state authorizes recovery from a duplicate.
                if (i.State == "Source")
                {
                    if (from.Exists) return make(Check.BackupProblem, "A backup copy exists although this item was never copied.");
                    if (!destinationFull) return make(to.Exists ? Check.DestinationInTheWay : Check.BackupProblem, to.Exists ? "The item in the game folder is no longer the one recorded." : "The original is gone and no backup copy was made.");
                    return make(Check.Proceed, null);
                }
                if (i.State == "Copying")
                {
                    if (!destinationFull) return make(to.Exists ? Check.DestinationInTheWay : Check.BackupProblem, to.Exists ? "The item in the game folder is no longer the one recorded." : "An interrupted copy left no complete original.");
                    if (from.Exists && !sourceFull) return make(Check.BackupProblem, "An interrupted copy left an incomplete backup.");
                    return make(Check.Proceed, null);
                }
                if (i.State == "RemovingSource")
                {
                    if (!sourceFull) return make(Check.BackupProblem, "An interrupted removal left an incomplete backup.");
                    return make(Check.Proceed, null);
                }
                if (i.State == "BackedUp")
                {
                    if (!sourceFull) return make(Check.BackupProblem, "The backup copy is incomplete.");
                    if (to.Exists) return make(Check.DestinationInTheWay, "A file or folder of this name is already in the game folder.");
                    return make(Check.Proceed, null);
                }
                throw new InvalidDataException("Unsafe holstering item state.");
            }
            if (m.State == "Restoring")
            {
                if (i.State == "BackedUp")
                {
                    if (!sourceFull) return make(Check.BackupProblem, "The backup copy is incomplete.");
                    if (to.Exists) return make(Check.DestinationInTheWay, "A file or folder of this name is already in the game folder.");
                    return make(Check.Proceed, null);
                }
                if (i.State == "Restoring")
                {
                    if (!sourceFull && !destinationFull) return make(to.Exists ? Check.DestinationInTheWay : Check.BackupProblem, to.Exists ? "Neither the backup nor the game folder holds a complete copy." : "An interrupted restore left no complete copy.");
                    return make(Check.Proceed, null);
                }
                if (i.State == "Restored")
                {
                    if (!destinationFull) return make(to.Exists ? Check.DestinationInTheWay : Check.BackupProblem, "The restored item is no longer complete in the game folder.");
                    if (from.Exists) return make(Check.BackupProblem, "A backup copy still exists after this item was restored.");
                    return make(Check.Proceed, null);
                }
                throw new InvalidDataException("Unsafe restoring item state.");
            }
            return make(Check.BackupProblem, "Unexpected session state: " + m.State);
        }

        static string Because(string reason) { return String.IsNullOrEmpty(reason) ? "" : " (" + reason + ")"; }

        // Copies are never written directly into a final target.  The stage is left behind on error.
        void StageCopyAndCommit(string source, string destination, Item i, string folder, string operation)
        {
            if (ExistsAny(destination)) throw new IOException("Refusing to overwrite destination: " + destination);
            EnsureSafeParent(Path.GetDirectoryName(destination));
            string stageBase = StageBase(destination, folder, operation);
            EnsureSafeParent(stageBase); Directory.CreateDirectory(stageBase); RejectLinkTree(stageBase);
            string stage = Path.Combine(stageBase, ShortId(8)); Directory.CreateDirectory(stage);
            string stagedItem = Path.Combine(stage, i.Name);
            try
            {
                // CopyExact checksums every byte as it writes, so a separate verification
                // read of the stage is not needed; the structure is still checked.
                CopyExact(source, stagedItem, i);
                TestFault("AfterStageCopy");
                VerifyExact(stagedItem, i); RejectLinkAncestors(destination);
                if (ExistsAny(destination)) throw new IOException("Refusing to overwrite destination: " + destination);
                if (i.Dir) Directory.Move(stagedItem, destination); // same stage/destination volume, atomic no-overwrite rename
                else File.Move(stagedItem, destination);
                TryRemoveEmpty(stage); TryRemoveEmpty(stageBase);
            }
            catch
            {
                Log("Stage retained for manual inspection after failed " + operation + ": " + stage);
                throw;
            }
        }

        string StageBase(string destination, string folder, string operation)
        {
            // Game stages are inside the protected session; Documents stages must be on Documents volume.
            // Kept deliberately short: every extra character here counts against the Windows path limit.
            if (!IsNestedOrSame(_documents, destination)) return Path.Combine(folder, "s");
            return Path.Combine(_documents, ".RDR2MH-stage-" + Path.GetFileName(folder));
        }

        void RestoreMissingFromFullSource(string source, string destination, Item i, string folder)
        {
            if (!i.Dir) throw new IOException("A partial file destination is unsafe: " + i.Name);
            VerifyExact(source, i); VerifySubset(destination, i);
            string stageBase = StageBase(destination, folder, "restore-missing"); EnsureSafeParent(stageBase); Directory.CreateDirectory(stageBase);
            string stage = Path.Combine(stageBase, ShortId(8)); Directory.CreateDirectory(stage);
            try
            {
                // Stage the full tree, then commit only absent inventory entries without overwriting.
                string staged = Path.Combine(stage, i.Name); CopyExact(source, staged, i); VerifyStructure(staged, i);
                foreach (FileMark f in i.Marks.Where(x => x.Dir).OrderBy(x => x.Rel.Length))
                {
                    string final = Join(destination, f.Rel); if (!ExistsAny(final)) { EnsureSafeParent(Path.GetDirectoryName(final)); Directory.Move(Join(staged, f.Rel), final); }
                }
                foreach (FileMark f in i.Marks.Where(x => !x.Dir))
                {
                    string final = Join(destination, f.Rel); if (!ExistsAny(final)) { EnsureSafeParent(Path.GetDirectoryName(final)); File.Move(Join(staged, f.Rel), final); }
                }
                // The full stage also contained already-restored members. Remove only
                // its verified remaining subset, never arbitrary stage content.
                try { if (ExistsAny(staged)) DeleteVerifiedSubset(staged, i, false); }
                catch (Exception ex) { Log("Verified restore complete; staging cleanup needs manual inspection at " + stage + ": " + ExplainError(ex)); }
                TryRemoveEmpty(stage); TryRemoveEmpty(stageBase);
            }
            catch { Log("Stage retained for manual inspection after partial restore: " + stage); throw; }
        }

        // ===== inventories and manifest =====
        void CopyExact(string source, string destination, Item i)
        {
            if (ExistsAny(destination)) throw new IOException("Refusing to overwrite destination: " + destination);
            if (!i.Dir) { Directory.CreateDirectory(Path.GetDirectoryName(destination)); CopyFileVerified(source, destination, i.Marks[0]); ApplyMark(destination, i.Marks[0]); return; }
            Directory.CreateDirectory(destination);
            foreach (FileMark d in i.Marks.Where(x => x.Dir).OrderBy(x => x.Rel.Length)) Directory.CreateDirectory(Join(destination, d.Rel));
            foreach (FileMark f in i.Marks.Where(x => !x.Dir)) { string target = Join(destination, f.Rel); Directory.CreateDirectory(Path.GetDirectoryName(target)); CopyFileVerified(Join(source, f.Rel), target, f); ApplyMark(target, f); }
            foreach (FileMark d in i.Marks.Where(x => x.Dir).OrderByDescending(x => x.Rel.Length)) ApplyMark(Join(destination, d.Rel), d);
        }

        // Single read of the source: hashed and written at the same time, refusing to keep
        // a copy whose checksum or length does not match the recorded inventory.
        void CopyFileVerified(string source, string destination, FileMark mark)
        {
            byte[] buffer = new byte[1 << 20];
            string hash;
            long written = 0;
            using (SHA256 sha = SHA256.Create())
            using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length))
            using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length))
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    output.Write(buffer, 0, read); written += read; Advance(read);
                }
                sha.TransformFinalBlock(new byte[0], 0, 0);
                output.Flush(true);
                hash = Hex(sha.Hash);
            }
            if (written != mark.Size || !Eq(hash, mark.Hash))
            {
                try { ClearReadOnly(destination); File.Delete(destination); } catch { }
                throw new InvalidDataException("A file changed while it was being copied: " + source);
            }
        }

        static void ApplyMark(string path, FileMark mark)
        {
            try { File.SetLastWriteTimeUtc(path, new DateTime(mark.Ticks, DateTimeKind.Utc)); File.SetAttributes(path, (FileAttributes)mark.Attributes); } catch { /* metadata preservation is best effort; data verification remains strict */ }
        }
        List<FileMark> Inventory(string root, bool directory, bool hash)
        {
            RejectLinkTree(root); var list = new List<FileMark>();
            if (!directory) { FileInfo f = new FileInfo(root); list.Add(Mark("", false, f, hash)); return list; }
            Walk(root, "", list, hash); return list;
        }
        FileMark Mark(string rel, bool dir, FileSystemInfo info, bool hash)
        {
            var f = new FileMark { Rel = rel, Dir = dir, Ticks = info.LastWriteTimeUtc.Ticks, Attributes = (int)info.Attributes };
            if (!dir) { var fi = (FileInfo)info; f.Size = fi.Length; if (hash) { f.Hash = Hash(fi.FullName); Advance(f.Size); } }
            return f;
        }
        void Walk(string root, string rel, List<FileMark> list, bool hash)
        {
            string here = Join(root, rel); RejectLink(here); list.Add(Mark(rel, true, new DirectoryInfo(here), hash));
            foreach (string path in Directory.GetFileSystemEntries(here).OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                RejectLink(path); string name = Path.GetFileName(path), child = rel.Length == 0 ? name : rel + "/" + name;
                if (Directory.Exists(path)) Walk(root, child, list, hash); else list.Add(Mark(child, false, new FileInfo(path), hash));
            }
        }

        // One inventory pass answers both questions, so nothing is ever read twice to decide.
        Probe Examine(string path, Item item)
        {
            var p = new Probe();
            if (!ExistsAny(path)) return p;
            p.Exists = true;
            List<FileMark> now;
            try { now = Inventory(path, item.Dir, true); }
            catch (Exception ex) { p.Reason = ExplainError(ex); return p; }
            var expected = new Dictionary<string, FileMark>(StringComparer.Ordinal);
            foreach (FileMark f in item.Marks) expected[f.Rel] = f;
            p.SubsetOk = true;
            foreach (FileMark mark in now)
            {
                FileMark e;
                if (!expected.TryGetValue(mark.Rel, out e) || !Same(mark, e)) { p.SubsetOk = false; p.Reason = mark.Rel.Length == 0 ? "contents differ" : "unexpected or changed: " + mark.Rel; break; }
            }
            // Inventory paths are unique, so a verified subset of equal size is the whole record.
            p.Exact = p.SubsetOk && now.Count == item.Marks.Count;
            return p;
        }

        bool IsExact(string path, Item item) { Probe p = Examine(path, item); return p.Exists && p.Exact; }
        void VerifyExact(string root, Item item)
        {
            if (!ExistsAny(root)) throw new IOException("Expected item is missing: " + root);
            Probe p = Examine(root, item);
            if (!p.Exact) throw new InvalidDataException("Contents changed or unexpected files were found: " + root);
        }
        // Names, sizes and layout only.  Used straight after a checksum-verified copy,
        // where re-reading every byte would prove nothing new.
        void VerifyStructure(string root, Item item)
        {
            if (!ExistsAny(root)) throw new IOException("Expected item is missing: " + root);
            List<FileMark> now = Inventory(root, item.Dir, false);
            if (now.Count != item.Marks.Count) throw new InvalidDataException("Contents changed or unexpected files were found: " + root);
            var expected = new Dictionary<string, FileMark>(StringComparer.Ordinal);
            foreach (FileMark f in item.Marks) expected[f.Rel] = f;
            foreach (FileMark mark in now)
            {
                FileMark e;
                if (!expected.TryGetValue(mark.Rel, out e) || e.Dir != mark.Dir || e.Size != mark.Size)
                    throw new InvalidDataException("Contents changed or unexpected files were found: " + root);
            }
        }
        // A subset is permitted only while an already-journalled source deletion is being resumed.
        void VerifySubset(string root, Item item)
        {
            if (!ExistsAny(root)) return;
            Probe p = Examine(root, item);
            if (!p.SubsetOk) throw new InvalidDataException("Contents changed or unexpected files were found: " + root);
        }
        static bool Same(FileMark a, FileMark b) { return a.Rel == b.Rel && a.Dir == b.Dir && a.Size == b.Size && (a.Dir || Eq(a.Hash, b.Hash)); }

        void DeleteVerifiedSubset(string root, Item item, bool recheck)
        {
            if (!ExistsAny(root)) return;
            if (recheck) VerifySubset(root, item);
            if (!item.Dir) { ClearReadOnly(root); File.Delete(root); return; }
            foreach (FileMark f in item.Marks.Where(x => !x.Dir).OrderByDescending(x => x.Rel.Length)) { string p = Join(root, f.Rel); if (ExistsAny(p)) { ClearReadOnly(p); File.Delete(p); } }
            foreach (FileMark d in item.Marks.Where(x => x.Dir).OrderByDescending(x => x.Rel.Length)) { string p = Join(root, d.Rel); if (Directory.Exists(p)) { ClearReadOnly(p); Directory.Delete(p, false); } }
        }

        // Windows refuses to delete a read-only file or folder; mod archives and cautious
        // users both produce them.  Attributes are not part of verification, so clearing
        // the flag immediately before a delete cannot weaken any check.
        static void ClearReadOnly(string path)
        {
            try
            {
                FileAttributes a = File.GetAttributes(path);
                if ((a & FileAttributes.ReadOnly) != 0) File.SetAttributes(path, a & ~FileAttributes.ReadOnly);
            }
            catch { }
        }

        string Hash(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
                return Hex(sha.ComputeHash(stream));
        }
        static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }

        void SaveManifest(Manifest m, string folder)
        {
            ValidateManifest(m, folder);
            XElement doc = new XElement("ModHolster", new XAttribute("schema", Schema), new XAttribute("id", m.Id), new XAttribute("state", m.State), new XAttribute("group", m.Group), new XAttribute("kind", m.Kind), new XAttribute("game", m.Game), new XAttribute("documents", m.Documents), new XAttribute("legacy", m.Legacy),
                m.Items.Select(i => new XElement("Item", new XAttribute("ns", i.Ns), new XAttribute("name", i.Name), new XAttribute("dir", i.Dir), new XAttribute("state", i.State), i.Marks.Select(f => new XElement("Mark", new XAttribute("rel", f.Rel), new XAttribute("dir", f.Dir), new XAttribute("size", f.Size), new XAttribute("hash", f.Hash ?? ""), new XAttribute("ticks", f.Ticks), new XAttribute("attributes", f.Attributes))))));
            byte[] bytes;
            using (var buffer = new MemoryStream()) { doc.Save(buffer); bytes = buffer.ToArray(); }
            // Refuse to write a record that could never be read back again.
            if (bytes.LongLength > ManifestLimit)
                throw new InvalidDataException("This selection contains too many files to record in one session (" + (bytes.LongLength / (1024 * 1024)) + " MB of inventory). Holster it in smaller groups. Nothing was changed.");
            string path = Path.Combine(folder, "manifest.xml"), temp = path + ".new", previous = path + ".previous";
            using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { fs.Write(bytes, 0, bytes.Length); fs.Flush(true); }
            if (File.Exists(path)) File.Replace(temp, path, previous, true); else File.Move(temp, path);
        }

        Manifest LoadManifest(string path, string folder)
        {
            if (!File.Exists(path) || new FileInfo(path).Length > ManifestLimit) throw new InvalidDataException("Session manifest is missing or too large.");
            XDocument doc; var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxManifestBytes, MaxCharactersFromEntities = 0 };
            using (var reader = XmlReader.Create(path, settings)) doc = XDocument.Load(reader, LoadOptions.None);
            XElement root = doc.Root;
            string schema = root == null ? null : (string)root.Attribute("schema");
            if (root == null || root.Name != "ModHolster" || doc.Nodes().OfType<XElement>().Count() != 1 || schema == null || !ReadableSchemas.Contains(schema)) throw new InvalidDataException("Unsupported or corrupt session manifest.");
            bool hasKind = schema != "3.1";
            if (hasKind) RequireOnlyAttributes(root, "schema", "id", "state", "group", "kind", "game", "documents", "legacy");
            else RequireOnlyAttributes(root, "schema", "id", "state", "group", "game", "documents", "legacy");
            var m = new Manifest { Id = Attr(root, "id"), State = Attr(root, "state"), Game = Attr(root, "game"), Documents = Attr(root, "documents"), Legacy = Bool(Attr(root, "legacy")), Kind = SessionKind.Session };
            if (hasKind && !Enum.TryParse(Attr(root, "kind"), out m.Kind)) throw new InvalidDataException("Unsupported session kind.");
            if (!Enum.IsDefined(typeof(SessionKind), m.Kind)) throw new InvalidDataException("Unsupported session kind.");
            if (!ValidSessionId(m.Id) || !IsSessionLocation(folder, m.Id) || !Enum.TryParse(Attr(root, "group"), out m.Group) || !Enum.IsDefined(typeof(ModGroup), m.Group) || !States.Contains(m.State)) throw new InvalidDataException("Manifest identity, root, group, or state is unsafe.");
            // Recorded roots are remembered rather than enforced: the payload is proven by
            // checksum, so a moved game folder is a question for the user, not a dead end.
            m.GameMoved = !PathEq(m.Game, GameRoot);
            m.DocumentsMoved = !PathEq(m.Documents, _documents);
            foreach (XElement e in root.Elements())
            {
                if (e.Name != "Item") throw new InvalidDataException("Unknown manifest record."); RequireOnlyAttributes(e, "ns", "name", "dir", "state");
                var i = new Item { Ns = Attr(e, "ns"), Name = Attr(e, "name"), Dir = Bool(Attr(e, "dir")), State = Attr(e, "state") }; ValidateTop(i.Name);
                if ((i.Ns != "Game" && i.Ns != "Documents") || !ItemStates.Contains(i.State) || (i.Ns == "Documents" && !Eq(i.Name, "modconfig")) || (i.Ns == "Game" && ProtectedName(i.Name, i.Dir) && (!m.Legacy || !LegacyAllowedProtected(i.Name, i.Dir)))) throw new InvalidDataException("Manifest item is unsafe.");
                foreach (XElement q in e.Elements())
                {
                    if (q.Name != "Mark") throw new InvalidDataException("Unknown manifest record."); RequireOnlyAttributes(q, "rel", "dir", "size", "hash", "ticks", "attributes");
                    var mark = new FileMark { Rel = Attr(q, "rel"), Dir = Bool(Attr(q, "dir")), Hash = Attr(q, "hash") };
                    if (!long.TryParse(Attr(q, "size"), NumberStyles.None, CultureInfo.InvariantCulture, out mark.Size) || mark.Size < 0 || !long.TryParse(Attr(q, "ticks"), NumberStyles.None, CultureInfo.InvariantCulture, out mark.Ticks) || mark.Ticks < DateTime.MinValue.Ticks || mark.Ticks > DateTime.MaxValue.Ticks || !Int32.TryParse(Attr(q, "attributes"), NumberStyles.Integer, CultureInfo.InvariantCulture, out mark.Attributes)) throw new InvalidDataException("Manifest inventory is unsafe.");
                    ValidateRel(mark.Rel); if (!mark.Dir && (mark.Hash.Length != 64 || mark.Hash.Any(c => !Uri.IsHexDigit(c)))) throw new InvalidDataException("Manifest hash is unsafe."); i.Marks.Add(mark);
                }
                ValidateMarks(i); m.Items.Add(i);
            }
            if (m.Items.Count == 0) throw new InvalidDataException("Empty session manifest."); RejectOverlaps(m.Items.Select(TargetPath)); return m;
        }

        static readonly string[] States = { "Holstering", "Holstered", "Restoring", "Restored", "Purging", "Purged" };
        static readonly string[] ItemStates = { "Source", "Copying", "RemovingSource", "BackedUp", "Restoring", "Restored" };
        static void RequireOnlyAttributes(XElement e, params string[] names) { if (e.Attributes().Any(a => !names.Contains(a.Name.LocalName))) throw new InvalidDataException("Unknown manifest field."); foreach (string n in names) Attr(e, n); }
        static void ValidateMarks(Item i)
        {
            if (i.Marks.Count == 0 || (!i.Dir && (i.Marks.Count != 1 || i.Marks[0].Dir || i.Marks[0].Rel != "")) || (i.Dir && (!i.Marks[0].Dir || i.Marks[0].Rel != ""))) throw new InvalidDataException("Manifest inventory is incomplete.");
            var paths = new Dictionary<string, FileMark>(StringComparer.OrdinalIgnoreCase);
            foreach (FileMark f in i.Marks)
            {
                if (paths.ContainsKey(f.Rel)) throw new InvalidDataException("Duplicate or aliased manifest inventory path.");
                paths.Add(f.Rel, f);
            }
            foreach (FileMark f in i.Marks)
                if (f.Rel != "") { FileMark parent; if (!paths.TryGetValue(ParentRel(f.Rel), out parent) || !parent.Dir) throw new InvalidDataException("Manifest directory parent is missing."); }
        }
        static string ParentRel(string rel) { int p = rel.LastIndexOf('/'); return p < 0 ? "" : rel.Substring(0, p); }
        static string Attr(XElement x, string n) { XAttribute a = x.Attribute(n); if (a == null) throw new InvalidDataException("Manifest field missing: " + n); return a.Value; }
        static bool Bool(string value) { bool b; if (!Boolean.TryParse(value, out b)) throw new InvalidDataException("Manifest boolean invalid."); return b; }
        void ValidateManifest(Manifest m, string folder)
        {
            if (!ValidSessionId(m.Id) || !IsSessionLocation(folder, m.Id) || !States.Contains(m.State) || m.Items.Count == 0) throw new InvalidDataException("Refusing to write unsafe manifest.");
            foreach (Item i in m.Items) { ValidateTop(i.Name); if (!ItemStates.Contains(i.State)) throw new InvalidDataException("Unsafe item state."); ValidateMarks(i); }
        }

        // Session folder names stay short so that deep mod trees keep as much of the
        // Windows path budget as possible.  v3.1 GUID folders remain readable.
        static string NewSessionId() { return ShortId(16); }
        static string ShortId(int hexChars)
        {
            var bytes = new byte[(hexChars + 1) / 2];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return Hex(bytes).Substring(0, hexChars);
        }
        static bool ValidSessionId(string id)
        {
            if (String.IsNullOrEmpty(id)) return false;
            if (id.Length == 16 && id.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return true;
            Guid g; return Guid.TryParseExact(id, "D", out g) && id == g.ToString("D");
        }

        // ===== policy and paths =====
        Candidate Classify(Entry e, bool asi, bool reshade, bool enb, bool lml)
        {
            var c = new Candidate { Name = e.Name, FullPath = e.Path, IsDirectory = e.IsDirectory, Group = ModGroup.Mods, Reason = "Unknown; select only if you recognize it." };
            if (ProtectedName(e.Name, e.IsDirectory) || PathEq(e.Path, CurrentProcessPath())) { c.Protected = true; c.Reason = "Protected vanilla, launcher, or safety-engine content."; return c; }
            string[] mods = { "ScriptHookRDR2.dll", "ScriptHookRDR2.ini", "ScriptHookRDR2.log", "ScriptHookRDR2.dev", "lml.ini", "lml.log", "ModManager.Core.dll", "ModManager.NativeInterop.dll" };
            string[] moddirs = { "lml", "RampageFiles", "scripts", "scripthookrdr2dotnet" };
            string[] rf = { "ReShade.ini", "ReShade.log", "ReShade32.dll", "ReShade64.dll", "ReShadePreset.ini", "dxgi.dll", "d3d12.dll" };
            string[] ef = { "enblocal.ini", "enbseries.ini", "enbadaptation.fx", "enbbloom.fx", "enbdepthoffield.fx", "enbeffect.fx", "enbeffectpostpass.fx", "enbeffectprepass.fx", "enblens.fx", "enblensmask.png", "enbpalette.bmp", "enbsunsprite.fx", "d3d11.dll", "d3dcompiler_46e.dll" };
            if ((!e.IsDirectory && (Ends(e.Name, ".asi") || mods.Any(x => Eq(x, e.Name)))) || (e.IsDirectory && moddirs.Any(x => Eq(x, e.Name))) || (!e.IsDirectory && (Eq(e.Name, "dinput8.dll") || Eq(e.Name, "version.dll")) && asi)) { c.Recommended = true; c.Reason = "Recognized mod component"; return c; }
            // Larry's Mod Loader ships NLog.dll beside its own DLLs.  Only treated as a mod
            // component when other LML files are present, so an unrelated future NLog.dll
            // shipped by a game update is still left alone.
            if (!e.IsDirectory && Eq(e.Name, "NLog.dll") && lml) { c.Recommended = true; c.Reason = "Recognized mod component (LML support library)"; return c; }
            if ((!e.IsDirectory && rf.Any(x => Eq(x, e.Name)) && (!Eq(e.Name, "dxgi.dll") && !Eq(e.Name, "d3d12.dll") || reshade)) || (e.IsDirectory && Eq(e.Name, "reshade-shaders"))) { c.Recommended = true; c.Group = ModGroup.ReShade; c.Reason = "Recognized ReShade component"; return c; }
            if ((!e.IsDirectory && ef.Any(x => Eq(x, e.Name)) && (!Eq(e.Name, "d3d11.dll") && !Eq(e.Name, "d3dcompiler_46e.dll") || enb)) || (e.IsDirectory && (Eq(e.Name, "enbseries") || Eq(e.Name, "enbcache")))) { c.Recommended = true; c.Group = ModGroup.ENB; c.Reason = "Recognized ENB component"; return c; }
            return c;
        }
        static bool LegacyAllowedProtected(string n, bool dir)
        {
            if (Ends(n, ".exe") || Ends(n, ".rpf")) return false;
            return (dir && Eq(n, ".egstore")) || Starts(n, "steam") || Starts(n, "eossdk") || Ends(n, ".manifest") || Ends(n, ".mancpn") || Ends(n, ".egstore");
        }
        static bool ProtectedName(string n, bool dir)
        {
            // Keep the portable app's runtime settings beside its executable. Apply this
            // policy to scan, manifest recovery and conflict handling, not just UI selection.
            if (!dir && (Eq(n, "CLEAN_v3.2.exe.config") ||
                Eq(n, Path.GetFileName(typeof(SafetyEngine).Assembly.Location) + ".config") ||
                Eq(n, Path.GetFileName(CurrentProcessPath()) + ".config"))) return true;
            string[] exact = { "amd_ags_x64.dll", "bink2w64.dll", "dxilconv7.dll", "ffx_fsr2_api_dx12_x64.dll", "ffx_fsr2_api_vk_x64.dll", "ffx_fsr2_api_x64.dll", "NvLowLatencyVk.dll", "nvngx_dlss.dll", "oo2core_5_win64.dll", "steam_api64.dll", "installscript.vdf", "installscript_sdk.vdf", "title.rgl", ToolFolder };
            if (exact.Any(x => Eq(x, n)) || Starts(n, "_RDR2_ModHolster") || Eq(n, "_RDR2_Mods_Disabled") || Eq(n, "_RDR2_ReShade_Disabled") || Eq(n, "_RDR2_ENB_Disabled") || Ends(n, ".exe") || Ends(n, ".rpf") || (dir && new[] { "12on7", "Redistributables", "x64", ".egstore" }.Any(x => Eq(x, n)))) return true;
            return Starts(n, "steam") || Starts(n, "eossdk") || Ends(n, ".manifest") || Ends(n, ".mancpn") || Ends(n, ".egstore");
        }
        void CheckRoots()
        {
            string exe = Path.Combine(GameRoot, "RDR2.exe"); if (!ExistsAny(exe) || Directory.Exists(exe)) throw new InvalidOperationException("RDR2.exe is no longer present in the selected game folder.");
            RejectLink(exe); RejectLinkAncestors(GameRoot); RejectLinkAncestors(_documents);
            foreach (string root in new[] { BackupRoot, Path.Combine(GameRoot, PreviousToolFolder) })
                if (ExistsAny(root)) { RejectLinkAncestors(root); if (!Directory.Exists(root)) throw new IOException("Recovery path is not a folder: " + root); }
        }
        static string CanonicalExistingRoot(string path, string what)
        {
            string p = CanonicalOptionalRoot(path, what); if (!Directory.Exists(p)) throw new ArgumentException("The " + what + " must be an existing non-root folder."); return p;
        }
        static string CanonicalOptionalRoot(string path, string what)
        {
            if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new ArgumentException("The " + what + " must be an absolute non-root folder.");
            string full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string root = Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (full.Length == 0 || Eq(full, root)) throw new ArgumentException("The " + what + " must not be a filesystem root.");
            RejectLinkAncestors(full); return full;
        }
        static void EnsureSafeParent(string directory)
        {
            if (String.IsNullOrEmpty(directory)) throw new IOException("Unsafe destination parent.");
            RejectLinkAncestors(directory); Directory.CreateDirectory(directory); RejectLinkAncestors(directory);
        }
        static void RejectLinkAncestors(string p)
        {
            string q = Path.GetFullPath(p); while (!String.IsNullOrEmpty(q)) { if (ExistsAny(q)) RejectLink(q); string parent = Path.GetDirectoryName(q); if (parent == q) break; q = parent; }
        }
        static void RejectLinkTree(string p)
        {
            RejectLink(p); if (Directory.Exists(p)) foreach (string child in Directory.GetFileSystemEntries(p)) { RejectLink(child); if (Directory.Exists(child)) RejectLinkTree(child); }
        }
        static void RejectLink(string p) { if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse point/symlink refused: " + p); }
        static bool ExistsAny(string p) { try { File.GetAttributes(p); return true; } catch (FileNotFoundException) { return false; } catch (DirectoryNotFoundException) { return false; } }
        static IEnumerable<Entry> DirectEntries(string p) { return Directory.GetFileSystemEntries(p).Select(x => new Entry { Name = Path.GetFileName(x), Path = x, IsDirectory = Directory.Exists(x) }); }
        static void RejectOverlaps(IEnumerable<string> paths) { var a = paths.Select(Path.GetFullPath).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); for (int i = 0; i < a.Length; i++) for (int j = i + 1; j < a.Length; j++) if (IsNestedOrSame(a[i], a[j])) throw new InvalidDataException("Overlapping selected paths are unsafe."); }
        static bool IsNestedOrSame(string parent, string child) { string a = Path.GetFullPath(parent).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), b = Path.GetFullPath(child).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); return Eq(a, b) || b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase); }
        static void ValidateTop(string n) { ValidateWindowsComponent(n, false); }
        static void ValidateRel(string rel)
        {
            if (rel == "") return; if (rel.IndexOf('\\') >= 0 || rel.StartsWith("/") || rel.IndexOf(':') >= 0) throw new InvalidDataException("Unsafe inventory path.");
            foreach (string part in rel.Split('/')) ValidateWindowsComponent(part, false);
        }
        static void ValidateWindowsComponent(string name, bool allowEmpty)
        {
            if ((String.IsNullOrEmpty(name) && !allowEmpty) || name == "." || name == ".." || name.IndexOfAny(new[] { '/', '\\', ':', '\0', '<', '>', '"', '|', '?', '*' }) >= 0 || name.Any(char.IsControl) || name.EndsWith(" ") || name.EndsWith(".")) throw new InvalidDataException("Unsafe Windows path component.");
            string stem = name.Split('.')[0].ToUpperInvariant(); if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && stem[3] >= '1' && stem[3] <= '9')) throw new InvalidDataException("Reserved Windows path component.");
        }
        string TargetPath(Item i) { return i.Ns == "Game" ? Path.Combine(GameRoot, i.Name) : Path.Combine(_documents, i.Name); }
        string BackupPath(string folder, Item i) { return Path.Combine(folder, i.Ns, i.Name); }
        string LegacyRoot(ModGroup g) { return Path.Combine(GameRoot, g == ModGroup.Mods ? "_RDR2_Mods_Disabled" : g == ModGroup.ReShade ? "_RDR2_ReShade_Disabled" : "_RDR2_ENB_Disabled"); }
        string LegacyPayloadPath(ModGroup group, Item item) { string root = LegacyRoot(group); return item.Ns == "Documents" ? Path.Combine(root, "Documents", "modconfig") : Path.Combine(root, item.Name); }
        // Old sessions remain in place: no automatic move/merge of the user's recovery data.
        IEnumerable<string> StoreRoots() { return new[] { BackupRoot, Path.Combine(GameRoot, PreviousToolFolder) }.Where(Directory.Exists); }
        bool IsSessionLocation(string folder, string id)
        {
            return StoreRoots().Any(root => PathEq(folder, Path.Combine(root, id))) &&
                StoreRoots().Count(root => ExistsAny(Path.Combine(root, id))) == 1;
        }
        string SessionFolder(string id)
        {
            if (!ValidSessionId(id)) throw new ArgumentException("Invalid session ID.");
            var paths = StoreRoots().Select(root => Path.Combine(root, id)).Where(ExistsAny).ToList();
            if (paths.Count != 1) throw new IOException(paths.Count == 0 ? "Session folder was not found." : "Duplicate session ID in both recovery folders. Keep both copies; recovery is blocked to avoid choosing the wrong one.");
            if (!Directory.Exists(paths[0])) throw new IOException("Session path is not a folder.");
            RejectLinkTree(paths[0]); return paths[0];
        }
        sealed class StoreLocks : IDisposable
        {
            public readonly List<FileStream> Streams = new List<FileStream>();
            public void Dispose() { for (int i = Streams.Count - 1; i >= 0; i--) Streams[i].Dispose(); }
        }
        IDisposable AcquireLock()
        {
            var held = new StoreLocks();
            try
            {
                CheckRoots();
                foreach (string root in StoreRoots()) held.Streams.Add(new FileStream(Path.Combine(root, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None));
                return held;
            }
            catch (Exception ex) { held.Dispose(); throw new IOException("Could not lock the recovery folders. Another CLEAN operation may be running; close other versions before retrying.", ex); }
        }
        void CheckRunning()
        {
            foreach (Process process in Process.GetProcesses())
            {
                string n = null; try { n = process.ProcessName; } catch { } finally { process.Dispose(); }
                if (!String.IsNullOrEmpty(n) && (Eq(n, "RDR2") || Eq(n, "RDR2Launcher") || Starts(n, "lml") || Starts(n, "lenny") || Eq(n, "ModManager") || Eq(n, "ModManager.UI"))) throw new InvalidOperationException("Close RDR2 and LML/mod-manager processes before changing mod files.");
            }
        }
        void VerifyPurgePayload(Manifest m, string folder, bool allowSubset)
        {
            foreach (string ns in new[] { "Game", "Documents" })
            {
                string payload = Path.Combine(folder, ns); var expected = new HashSet<string>(m.Items.Where(x => x.Ns == ns).Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
                if (Directory.Exists(payload)) { RejectLinkTree(payload); foreach (string found in Directory.GetFileSystemEntries(payload)) if (!expected.Contains(Path.GetFileName(found))) throw new InvalidDataException("Unexpected backup payload prevents purge: " + found); }
            }
            foreach (Item i in m.Items) { string p = BackupPath(folder, i); if (!ExistsAny(p)) { if (!allowSubset) throw new InvalidDataException("Missing backup payload prevents purge: " + i.Name); } else if (allowSubset) VerifySubset(p, i); else VerifyExact(p, i); }
        }

        // ===== free space, progress =====
        void RequireFreeSpace(string onPath, long bytes, string what)
        {
            if (bytes <= 0) return;
            long available = -1, need = bytes + Math.Max(64L * 1024 * 1024, bytes / 20);
#if HOLSTER_TESTS
            if (TestFreeSpaceBytes > 0) available = TestFreeSpaceBytes;
            else
#endif
            try { available = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(onPath))).AvailableFreeSpace; } catch { return; }
            if (available >= 0 && available < need)
                throw new IOException("Not enough free space for " + what + ". About " + Mb(need) + " is needed and " + Mb(available) + " is free on " + Path.GetPathRoot(Path.GetFullPath(onPath)) + ". Nothing was changed.");
        }
        static string Mb(long bytes) { return bytes >= 1024L * 1024 * 1024 ? (bytes / 1073741824.0).ToString("0.0", CultureInfo.InvariantCulture) + " GB" : (bytes / 1048576.0).ToString("0", CultureInfo.InvariantCulture) + " MB"; }

        string _phase = ""; long _done, _total; int _lastPercent = -1;
        void SetPhase(string phase, long total) { _phase = phase; _done = 0; _total = total; _lastPercent = -1; Report(true); }
        void Advance(long bytes) { _done += bytes; Report(false); }
        void Done() { _phase = ""; _done = _total = 0; _lastPercent = -1; }
        void Report(bool force)
        {
            Action<string, long, long> p = Progress; if (p == null) return;
            int percent = _total > 0 ? (int)Math.Min(100, _done * 100 / _total) : -1;
            if (!force && percent == _lastPercent) return;
            _lastPercent = percent;
            try { p(_phase, _done, _total); } catch { }
        }

        [System.Diagnostics.Conditional("HOLSTER_TESTS")]
        static void TestFault(string point)
        {
#if HOLSTER_TESTS
            if (FaultInjector != null) FaultInjector(point);
#endif
        }
#if HOLSTER_TESTS
        internal static Action<string> FaultInjector;
        internal static long TestManifestLimit;   // bytes; 0 = production limit
        internal static long TestFreeSpaceBytes;  // bytes; 0 = ask the real volume
#endif
        void Log(string text) { string p = Path.Combine(BackupRoot, "logs", DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture) + ".log"); try { File.AppendAllText(p, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + " " + text + Environment.NewLine); } catch { } try { _log(text); } catch { } }
        static void TryRemoveEmpty(string path) { try { if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any()) { ClearReadOnly(path); Directory.Delete(path, false); } } catch { } }
        static string Join(string root, string rel) { return rel.Length == 0 ? root : Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar)); }
        static bool Eq(string a, string b) { return String.Equals(a, b, StringComparison.OrdinalIgnoreCase); }
        static bool Starts(string a, string b) { return a.StartsWith(b, StringComparison.OrdinalIgnoreCase); }
        static bool Ends(string a, string b) { return a.EndsWith(b, StringComparison.OrdinalIgnoreCase); }
        static bool PathEq(string a, string b) { return a != null && b != null && Eq(Path.GetFullPath(a).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), Path.GetFullPath(b).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)); }
        static string CurrentProcessPath() { try { using (Process p = Process.GetCurrentProcess()) return p.MainModule.FileName; } catch { return ""; } }
        SessionInfo Info(Manifest m, string folder) { return new SessionInfo { Id = m.Id, State = m.State, Group = m.Group, ItemCount = m.Items.Count, Folder = folder, IsLegacy = m.Legacy, Kind = m.Kind, NeedsRebind = m.NeedsRebind, RecordedGame = m.Game, RecordedDocuments = m.Documents, PayloadBytes = m.Bytes }; }
        OperationResult NeedsRebindResult(Manifest m)
        {
            var r = Fail(m.Id, "This backup was made while the game folder was at:\n  " + m.Game + (m.DocumentsMoved && m.Items.Any(x => x.Ns == "Documents") ? "\n  " + m.Documents : "") + "\n\nThose folders have moved. The backup itself is verified by checksum, so it can still be restored into the current folders — but only if you confirm that this is the same installation.");
            r.NeedsRebind = true;
            return r;
        }
        OperationResult Ok(string id, string message, List<string> notes) { var r = new OperationResult { Success = true, SessionId = id, SessionPath = String.IsNullOrEmpty(id) ? null : SessionFolder(id), Message = message }; if (notes != null) r.Notes.AddRange(notes); return r; }
        OperationResult Fail(string id, string message) { Log(message); return new OperationResult { Success = false, SessionId = id, Message = message, Errors = new List<string> { message } }; }
        OperationResult ExceptionResult(string id, Exception ex)
        {
            string message = ExplainError(ex); try { Log("FAILED: " + message); } catch { }
            var r = new OperationResult { Success = false, SessionId = id, Message = message, Errors = new List<string> { message } };
            var conflict = ex as ConflictException; if (conflict != null) r.Conflicts.AddRange(conflict.Conflicts);
            return r;
        }
    }
}
