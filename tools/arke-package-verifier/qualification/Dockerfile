# Run additional kernel-resource fault checks against an already-qualified immutable image.
# This test-only layer is never deployed as the verifier.
ARG VERIFIER_IMAGE
FROM ${VERIFIER_IMAGE}
USER 0
COPY resource_tests.py /app/resource_tests.py
RUN chmod 0444 /app/resource_tests.py
USER 65532:65532
RUN python -B -m unittest -v resource_tests
