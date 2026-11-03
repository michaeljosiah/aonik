"""Loopback-only starter-kit preview with a public documentation alias."""
import argparse
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit

KIT_ROOT = Path(__file__).resolve().parent.parent
DOCS_ROOT = KIT_ROOT.parent.parent / "docs"


class DesignHandler(SimpleHTTPRequestHandler):
    def translate_path(self, path):
        route = unquote(urlsplit(path).path)
        root = DOCS_ROOT if route.startswith("/docs/") else KIT_ROOT
        relative = route[6:] if route.startswith("/docs/") else route.lstrip("/")
        try:
            target = (root / relative).resolve()
            if target.is_relative_to(root.resolve()):
                return str(target)
        except (OSError, ValueError):
            pass
        return str(KIT_ROOT / "__not_found__")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--port", type=int, default=4173)
    args = parser.parse_args()
    print(f"Design preview: http://127.0.0.1:{args.port}/food-business.html", flush=True)
    ThreadingHTTPServer(("127.0.0.1", args.port), DesignHandler).serve_forever()
