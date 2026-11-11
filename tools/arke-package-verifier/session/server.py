"""Single-use, credential-free transport for the pinned reader. Linux only."""
import ctypes
import fcntl
import hashlib
import http.server
import json
import os
import re
import resource
import selectors
import signal
import socket
import subprocess
import sys
import threading
import time
import tempfile
import urllib.parse

from capacity import (PROFILE, LARGE, MAX_PACKAGE, MAX_CHUNK, MAX_ASSET, MAX_ASSETS,
                      MAX_DOCUMENT, MAX_OUTPUT, CPU_SECONDS, HARD_SECONDS, IDLE_SECONDS)
READER = "27df122566bcdef18126932c15a74112e18b8573"
SEALS = fcntl.F_SEAL_WRITE | fcntl.F_SEAL_GROW | fcntl.F_SEAL_SHRINK | fcntl.F_SEAL_SEAL


class Refused(Exception):
    pass


class Job:
    def __init__(self, declaration):
        if (set(declaration) != {"nonce", "sha256", "bytes", "maxAssets", "maxAssetBytes", "maxDocumentBytes", "timeoutSeconds"}
                or not re.fullmatch(r"[a-f0-9]{64}", str(declaration["nonce"]))
                or not re.fullmatch(r"[a-f0-9]{64}", str(declaration["sha256"]))):
            raise Refused("invalid-declaration")
        for key, maximum in (("bytes", MAX_PACKAGE), ("maxAssets", MAX_ASSETS), ("maxAssetBytes", MAX_ASSET),
                             ("maxDocumentBytes", MAX_DOCUMENT), ("timeoutSeconds", CPU_SECONDS)):
            if type(declaration[key]) is not int or not 1 <= declaration[key] <= maximum:
                raise Refused("invalid-limits")
        self.declaration = declaration
        self.fd = os.memfd_create("package", os.MFD_ALLOW_SEALING | os.MFD_CLOEXEC)
        self.offset = 0
        self.hash = hashlib.sha256()
        self.finished = False
        self.metrics = {}
        self.scratch_peak = 0

    def append(self, offset, data):
        if self.finished or offset != self.offset or not 0 < len(data) <= MAX_CHUNK or self.offset + len(data) > self.declaration["bytes"]:
            raise Refused("upload-conflict")
        remaining = memoryview(data)
        while remaining:
            remaining = remaining[os.write(self.fd, remaining):]
        self.hash.update(data)
        self.offset += len(data)

    def seal(self):
        if self.finished or self.offset != self.declaration["bytes"] or self.hash.hexdigest() != self.declaration["sha256"]:
            raise Refused("archive-identity-mismatch")
        self.finished = True
        fcntl.fcntl(self.fd, fcntl.F_ADD_SEALS, SEALS)
        if fcntl.fcntl(self.fd, fcntl.F_GET_SEALS) != SEALS:
            raise Refused("input-not-sealed")

    def close(self):
        if self.fd >= 0:
            os.close(self.fd)
            self.fd = -1


def limits():
    # Applied in the child before exec; the parent contains no storage credentials.
    resource.setrlimit(resource.RLIMIT_CPU, (CPU_SECONDS, CPU_SECONDS))
    resource.setrlimit(resource.RLIMIT_NPROC, (32, 32))
    resource.setrlimit(resource.RLIMIT_NOFILE, (64, 64))
    resource.setrlimit(resource.RLIMIT_FSIZE, (MAX_ASSET, MAX_ASSET))
    resource.setrlimit(resource.RLIMIT_CORE, (0, 0))
    resource.setrlimit(resource.RLIMIT_AS, (2 * 1024 * 1024 * 1024, 2 * 1024 * 1024 * 1024))
    if ctypes.CDLL(None).prctl(38, 1, 0, 0, 0) != 0:  # PR_SET_NO_NEW_PRIVS
        os._exit(126)


def execute(job, executable="/app/verify"):
    job.seal()
    started = time.monotonic()
    before = resource.getrusage(resource.RUSAGE_CHILDREN)
    with tempfile.TemporaryDirectory(prefix="verification-", dir="/dev/shm") as scratch:
        try:
            return execute_reader(job, executable, scratch)
        finally:
            after = resource.getrusage(resource.RUSAGE_CHILDREN)
            job.metrics = {"verificationSeconds": round(time.monotonic() - started, 3),
                           "childCpuSeconds": round(after.ru_utime + after.ru_stime - before.ru_utime - before.ru_stime, 3),
                           "childPeakRssKiB": after.ru_maxrss, "inputBytes": job.offset,
                           "scratchLimitBytes": 64 * 1024 * 1024, "sampledScratchPeakBytes": job.scratch_peak}


