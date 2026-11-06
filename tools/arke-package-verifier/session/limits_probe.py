"""Fixed kernel-limit qualification; no caller-supplied commands or arguments."""
import json
import mmap
import resource

from server import limits

limits()
refused = False
try:
    region = mmap.mmap(-1, 2 * 1024 * 1024 * 1024 + 1)
    region.close()
except (OSError, MemoryError):
    refused = True
print(json.dumps({"addressSpaceBytes": resource.getrlimit(resource.RLIMIT_AS)[0],
                  "cpuSeconds": resource.getrlimit(resource.RLIMIT_CPU)[0],
                  "processes": resource.getrlimit(resource.RLIMIT_NPROC)[0],
                  "fileBytes": resource.getrlimit(resource.RLIMIT_FSIZE)[0],
                  "oversizedAllocationDenied": refused}))
