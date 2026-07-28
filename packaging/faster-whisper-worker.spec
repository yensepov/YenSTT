# -*- mode: python ; coding: utf-8 -*-
import os

from PyInstaller.utils.hooks import collect_data_files, collect_dynamic_libs


project_root = os.path.abspath(os.path.join(SPECPATH, ".."))
worker_script = os.path.join(project_root, "Python", "faster_whisper_worker.py")

datas = collect_data_files("faster_whisper")
binaries = collect_dynamic_libs("ctranslate2")
excludes = [
    "cryptography",
    "lxml",
    "matplotlib",
    "pandas",
    "PIL",
    "pyarrow",
    "pydantic",
    "pytest",
    "scipy",
    "tensorflow",
    "torch",
]

a = Analysis(
    [worker_script],
    pathex=[project_root],
    binaries=binaries,
    datas=datas,
    hiddenimports=[],
    hookspath=[],
    hooksconfig={},
    runtime_hooks=[],
    excludes=excludes,
    noarchive=False,
    optimize=1,
)
pyz = PYZ(a.pure)

exe = EXE(
    pyz,
    a.scripts,
    [],
    exclude_binaries=True,
    name="faster-whisper-worker",
    debug=False,
    bootloader_ignore_signals=False,
    strip=False,
    upx=True,
    console=True,
)

coll = COLLECT(
    exe,
    a.binaries,
    a.datas,
    strip=False,
    upx=True,
    upx_exclude=[],
    name="faster-whisper-worker",
)