def execute_reader(job, executable, scratch):
    d = job.declaration
    args = [executable, f"/proc/self/fd/{job.fd}", d["sha256"], str(d["bytes"]), str(d["maxAssets"]),
            str(d["maxAssetBytes"]), str(d["maxDocumentBytes"]), scratch]
    if LARGE and executable == "/app/verify":
        args.append(str(MAX_OUTPUT))
    # A fresh interpreter applies limits before exec. Never fork with a Python preexec_fn while the watchdog thread is running.
    process = subprocess.Popen([sys.executable, "-B", "/app/child.py", *args], stdin=subprocess.DEVNULL, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
                               pass_fds=(job.fd,), env={"LANG": "C.UTF-8", "HOME": "/nonexistent", "TMPDIR": "/dev/shm"},
                               start_new_session=True)
    output = {"stdout": bytearray(), "stderr": bytearray()}
    deadline = time.monotonic() + d["timeoutSeconds"]
    try:
        with selectors.DefaultSelector() as selector:
            selector.register(process.stdout, selectors.EVENT_READ, "stdout")
            selector.register(process.stderr, selectors.EVENT_READ, "stderr")
            while selector.get_map():
                volume = os.statvfs(scratch)
                job.scratch_peak = max(job.scratch_peak, (volume.f_blocks - volume.f_bfree) * volume.f_frsize)
                if time.monotonic() >= deadline:
                    raise Refused("verifier-timeout")
                for key, _ in selector.select(min(0.2, max(0, deadline - time.monotonic()))):
                    data = os.read(key.fileobj.fileno(), 4096)
                    if not data:
                        selector.unregister(key.fileobj)
                        continue
                    stream = output[key.data]
                    if len(stream) + len(data) > (MAX_OUTPUT if key.data == "stdout" else 8192):
                        raise Refused("verifier-output-limit")
                    stream.extend(data)
        if process.wait(timeout=max(0.01, deadline - time.monotonic())) != 0:
            raise Refused("verifier-resource-limit")
        result = json.loads(output["stdout"])
        if not isinstance(result, dict) or result.get("readerCommit") != READER:
            raise Refused("verifier-protocol-invalid")
        return result
    finally:
        # Descendants may outlive the reader even when it exits successfully.
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        process.wait()
        process.stdout.close()
        process.stderr.close()


