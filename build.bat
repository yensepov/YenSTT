@echo off
setlocal
title YenSTT Release Build

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build-release.ps1" -Version 1.0.0
if errorlevel 1 (
    echo.
    echo Release build failed.
    pause
    exit /b 1
)

echo.
echo Release assets are ready in artifacts\release
pause
