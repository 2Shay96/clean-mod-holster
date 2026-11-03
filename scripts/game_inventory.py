"""Read-only game inventory and optional independent save backup. Never installs mods."""
import argparse, datetime, hashlib, json, pathlib, shutil, sys, time

parser = argparse.ArgumentParser()
parser.add_argument('--game', required=True, type=pathlib.Path)
parser.add_argument('--out', required=True, type=pathlib.Path)
parser.add_argument('--documents', type=pathlib.Path)
parser.add_argument('--backup-saves', type=pathlib.Path)
args = parser.parse_args()
game = args.game.resolve(strict=True)
assert (game / 'RDR2.exe').is_file()
out = args.out.resolve()
assert not out.is_relative_to(game), 'Evidence must be outside the installation'
assert not out.exists(), 'Never overwrite an inventory'
start = time.monotonic()
files = []
for p in sorted(game.rglob('*')):
    if p.is_symlink() or p.is_junction():
        raise RuntimeError('Linked installation path refused: ' + str(p))
    if not p.is_file():
        continue
    before = p.stat()
    with p.open('rb') as stream:
        digest = hashlib.file_digest(stream, 'sha256').hexdigest()
    after = p.stat()
    assert (before.st_size, before.st_mtime_ns) == (after.st_size, after.st_mtime_ns), 'File changed while hashing: ' + str(p)
    files.append({'path':p.relative_to(game).as_posix(),'bytes':after.st_size,'sha256':digest})
    if len(files) % 20 == 0:
        print('Hashed', len(files), 'files;', sum(f['bytes'] for f in files), 'bytes', flush=True)
backup = None
if args.backup_saves:
    assert args.documents, '--documents is required for save backup'
    destination = args.backup_saves.resolve()
    assert not destination.is_relative_to(game) and not destination.exists()
    source = args.documents / 'Profiles'
    if source.exists():
        assert not source.is_symlink() and not source.is_junction()
        for p in source.rglob('*'):
            assert not p.is_symlink() and not p.is_junction(), 'Linked save entry refused'
        shutil.copytree(source, destination)
        for p in source.rglob('*'):
            if p.is_file():
                with p.open('rb') as a, (destination / p.relative_to(source)).open('rb') as b:
                    assert hashlib.file_digest(a,'sha256').digest() == hashlib.file_digest(b,'sha256').digest(), 'Save backup mismatch'
        backup = str(destination)
    else:
        backup = 'Profiles absent before first launch; no existing saves to copy'
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps({'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'game':str(game),'seconds':round(time.monotonic()-start,2),'saveBackup':backup,'files':files},indent=2)+'\n',encoding='utf-8')
print('Recorded',len(files),'files,',sum(f['bytes'] for f in files),'bytes:',out,flush=True)
