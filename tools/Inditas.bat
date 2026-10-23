@echo off
chcp 65001 >nul
title Bloch-gomb szimulator
cd /d "%~dp0"

echo.
echo  Bloch-gomb szimulator - inditas...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0szerver.ps1"

echo.
echo  A szerver leallt.
pause
