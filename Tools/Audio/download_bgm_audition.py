"""Download the author's free pack and extract only the selected music/readme files."""
import hashlib
import http.cookiejar
import json
from pathlib import Path
import re
import shutil
import urllib.parse
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'output/audio/bgm-audition-alan-zaring'
PAGE = 'https://alanzaringmusic.itch.io/rpg-music-starter-pack'
ARCHIVE = OUT / 'RPG Music Starter Pack.zip'


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    if not ARCHIVE.exists():
        opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        opener.addheaders = [('User-Agent', 'Mozilla/5.0'), ('Referer', PAGE)]
        with opener.open(PAGE, timeout=60) as response:
            html = response.read().decode('utf-8')
        csrf = re.search(r'<meta name="csrf_token" value="([^"]+)"', html)[1]
        upload = re.search(r'data-upload_id="(\d+)"', html)[1]
        url = PAGE + '/file/' + upload + '?source=view_game&as_props=1'
        request = urllib.request.Request(url, data=urllib.parse.urlencode({'csrf_token': csrf}).encode())
        with opener.open(request, timeout=60) as response:
            result = json.load(response)
        if not result.get('url'):
            raise RuntimeError('No authorized free download URL returned: ' + str(list(result)))
        temporary = ARCHIVE.with_suffix('.zip.part')
        with opener.open(result['url'], timeout=90) as response:
            length = int(response.headers.get('Content-Length', '0'))
            if length > 700 * 1024 * 1024:
                raise RuntimeError('Unexpectedly large download')
            print(f'Downloading official archive: {length/1024/1024:.1f} MiB', flush=True)
            received = 0
            next_report = 10 * 1024 * 1024
            with temporary.open('wb') as file:
                while chunk := response.read(1024 * 1024):
                    file.write(chunk)
                    received += len(chunk)
                    if received > 700 * 1024 * 1024:
                        raise RuntimeError('Download exceeds expected size bound')
                    if received >= next_report:
                        print(f'{received/1024/1024:.0f} MiB downloaded', flush=True)
                        next_report += 20 * 1024 * 1024
            if length and received != length:
                raise RuntimeError('Incomplete archive')
        temporary.rename(ARCHIVE)
    records = []
    with zipfile.ZipFile(ARCHIVE) as archive:
        members = [m for m in archive.infolist() if not m.is_dir()]
        for member in members:
            name = Path(member.filename).name
            lower = name.lower()
            # The author's page calls track 02 "Peaceful Village"; the actual
            # archive retains its earlier "02 Town 2" filename. Keep it intact.
            selected = any(track in lower for track in ('peaceful village', '02 town 2', 'jungle dungeon'))
            readme = lower.endswith('.txt') and any(word in lower for word in ('readme', 'license', 'credit'))
            if not selected and not readme:
                continue
            if member.file_size > 100 * 1024 * 1024:
                raise RuntimeError('Unexpectedly large audio member: ' + name)
            target = OUT / name
            if not target.exists():
                with archive.open(member) as source, target.open('wb') as destination:
                    shutil.copyfileobj(source, destination)
            records.append({'member': member.filename, 'file': name, 'bytes': target.stat().st_size,
                            'sha256': hashlib.sha256(target.read_bytes()).hexdigest()})
        (OUT / 'archive-members.json').write_text(json.dumps(
            [{'name': m.filename, 'bytes': m.file_size} for m in members], indent=2), encoding='utf-8')
    assert any('02 town 2' in r['file'].lower() or 'peaceful village' in r['file'].lower()
               for r in records), 'Town track not found'
    assert any('jungle dungeon' in r['file'].lower() for r in records), 'Dungeon track not found'
    manifest = {'author': 'Alan Zaring', 'source': PAGE, 'pack': ARCHIVE.name,
                'license_note': 'Free download. Author permits commercial use; attribution appreciated but not required (source page comments).',
                'files': records}
    (OUT / 'sources.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps(records, indent=2), flush=True)


if __name__ == '__main__':
    main()