class Handler(http.server.BaseHTTPRequestHandler):
    server_version = "Verifier/1"

    def log_message(self, *_):
        pass  # Never log authorization headers, identifiers or creator content.

    def setup(self):
        super().setup()
        self.connection.settimeout(10)

    def reply(self, status, value):
        body = json.dumps(value, separators=(",", ":")).encode()
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.end_headers()
        self.wfile.write(body)

    def body(self, maximum):
        lengths = self.headers.get_all("Content-Length", [])
        if len(lengths) != 1 or self.headers.get("Transfer-Encoding"):
            raise Refused("invalid-length")
        length = int(lengths[0])
        if not 0 <= length <= maximum:
            raise Refused("request-too-large")
        data = self.rfile.read(length)
        if len(data) != length:
            raise Refused("incomplete-request")
        return data

    def do_GET(self):
        if urllib.parse.urlsplit(self.path).path == "/health":
            self.reply(200, {"ready": True})
        else:
            self.reply(404, {"code": "not-found"})

    def do_POST(self):
        try:
            path = urllib.parse.urlsplit(self.path).path
            if path == "/qualify" and self.server.job is None:
                self.body(0)
                # Fixed probes only: callers cannot choose a URL, command, path or code.
                network = {}
                for label, host, port in (("public", "1.1.1.1", 443), ("metadata", "169.254.169.254", 80)):
                    try:
                        with socket.create_connection((host, port), timeout=2):
                            network[label] = "reachable"
                    except OSError:
                        network[label] = "denied"
                bounds = {}
                for name in ("memory.max", "cpu.max", "pids.max"):
                    try:
                        with open("/sys/fs/cgroup/" + name) as source:
                            bounds[name] = source.read(128).strip()
                    except OSError:
                        bounds[name] = "unavailable"
                # Dynamic Sessions may hide the cgroup mount. Independently prove the child kernel limits.
                probe = subprocess.run([sys.executable, "-B", "/app/limits_probe.py"], stdin=subprocess.DEVNULL,
                                       stdout=subprocess.PIPE, stderr=subprocess.DEVNULL, timeout=10, check=True,
                                       env={"LANG": "C.UTF-8", "HOME": "/nonexistent", "TMPDIR": "/dev/shm"})
                child_limits = json.loads(probe.stdout)
                scratch = os.statvfs("/dev/shm")
                self.reply(200, {"uid": os.geteuid(), "readerCommit": READER, "network": network, "cgroup": bounds,
                                 "capacityProfile": PROFILE, "maxPackageBytes": MAX_PACKAGE, "maxAssets": MAX_ASSETS,
                                 "maxReportBytes": MAX_OUTPUT, "maxChunkBytes": MAX_CHUNK, "hardDeadlineSeconds": HARD_SECONDS,
                                 "childLimits": child_limits,
                                 "scratchBytes": scratch.f_blocks * scratch.f_frsize,
                                 "writableRuntime": any(os.access(p, os.W_OK) for p in ("/app", "/tmp", "/var/tmp"))})
                return
            if path == "/begin" and self.server.job is None:
                self.server.job = Job(json.loads(self.body(4096)))
                # The deadline never renews on traffic, even if the caller disappears.
                self.server.watchdog = threading.Timer(HARD_SECONDS, lambda: os._exit(124))
                self.server.watchdog.daemon = True
                self.server.watchdog.start()
                self.server.last_upload = time.monotonic()
                self.server.started = False
                self.server.result = None
                self.server.error = None
                if LARGE:
                    def check_idle():
                        while not self.server.started:
                            if time.monotonic() - self.server.last_upload >= IDLE_SECONDS:
                                os._exit(124)
                            time.sleep(1)
                    threading.Thread(target=check_idle, daemon=True).start()
                self.reply(200, {"nonce": self.server.job.declaration["nonce"], "offset": 0, "capacityProfile": PROFILE})
                return
            job = self.server.job
            if job is None or self.headers.get("X-Verification-Nonce") != job.declaration["nonce"]:
                raise Refused("invalid-job")
            if path == "/chunk":
                if self.server.started:
                    raise Refused("upload-conflict")
                job.append(int(self.headers.get("X-Upload-Offset", "-1")), self.body(MAX_CHUNK))
                self.server.last_upload = time.monotonic()
                self.reply(200, {"nonce": job.declaration["nonce"], "offset": job.offset})
            elif path == "/verify":
                if self.body(0):
                    raise Refused("unexpected-body")
                if self.server.started:
                    raise Refused("job-already-started")
                self.server.started = True
                if LARGE:
                    def verify():
                        try:
                            self.server.result = execute(job)
                        except (Refused, ValueError, KeyError, TypeError, OSError, subprocess.SubprocessError) as error:
                            self.server.error = str(error) if isinstance(error, Refused) else "verifier-unavailable"
                        finally:
                            job.close()
                    threading.Thread(target=verify, daemon=True).start()
                    self.reply(200, {"nonce": job.declaration["nonce"], "status": "running"})
                    return
                try:
                    result = execute(job)
                    self.reply(200, {"nonce": job.declaration["nonce"], "result": result, "metrics": job.metrics})
                finally:
                    job.close()
            elif path == "/result" and LARGE and self.server.started:
                self.body(0)
                if self.server.error:
                    raise Refused(self.server.error)
                if self.server.result is None:
                    self.reply(200, {"nonce": job.declaration["nonce"], "status": "running"})
                else:
                    self.reply(200, {"nonce": job.declaration["nonce"], "result": self.server.result, "metrics": job.metrics})
            else:
                raise Refused("invalid-operation")
        except (Refused, ValueError, KeyError, TypeError, OSError, subprocess.SubprocessError) as error:
            code = str(error) if isinstance(error, Refused) else "verifier-unavailable"
            self.reply(422, {"code": code})


def main():
    if os.geteuid() != 65532:
        raise RuntimeError("The verifier must run as its unprivileged identity")
    # Prevent the parser from inspecting the parent's HTTP authentication headers.
    if ctypes.CDLL(None).prctl(4, 0, 0, 0, 0) != 0:  # PR_SET_DUMPABLE
        raise RuntimeError("Cannot isolate transport memory")
    scratch = os.statvfs("/dev/shm")
    if scratch.f_blocks * scratch.f_frsize > 64 * 1024 * 1024 or scratch.f_bavail * scratch.f_frsize < MAX_ASSET:
        raise RuntimeError("Unqualified scratch volume")
    for directory in ("/app", "/tmp", "/var/tmp"):
        if os.access(directory, os.W_OK):
            raise RuntimeError("Writable runtime directory")
    with http.server.HTTPServer(("0.0.0.0", 8080), Handler) as server:
        server.job = None
        server.serve_forever()


if __name__ == "__main__":
    main()
