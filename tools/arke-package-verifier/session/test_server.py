import fcntl
import hashlib
import http.client
import json
import os
import sys
import subprocess
import time
import unittest

from server import Job, Refused, SEALS, execute


def job_for(data, timeout=2):
    job = Job({"nonce": "a" * 64, "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data),
               "maxAssets": 1024, "maxAssetBytes": 16777216, "maxDocumentBytes": 4194304, "timeoutSeconds": timeout})
    job.append(0, data)
    return job


class SessionTests(unittest.TestCase):
    def test_http_transport_verifies_exact_frozen_input(self):
        process = subprocess.Popen([sys.executable, '-B', '/app/server.py'], stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
        try:
            for _ in range(30):
                if process.poll() is not None:
                    self.fail(process.stderr.read().decode())
                try:
                    connection = http.client.HTTPConnection('127.0.0.1', 8080, timeout=2)
                    connection.request('GET', '/health')
                    self.assertEqual(connection.getresponse().status, 200)
                    connection.close()
                    break
                except OSError:
                    time.sleep(0.1)
            with open('/app/fixtures/comic.zip', 'rb') as stream:
                data = stream.read()
            declaration = {'nonce':'a'*64,'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),
                           'maxAssets':1024,'maxAssetBytes':16777216,'maxDocumentBytes':4194304,'timeoutSeconds':10}
            def post(path, body, headers=None):
                connection = http.client.HTTPConnection('127.0.0.1',8080,timeout=15)
                try:
                    connection.request('POST',path,body,headers or {})
                    response=connection.getresponse()
                    result=json.loads(response.read())
                    self.assertEqual(response.status,200,result)
                    return result
                finally:
                    connection.close()
            post('/begin',json.dumps(declaration))
            post('/chunk',data,{'X-Verification-Nonce':'a'*64,'X-Upload-Offset':'0'})
            result=post('/verify',b'',{'X-Verification-Nonce':'a'*64})
            self.assertTrue(result['result']['passed'],result)
            self.assertEqual(result['result']['archiveSha256'],declaration['sha256'])
        finally:
            process.terminate()
            process.wait(timeout=5)
            process.stderr.close()

    def test_sealed_input_cannot_be_changed_even_by_same_uid(self):
        job = job_for(b"abc")
        try:
            job.seal()
            self.assertEqual(fcntl.fcntl(job.fd, fcntl.F_GET_SEALS), SEALS)
            with self.assertRaises(OSError):
                os.pwrite(job.fd, b"x", 0)
            with self.assertRaises(OSError):
                os.ftruncate(job.fd, 0)
        finally:
            job.close()

    def test_offset_conflict_and_short_upload_are_refused(self):
        job = job_for(b"abc")
        try:
            with self.assertRaisesRegex(Refused, "upload-conflict"):
                job.append(0, b"abc")
            job.declaration["bytes"] += 1
            with self.assertRaisesRegex(Refused, "archive-identity-mismatch"):
                job.seal()
        finally:
            job.close()

    def test_changed_archive_is_refused(self):
        job = job_for(b"abc")
        try:
            job.declaration["sha256"] = "b" * 64
            with self.assertRaisesRegex(Refused, "archive-identity-mismatch"):
                job.seal()
        finally:
            job.close()

    def test_one_job_cannot_be_reused(self):
        job = job_for(b"abc")
        try:
            job.seal()
            with self.assertRaises(Refused):
                job.seal()
        finally:
            job.close()

    def run_probe(self, source, timeout=2):
        # Only this test harness selects Python. The HTTP endpoint always invokes the pinned Dart reader.
        job = job_for(source.encode(), timeout)
        try:
            return execute(job, sys.executable)
        finally:
            job.close()

    def test_reader_has_resource_limits_no_credentials_and_sealed_input(self):
        result = self.run_probe('''import os,resource,json,fcntl,sys
assert resource.getrlimit(resource.RLIMIT_NPROC)==(32,32)
assert resource.getrlimit(resource.RLIMIT_NOFILE)==(64,64)
assert resource.getrlimit(resource.RLIMIT_FSIZE)==(16777216,16777216)
assert resource.getrlimit(resource.RLIMIT_CPU)==(45,45)
assert resource.getrlimit(resource.RLIMIT_AS)==(2147483648,2147483648)
assert set(os.environ)<=set(['LANG','HOME','TMPDIR','LC_CTYPE'])
assert fcntl.fcntl(int(sys.argv[0].split('/')[-1]),fcntl.F_GET_SEALS)==15
assert 'NoNewPrivs:\\t1' in open('/proc/self/status').read()
print(json.dumps({'readerCommit':'27df122566bcdef18126932c15a74112e18b8573','passed':True}))
''')
        self.assertTrue(result["passed"])

    def test_timeout_terminates_reader(self):
        with self.assertRaisesRegex(Refused, "verifier-timeout"):
            self.run_probe("while True: pass", timeout=1)

    def test_unbounded_output_is_terminated(self):
        with self.assertRaisesRegex(Refused, "verifier-output-limit"):
            self.run_probe("print('x'*300000)")

    def test_pinned_reader_consumes_sealed_memfd(self):
        with open('/app/fixtures/story.zip', 'rb') as stream:
            job = job_for(stream.read(), timeout=10)
        try:
            report = execute(job)
            self.assertTrue(report['passed'], json.dumps(report))
            self.assertEqual(report['archiveSha256'], job.declaration['sha256'])
        finally:
            job.close()


if __name__ == "__main__":
    unittest.main()
