@echo off
rem Lanseaza SirilCleanup.ps1 cu dublu click, ocolind politica de executie a PowerShell.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0SirilCleanup.ps1"
