"""Compile/run portable production logic; no board or simulated hardware claims."""
from pathlib import Path
import os
import subprocess

ROOT = Path(__file__).resolve().parents[1]
GCC = Path.home() / '.platformio/packages/toolchain-gccmingw32/bin/g++.exe'
os.environ['PATH'] = str(GCC.parent) + os.pathsep + os.environ['PATH']
BUILD = ROOT / 'tests/host/build'
BUILD.mkdir(exist_ok=True)
for source in sorted((ROOT / 'tests/host').glob('*_test.cpp')):
    target = BUILD / (source.stem + '.exe')
    extra = [str(ROOT / 'src/MeasurementQueue.cpp')] if source.stem == 'queue_test' else []
    if source.stem == 'time_test': extra = [str(ROOT / 'src/TimeManager.cpp')]
    subprocess.run([str(GCC), '-std=c++17', '-Wall', '-Wextra', '-Werror',
                    '-I' + str(ROOT / 'include'), '-I' + str(ROOT / 'src'),
                    '-I' + str(ROOT / '.pio/libdeps/nodemcuv2/ArduinoJson/src'),
                    str(source), *extra, '-o', str(target)], check=True)
    subprocess.run([str(target)], check=True)
