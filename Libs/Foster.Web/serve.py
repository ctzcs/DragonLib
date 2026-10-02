"""Local static WebAssembly server; defaults to the published demo (localhost:8138)."""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

parser = argparse.ArgumentParser()
parser.add_argument('--root', type=Path, default=Path(__file__).parent / 'artifacts' / 'web')
parser.add_argument('--port', type=int, default=8138)
args = parser.parse_args()
root = args.root.resolve()
if not (root / 'index.html').exists():
    candidates = list(root.rglob('index.html'))
    if len(candidates) != 1:
        parser.error('Publish the demo first, or specify --root with the directory containing index.html.')
    root = candidates[0].parent

class Handler(SimpleHTTPRequestHandler):
    extensions_map = {**SimpleHTTPRequestHandler.extensions_map, '.wasm': 'application/wasm', '.mjs': 'text/javascript', '.js': 'text/javascript'}
    def end_headers(self):
        self.send_header('Cache-Control', 'no-cache')
        super().end_headers()

print(f'Serving {root} at http://localhost:{args.port}/', flush=True)
ThreadingHTTPServer(('127.0.0.1', args.port), partial(Handler, directory=str(root))).serve_forever()
