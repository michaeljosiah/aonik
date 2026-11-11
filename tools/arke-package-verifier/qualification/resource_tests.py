"""Fault probes for the exact runtime image; no HTTP caller can select these programs."""
import os
import unittest

import test_server
from server import Refused


class CapacityResourceTests(test_server.SessionTests):
    def test_full_scratch_is_refused_and_next_job_recovers(self):
        before = set(os.listdir('/dev/shm'))
        # Several files stay within each file's limit while exhausting the actual 64 MiB tmpfs.
        result = self.run_probe('''import errno, json, os, sys
try:
    for number in range(65):
        with open(sys.argv[6] + '/fill-' + str(number), 'wb') as target:
            target.write(b'x' * 1048576)
except OSError as error:
    assert error.errno == errno.ENOSPC
    print(json.dumps({'readerCommit':'27df122566bcdef18126932c15a74112e18b8573','passed':False,'code':'scratch-exhausted'}))
    sys.exit(0)
raise AssertionError('Scratch volume did not enforce its limit')
''', timeout=10)
        self.assertFalse(result['passed'])
        self.assertEqual(result['code'], 'scratch-exhausted')
        self.assertEqual(set(os.listdir('/dev/shm')), before)
        self.test_pinned_reader_consumes_sealed_memfd()

    def test_killed_reader_reclaims_scratch_and_next_job_recovers(self):
        before = set(os.listdir('/dev/shm'))
        with self.assertRaisesRegex(Refused, 'verifier-resource-limit'):
            self.run_probe("import os,sys,signal; open(sys.argv[6]+'/leftover','wb').write(b'x'*4096); os.kill(os.getpid(),signal.SIGKILL)")
        self.assertEqual(set(os.listdir('/dev/shm')), before)
        self.test_pinned_reader_consumes_sealed_memfd()


if __name__ == '__main__':
    unittest.main()
