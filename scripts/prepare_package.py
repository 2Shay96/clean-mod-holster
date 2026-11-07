"""Create unpublished review ZIPs from an explicit allowlist. Never uploads."""
import argparse, datetime, hashlib, json, pathlib, zipfile
ROOT=pathlib.Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser()
parser.add_argument('--name',default='v32-private-candidate-03')
parser.add_argument('--distribution',action='store_true',help='Use distribution-format notices; still unpublished pending review')
args=parser.parse_args()
assert args.name.replace('-','').replace('_','').isalnum()
out=ROOT/'build'/args.name
assert not out.exists(), 'Choose a new review name; never overwrite a prior package'
out.mkdir(parents=True)
exe=ROOT/'src/bin/Release/net48/CLEAN_v3.2.exe'
config=exe.with_suffix('.exe.config')
for p in (exe,config): assert p.is_file()
binary=exe.read_bytes()
for forbidden in ('FaultInjector','TestFreeSpaceBytes','TestManifestLimit','SimulateDpi','DaemonShay'):
    assert forbidden.encode('utf-16le') not in binary and forbidden.encode() not in binary, forbidden
readme=['ARTWORK-NOTICE.txt','LICENSE.txt','README.md','BUILD.md','UPGRADE-RECOVERY.md','CHANGELOG.md','UI-CHANGES.md','VALIDATION.md','THIRD-PARTY-NOTICES.txt']
licences=['docs/ACTION-ICONS.md','docs/HEADER-LETTERING.md','docs/ICON-PROVENANCE.md','docs/LICENCE-PROPOSAL.md','docs/ATTRIBUTION-AUDIT.md','docs/PUBLIC-V23-MIT-LICENSE.txt','docs/SUPPLIED-ALPHA1-MIT-LICENSE.txt']
evidence=['docs/release/FINAL-STEP1-RESULTS-20261029.md','docs/release/FINAL-UI-CHECKS-20261029.md','docs/evidence/archive-provenance.json','docs/evidence/sdk.json','docs/evidence/final-ui-native-20261029/final15-source-receipt.json','docs/evidence/final-ui-native-20261029/final15-native-results.xml','docs/evidence/final-ui-native-20261029/step3-ui-output.txt']
evidence += ['docs/release/PORTABLE-FOLDER-CHECKS-20261106.md']
evidence += ['docs/release/RECOVERY-FOLDER-CHECKS-20261106.md']
evidence += ['docs/release/STORY-RESHADE-CYCLE-20261106.md',
             'docs/release/LML-CONFLICT-CHECKS-20261106.md',
             'docs/release/WINDOWS-CI-20261107.md']
evidence += ['docs/evidence/recovery-folder-20261106/'+name for name in [
    'recovery-folder-build-20261106.txt','recovery-folder-extended-20261106.txt',
    'recovery-folder-native-all-20261106.txt','recovery-folder-native-final.txt',
    'recovery-folder-extended-final.txt','recovery-folder-extended-20261106.xml',
    'recovery-folder-native-all-20261106.xml','recovery-folder-native-final.xml',
    'recovery-folder-extended-final.xml']]
evidence += ['docs/evidence/news-links-20261106/news-links-build-20261106.txt','docs/evidence/news-links-20261106/news-links-build-retry-20261106.txt']
notice=b'PRIVATE RELEASE CANDIDATE. Licence approved by author on 28 October 2026. Public distribution is not approved. See VALIDATION.md for actual evidence and remaining limits.\n'
if args.distribution:
    notice=b'CLEAN - Mod Remover and Holster v3.2 by 2Shay. Windows / .NET Framework 4.8. Keep CLEAN_v3.2.exe and CLEAN_v3.2.exe.config together; the pair may go beside RDR2.exe. Keep guides/notices/source outside the game. See README for the shortcut alternative. Read README.md, LICENSE.txt, THIRD-PARTY-NOTICES.txt, ARTWORK-NOTICE.txt and VALIDATION.md before use.\n'
def sha(data): return hashlib.sha256(data).hexdigest()
def pack(path,files):
    members={name:p.read_bytes() for name,p in files}
    assert len(members)==len(files)
    members['RELEASE-INFORMATION.txt' if args.distribution else 'APPROVAL-REQUIRED.txt']=notice
    members['SHA256SUMS.txt']=''.join(sha(data)+'  '+name+'\n' for name,data in sorted(members.items())).encode()
    with zipfile.ZipFile(path,'x',compression=zipfile.ZIP_DEFLATED,compresslevel=9) as z:
        for name,data in sorted(members.items()):
            assert not pathlib.PurePosixPath(name).is_absolute() and '..' not in pathlib.PurePosixPath(name).parts
            info=zipfile.ZipInfo(name,(2026,10,27,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED
            z.writestr(info,data)
    with zipfile.ZipFile(path) as z: assert z.testzip() is None
    return {'file':path.name,'bytes':path.stat().st_size,'sha256':sha(path.read_bytes()),'members':[{ 'path':n,'sha256':sha(d),'bytes':len(d)} for n,d in sorted(members.items())]}
binfiles=[(exe.name,exe),(config.name,config)]+[(p,ROOT/p) for p in readme+licences+evidence]
sourcefiles=[(p,ROOT/p) for p in readme+licences+evidence+['build.ps1','.gitignore','docs/NATIVE-VOLUME-TEST.md']]
for name in ['trash.svg','reload-icon.svg','joede-store-symbol.svg','Anonymous_Magnify_1_icon.svg']:
    original=ROOT/'assets/action-icons-original'/name
    sourcefiles.append((original.relative_to(ROOT).as_posix(),original))
for folder in ['src','tests','.github']:
    for p in (ROOT/folder).rglob('*'):
        if p.is_file() and not any(x in ('bin','obj','screenshots') for x in p.relative_to(ROOT/folder).parts):
            if p.name=='release-test-report.json': continue
            assert p.suffix.lower() not in ('.exe','.dll','.zip','.rpf','.esm','.bsa','.ba2')
            sourcefiles.append((p.relative_to(ROOT).as_posix(),p))
for name in ['generate_icon.ps1','prepare_package.py','verify_review_package.py','game_inventory.py','prepare_native_test_volume.ps1','test_portable.ps1']:
    sourcefiles.append(('scripts/'+name,ROOT/'scripts'/name))
records=[pack(out/'CLEAN-v3.2-binary.zip',binfiles),pack(out/'CLEAN-v3.2-source.zip',sourcefiles)]
manifest={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'state':'DISTRIBUTION-FORMAT REVIEW; UNPUBLISHED; AUTHOR APPROVAL REQUIRED' if args.distribution else 'PRIVATE RELEASE CANDIDATE; PUBLIC PUBLICATION NOT APPROVED','exeSHA256':sha(binary),'configSHA256':sha(config.read_bytes()),'sourceFiles':{n:sha(p.read_bytes()) for n,p in sourcefiles},'archives':records}
(out/'package-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
(out/'SHA256SUMS.txt').write_text(''.join(r['sha256']+'  '+r['file']+'\n' for r in records))
print(out)
for r in records:print(r['file'],r['bytes'],r['sha256'])
