"""Print non-secret SHA-256 identities of a completed publication payload."""
import hashlib
import json
from pathlib import Path
import sys


def fingerprint(root, assembly):
    entries = []
    for path in sorted(root.rglob('*')):
        if path.is_symlink():
            raise ValueError('Symbolic links are not accepted in a publication payload.')
        if not path.is_file():
            continue
        digest = hashlib.sha256()
        size = 0
        with path.open('rb') as source:
            while chunk := source.read(1024 * 1024):
                digest.update(chunk)
                size += len(chunk)
        entries.append({'path': path.relative_to(root).as_posix(), 'bytes': size, 'sha256': digest.hexdigest()})
    selected = next(entry for entry in entries if entry['path'] == assembly)
    canonical = json.dumps(entries, sort_keys=True, separators=(',', ':')).encode()
    return {'files': len(entries), 'bytes': sum(entry['bytes'] for entry in entries),
            'assemblySha256': selected['sha256'], 'payloadManifestSha256': hashlib.sha256(canonical).hexdigest()}


if __name__ == '__main__':
    print(json.dumps(fingerprint(Path(sys.argv[1]), sys.argv[2]), sort_keys=True))
