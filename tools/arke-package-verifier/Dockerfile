# Build context is the immutable workspace emitted by build.ps1. Supply a qualified SDK image by digest.
ARG DART_IMAGE
FROM ${DART_IMAGE} AS build
WORKDIR /build
COPY . .
RUN dart pub get --enforce-lockfile && cd verifier && dart analyze && dart test && dart compile exe bin/verify.dart -o /verify

FROM ${DART_IMAGE}
WORKDIR /app
RUN mkdir -p /input /scratch && test -x /usr/bin/timeout
COPY --from=build /verify /app/verify
COPY --from=build /build/reader-pin.json /app/reader-pin.json
USER 65532:65532
# The container terminates even if the API/Worker dies before its Docker cleanup runs.
ENTRYPOINT ["/usr/bin/timeout", "--signal=KILL", "90", "/app/verify"]
