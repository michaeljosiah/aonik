"""Apply kernel limits in a single-threaded child before replacing it with the reader."""
import os
import sys

from server import limits

limits()
os.execve(sys.argv[1], sys.argv[1:], os.environ)
