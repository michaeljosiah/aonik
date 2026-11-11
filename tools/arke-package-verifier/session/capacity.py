"""Image-owned capacity contract. No request or environment variable can raise these limits."""
from pathlib import Path

source = Path(__file__).with_name("capacity-profile")
PROFILE = source.read_text().strip() if source.exists() else "standard"
if PROFILE not in ("standard", "large-v1"):
    raise RuntimeError("Unknown compiled capacity profile")
LARGE = PROFILE == "large-v1"
MAX_PACKAGE = (1536 if LARGE else 32) * 1024 * 1024
MAX_CHUNK = (4 if LARGE else 1) * 1024 * 1024
MAX_ASSET = (64 if LARGE else 16) * 1024 * 1024
MAX_ASSETS = 2048 if LARGE else 1024
MAX_DOCUMENT = 4 * 1024 * 1024
MAX_OUTPUT = (1024 if LARGE else 256) * 1024
CPU_SECONDS = 180 if LARGE else 45
HARD_SECONDS = 840 if LARGE else 150
IDLE_SECONDS = 150
