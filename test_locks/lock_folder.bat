@echo off
powershell -ExecutionPolicy Bypass -File "%~dp0lock_folder.ps1" %*
