"""Verify review ZIP members and safely extract both archives to new scratch folders."""
import argparse,hashlib,json,pathlib,zipfile
root=pathlib.Path(__file__).resolve().parents[1]
parser=argparse.ArgumentParser();parser.add_argument('name');args=parser.parse_args()
assert args.name.replace('-','').isalnum()
folder=root/'build'/args.name
manifest=json.loads((folder/'package-manifest.json').read_text())
dest=root/'scratch'/('extracted-'+args.name);assert not dest.exists()
source_dest=root/'scratch'/('source-'+args.name);assert not source_dest.exists()
for record in manifest['archives']:
    archive=folder/record['file']
    assert hashlib.sha256(archive.read_bytes()).hexdigest()==record['sha256']
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None and len(z.namelist())==len(record['members'])
        assert len(z.namelist())==len(set(z.namelist()))
        assert len(z.namelist())==len({n.casefold() for n in z.namelist()})
        sums={name:digest for digest,name in (line.split('  ',1) for line in z.read('SHA256SUMS.txt').decode('utf-8').splitlines())}
        assert set(sums)==set(z.namelist())-{'SHA256SUMS.txt'}
        for member in record['members']:
            name=pathlib.PurePosixPath(member['path'])
            assert not name.is_absolute() and '..' not in name.parts and ':' not in str(name) and '\\' not in str(name)
            info=z.getinfo(str(name));assert info.file_size==member['bytes'] and info.file_size<20*1024*1024
            data=z.read(info);assert hashlib.sha256(data).hexdigest()==member['sha256']
            if str(name) in sums: assert hashlib.sha256(data).hexdigest()==sums[str(name)]
            if 'source' in record['file']: assert str(name) not in manifest['sourceFiles'] or member['sha256']==manifest['sourceFiles'][str(name)]
            target=(dest if 'binary' in record['file'] else source_dest)/pathlib.Path(*name.parts)
            target.parent.mkdir(parents=True,exist_ok=True)
            with target.open('xb') as stream:stream.write(data)
for name,digest in manifest['sourceFiles'].items():
    assert hashlib.sha256((root/name).read_bytes()).hexdigest()==digest,'Source drift: '+name
    assert hashlib.sha256((source_dest/name).read_bytes()).hexdigest()==digest,'Extracted source mismatch: '+name
assert hashlib.sha256((dest/'CLEAN_v3.2.exe').read_bytes()).hexdigest()==manifest['exeSHA256']
assert hashlib.sha256((dest/'CLEAN_v3.2.exe.config').read_bytes()).hexdigest()==manifest['configSHA256']
print('PASS both ZIP hashes, every member, embedded checksums, source snapshot and safe extraction:',dest,source_dest)
