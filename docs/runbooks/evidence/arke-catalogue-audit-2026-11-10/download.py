"""Read-only anonymous verification of the frozen catalogue's public pins."""
import concurrent.futures
import datetime
import hashlib
import json
import pathlib
import urllib.request

ROOT = pathlib.Path(__file__).resolve().parent
SOURCE = ROOT / 'hosted-catalogue.json'
CACHE = ROOT / 'assets'
CACHE.mkdir(exist_ok=True)


def pins(value, location=''):
    if isinstance(value, dict):
        if {'url', 'byteLength', 'sha256'} <= value.keys():
            yield {'location': location, **value}
        for key, child in value.items():
            yield from pins(child, location + '/' + key)
    elif isinstance(value, list):
        for index, child in enumerate(value):
            yield from pins(child, location + '/' + str(index))


def verify(pin):
    url = pin['url']
    if not url.startswith('https://github.com/michaeljosiah/ArkeStudio/releases/download/'):
        raise ValueError('Unexpected public source')
    digest = pin['sha256']
    if len(digest) != 64 or any(c not in '0123456789abcdef' for c in digest):
        raise ValueError('Invalid digest')
    is_zip = url.endswith('.zip')
    destination = CACHE / (digest + ('.zip' if is_zip else '.image'))
    expected = pin['byteLength']
    downloaded = False
    if not destination.exists():
        pending = destination.with_suffix('.partial')
        request = urllib.request.Request(url, headers={'User-Agent': 'Arke-catalogue-integrity-audit', 'Cache-Control': 'no-cache'})
        with urllib.request.urlopen(request, timeout=120) as response, pending.open('wb') as target:
            if response.status != 200 or not response.url.startswith('https://'):
                raise ValueError('Invalid public response')
            size = 0
            calculated = hashlib.sha256()
            while chunk := response.read(1024 * 1024):
                size += len(chunk)
                if size > expected:
                    raise ValueError('Public response exceeds pin')
                calculated.update(chunk)
                target.write(chunk)
        if size != expected or calculated.hexdigest() != digest:
            raise ValueError('Public pin mismatch: ' + pin['location'])
        pending.replace(destination)
        downloaded = True
    with destination.open('rb') as stream:
        actual = hashlib.file_digest(stream, 'sha256').hexdigest()
    if destination.stat().st_size != expected or actual != digest:
        raise ValueError('Retained byte mismatch')
    result = {'location': pin['location'], 'url': url, 'byteLength': expected, 'sha256': actual,
              'kind': 'archive' if is_zip else 'picture', 'anonymousDownload': True,
              'freshDownload': downloaded, 'verified': True}
    if not is_zip:
        from PIL import Image
        with Image.open(destination) as picture:
            result['image'] = {'format': picture.format, 'width': picture.width, 'height': picture.height}
            picture.verify()
    print(json.dumps({'verified': pin['location'], 'bytes': expected}), flush=True)
    return result


if __name__ == '__main__':
    catalogue = json.loads(SOURCE.read_bytes())
    inventory = list(pins(catalogue))
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as executor:
        results = list(executor.map(verify, inventory))
    report = {'checkedAtUtc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
              'serial': catalogue['serial'], 'catalogueSha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
              'anonymous': True, 'writesToRemote': False, 'pins': results}
    (ROOT / 'public-pins.json').write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'passed': len(results), 'archiveBytes': sum(r['byteLength'] for r in results if r['kind'] == 'archive')}), flush=True)
