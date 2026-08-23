FROM mcr.microsoft.com/dotnet/runtime@sha256:a365ce6a50b09176855d085c69da3fc1204a48432e36087e9a208f6e5860e235

ARG DEBIAN_FRONTEND=noninteractive
ARG RIDEBOUND_RIDEPY_COMMIT=bf1863e49a432f2f1f6230f86b2777a5ef5b9f14
ARG RIDEBOUND_RIDEPY_TREE_SHA256=d99ffac89d4bcc09e04dcd82c9ae6b08ce5891cb2f39b1af18cd9e5311cd891e

LABEL org.opencontainers.image.source="https://github.com/PhysicsOfMobility/ridepy" \
      org.opencontainers.image.version="2.10.1" \
      ridebound.ridepy.commit="${RIDEBOUND_RIDEPY_COMMIT}" \
      ridebound.ridepy.tree-sha256="${RIDEBOUND_RIDEPY_TREE_SHA256}" \
      ridebound.ridepy.lru-cache-commit="13f30ad33a227a3e9682578c450777380ecddfcf" \
      ridebound.ridepy.googletest-commit="a2b8a8e07628e5fd60644b6dd99c1b5e7d7f1f47" \
      ridebound.base.digest="sha256:a365ce6a50b09176855d085c69da3fc1204a48432e36087e9a208f6e5860e235"

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        build-essential \
        cmake \
        libboost-all-dev \
        python3 \
        python3-dev \
        python3-pip \
        python3-venv \
    && rm -rf /var/lib/apt/lists/*

COPY . /opt/ridepy-source

RUN python3 - <<'PY'
import hashlib
import pathlib

root = pathlib.Path('/opt/ridepy-source')
rows = []
for path in root.rglob('*'):
    if not path.is_file() or '.git' in path.relative_to(root).parts:
        continue
    relative = path.relative_to(root).as_posix()
    rows.append(f"{relative}\t{hashlib.sha256(path.read_bytes()).hexdigest()}")
payload = ('\n'.join(sorted(rows)) + '\n').encode('utf-8')
actual = hashlib.sha256(payload).hexdigest()
expected = 'd99ffac89d4bcc09e04dcd82c9ae6b08ce5891cb2f39b1af18cd9e5311cd891e'
if len(rows) != 527 or actual != expected:
    raise SystemExit(f'RidePy source inventory mismatch: files={len(rows)} sha256={actual}')
license_hash = hashlib.sha256((root / 'LICENSE').read_bytes()).hexdigest()
if license_hash != '87e0c317105d31484c536b97fc7bc0789f4cc7b3c3df44f858b5fc49ed511798':
    raise SystemExit(f'RidePy license mismatch: {license_hash}')
PY

RUN python3 -m venv /opt/ridepy-env \
    && /opt/ridepy-env/bin/python -m pip install --disable-pip-version-check --upgrade pip \
    && /opt/ridepy-env/bin/python -m pip install --disable-pip-version-check setuptools==80.9.0 \
    && /opt/ridepy-env/bin/python -m pip install --disable-pip-version-check /opt/ridepy-source \
    && /opt/ridepy-env/bin/python -c "import importlib.metadata as m, ridepy; from ridepy.util.spaces import Graph; assert m.version('ridepy') == '2.10.1'; assert Graph(vertices=[0,1], edges=[(0,1)]).t(0,1) == 1" \
    && rm -rf /root/.cache/pip /opt/ridepy-source

ENV PATH="/opt/ridepy-env/bin:${PATH}" \
    PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1

WORKDIR /workspace/simulators/ridepy-ridebound

CMD ["python", "-c", "import importlib.metadata as m; print(m.version('ridepy'))"]
