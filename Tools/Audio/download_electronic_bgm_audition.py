"""Fetch Abstraction's public, free CC0 pack for local audition only."""
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
OUT = ROOT / 'output/audio/bgm-audition-abstraction'
PAGE = 'https://tallbeard.itch.io/three-red-hearts-prepare-to-dev'
ARCHIVE = OUT / 'three-red-hearts-prepare-to-dev-download.zip'
TRACKS = ('rabbit town', 'penguin town', 'pixel war 2', 'go')
LIMIT = 100 * 1024 * 1024


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    if not ARCHIVE.exists():
        opener = urllib.request.build_opener(
            urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
        opener.addheaders = [('User-Agent', 'Mozilla/5.0'), ('Referer', PAGE)]
        with opener.open(PAGE, timeout=45) as response:
            html = response.read().decode('utf-8')
        if 'LICENSE CC-0' not in html:
            raise RuntimeError('Expected public CC0 license not found; check the author page.')
        csrf = re.search(r'<meta name="csrf_token" value="([^"]+)"', html)[1]
        uploads = re.findall(r'data-upload_id="(\d+)"', html)
        if not uploads and '"actual_price":0' in html:
            # Follow itch.io's public "No thanks, just take me to downloads"
            # flow. This is an anonymous free grant, not a purchase/login.
            request = urllib.request.Request(
                PAGE + '/download_url',
                data=urllib.parse.urlencode({'csrf_token': csrf}).encode())
            with opener.open(request, timeout=45) as response:
                download_page = json.load(response).get('url')
            if not download_page:
                raise RuntimeError('No free download page was granted.')
            with opener.open(download_page, timeout=45) as response:
                html = response.read().decode('utf-8')
            csrf_match = re.search(r'<meta name="csrf_token" value="([^"]+)"', html)
            if csrf_match:
                csrf = csrf_match[1]
            uploads = re.findall(r'data-upload_id="(\d+)"', html)
        if len(set(uploads)) != 1:
            raise RuntimeError('Expected one free pack upload; inspect the author page.')
        request = urllib.request.Request(
            PAGE + '/file/' + uploads[0] + '?source=view_game&as_props=1',
            data=urllib.parse.urlencode({'csrf_token': csrf}).encode())
        with opener.open(request, timeout=45) as response:
            result = json.load(response)
        if not result.get('url'):
            raise RuntimeError('The public download endpoint did not provide a free file.')
        temporary = ARCHIVE.with_suffix('.zip.part')
        with opener.open(result['url'], timeout=45) as response:
            length = int(response.headers.get('Content-Length', '0'))
            if length > LIMIT:
                raise RuntimeError('Unexpected archive size.')
            print(f'Downloading official free pack: {length / 1048576:.1f} MiB', flush=True)
            received = 0
            with temporary.open('wb') as target:
                while chunk := response.read(1024 * 1024):
                    received += len(chunk)
                    if received > LIMIT:
                        raise RuntimeError('Download exceeds expected size.')
                    target.write(chunk)
                    if received % (10 * 1024 * 1024) == 0:
                        print(f'{received // 1048576} MiB downloaded', flush=True)
            if length and received != length:
                raise RuntimeError('Incomplete download.')
        temporary.rename(ARCHIVE)

    records = []
    found = set()
    with zipfile.ZipFile(ARCHIVE) as archive:
        members = [item for item in archive.infolist() if not item.is_dir()]
        print('Archive files:\n' + '\n'.join(item.filename for item in members), flush=True)
        for member in members:
            name = Path(member.filename).name
            stem = re.sub(r'[_\-]+', ' ', Path(name).stem.lower())
            matching = [track for track in TRACKS
                        if re.search(r'(?<![a-z])' + re.escape(track) + r'(?![a-z0-9])', stem)]
            audio = Path(name).suffix.lower() in ('.wav', '.ogg', '.mp3', '.flac')
            license_file = (name.lower().endswith(('.txt', '.md')) and
                            any(word in name.lower() for word in ('license', 'readme', 'credit')))
            if not (audio and matching) and not license_file:
                continue
            if member.file_size > LIMIT:
                raise RuntimeError('Unexpected uncompressed file size.')
            target = OUT / name
            if not target.exists():
                with archive.open(member) as source, target.open('xb') as destination:
                    shutil.copyfileobj(source, destination)
            digest = hashlib.sha256(target.read_bytes()).hexdigest()
            if digest != hashlib.sha256(archive.read(member)).hexdigest():
                raise RuntimeError('Existing local file does not match source: ' + name)
            found.update(matching)
            records.append({'member': member.filename, 'file': name,
                            'bytes': member.file_size, 'sha256': digest})
    manifest = {
        'author': 'Abstraction / Tallbeard Studios', 'source': PAGE,
        'album': 'https://abstractionmusic.bandcamp.com/album/three-red-hearts',
        'license': 'CC0, per the official itch.io pack page. Commercial use and modification allowed; attribution optional.',
        'purpose': 'Local audition only; no Unity Assets or BGM configuration changed.',
        'audio_changes': 'None. Files extracted byte-for-byte from the official pack.',
        'files': records, 'missing_selected_tracks': sorted(set(TRACKS) - found)}
    (OUT / 'sources.json').write_text(json.dumps(manifest, indent=2), encoding='utf-8')
    print(json.dumps(manifest, indent=2), flush=True)
    if set(TRACKS) - found:
        raise RuntimeError('Some selected tracks were not found; inspect archive listing.')


if __name__ == '__main__':
    main()
