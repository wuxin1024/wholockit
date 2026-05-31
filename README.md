# WhoLockIt

[![中文](https://img.shields.io/badge/lang-%E4%B8%AD%E6%96%87-blue)](README_zh-CN.md)

A Windows desktop tool to find out which process is locking your file or folder.

<img src="screenshot.png" width="600" alt="WhoLockIt screenshot" />

## Features

- **Drag & drop** any file or folder onto the window to scan
- **Dual scanning engine** — Restart Manager (no admin required) + NT API (admin, more thorough)
- **Three unlock modes** — Unlock selected / Unlock all / Unlock all and delete
- **Context menu integration** — Right-click any file or folder in Explorer to scan with WhoLockIt
- **Bilingual UI** — Chinese / English with runtime language toggle
- **Window topmost** toggle

## Requirements

- Windows 10 or later
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- Administrator privileges (for NT API scanning and handle unlock)

## Quick Start

Download the latest release from [Releases](../../releases).

**Option 1: Installer** — Run `WhoLockIt_Setup.msi` to install (Start Menu + Desktop shortcuts).

**Option 2: Portable** — Unzip `WhoLockIt_vX.X.X.zip` and run `WhoLockIt.exe` directly.

### Build from Source

```powershell
dotnet publish WhoLockIt/WhoLockIt.csproj -c Release -o publish
# Output: publish/WhoLockIt.exe
```

## Usage

| Action | How |
|--------|-----|
| Scan a file/folder | Drag & drop onto the window, paste a path, or use Explorer right-click menu |
| Unlock a single handle | Select a row in the results grid → **Unlock Selected** |
| Unlock all handles | **Unlock All** |
| Unlock all and delete file | **Unlock All & Delete** |
| Toggle language | Click the language button in the bottom bar |
| Toggle topmost | Click the topmost button in the bottom bar |
| Add Explorer context menu | Click **Add context menu** in the bottom bar |

## Test Locking

The `test_locks/` folder contains scripts to simulate file locks for testing:

```powershell
# Lock a file
.\test_locks\lock.bat

# Lock a folder
.\test_locks\lock_folder.bat

# Force-unlock (kill locking process)
.\test_locks\unlock.bat locked_file.txt
```

## License

MIT
