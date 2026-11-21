# PlayStation Universal Converter

[简体中文](README.md)

A Windows 10/11 x64 utility for working with PS1, PSP, and PS2 disc images. Drop in an image to identify its platform and pick an output for your intended use. The app also includes a PSP menu artwork editor. Source version **2.0.3**, by **metalmouse on Bilibili**.

## Features

| Input | For emulators | For hardware or extraction |
| --- | --- | --- |
| PS1 images | CHD. Existing unencrypted PS1 PBP files are accepted. Multi-disc sets can produce one CHD per disc and an M3U playlist | Single- or multi-disc `EBOOT.PBP` for PSP / PS Vita. A single PS1 CHD can also be extracted to BIN + CUE |
| PSP images | CHD | ISO. An existing ISO can be used directly |
| PS2 images | CHD once the app confirms CD or DVD media. An existing CHD can be used directly | Extract ISO from DVD CHD or from a supported single-data-track CD CHD |

Supported inputs include ISO, BIN/CUE, CHD, and unencrypted PS1 PBP. Open the original CUE for multi-track discs. For a PS1 multi-disc set, select two to five files with consecutive disc numbers, such as `Game (CD1).cue` and `Game (CD2).cue`. Conversions default to the source folder and offer a new destination if a file already exists.

The app uses different chdman commands for PS2 CDs and DVDs. A file extension or small 2,048-byte-sector image alone may leave the disc type ambiguous. DVD CHD extraction yields an ISO. CD CHD extraction requires one data track that can be safely reduced to 2,048 bytes of user data per sector. The app declines audio tracks, Form 2 sectors, nonzero data-track starts, and unrecognized disc types. On PS2 hardware, loading an ISO requires a compatible image-loading setup.

## PSP menu artwork

Use the **PSP 图标/背景设计** tab. Import a PNG, JPEG, BMP, or GIF. Enter or import UTF-8 text and adjust the crop, position, size, and color. The right pane previews the PSP menu.

- Icon output is `ICON0.PNG` at 144 × 80. Background output is `PIC1.PNG` at 480 × 272.
- Open a PSP ISO or EBOOT.PBP to read its artwork. Export standalone PNGs or save the edited artwork in a new copy of the game file. Some ISO layouts, including supplementary directory trees, multi-extent records, and directories without enough room, cannot be edited directly.
- Text is rendered into the image. The game's existing `ICON1.PMF` still supplies any animated icon. Convert CHD back to ISO before opening it in the artwork editor.

## Software and components used

| Component | Role in this app | Upstream and license |
| --- | --- | --- |
| MAME chdman 0.289 | Inspect CHDs and create or extract PS1, PSP, and PS2 CD, DVD, and UMD images | [MAME 0.289](https://github.com/mamedev/mame/releases/tag/mame0289) · [licensing](https://github.com/mamedev/mame/blob/mame0289/COPYING) · [command reference](https://docs.mamedev.org/tools/chdman.html) |
| PSXPackager 1.6.3 CLI | Package PS1 images as single- or multi-disc EBOOT.PBP files | [PSXPackager 1.6.3](https://github.com/RupertAvery/PSXPackager/releases/tag/v1.6.3) · [MIT license](https://github.com/RupertAvery/PSXPackager/blob/v1.6.3/LICENSE) |
| Windows Forms / .NET Framework | Desktop interface, image processing, and application runtime | [Microsoft Windows Forms documentation](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/) |
| Microsoft .NET Runtime 8 x64 | Private runtime for PSXPackager in the local portable build. Tested here with 8.0.30 | [.NET 8 downloads](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) · [runtime source and license](https://github.com/dotnet/runtime) |

This project implements image detection, format selection, multi-disc workflows, the interface, and the artwork editor. The PBP resource layout follows [PSPSDK's pack-pbp](https://github.com/pspdev/pspsdk/blob/master/tools/pack-pbp.c). Each upstream component retains its own license. This project's own source has no declared open-source license.

## Build on Windows

Use Windows 10/11 x64, Windows PowerShell or PowerShell 7, the system .NET Framework, and Python 3.10 or later. From the repository root:

1. Get the Windows x64 chdman and PSXPackager CLI from the official releases linked above. Extract chdman with MAME's `COPYING`, and place the PSXPackager CLI executable, `LICENSE`, and complete `Resources` directory as shown below.
2. Install or extract Microsoft .NET Runtime 8 x64. The default is 8.0.30 under `%ProgramFiles%\dotnet`. For a different 8.0.x patch, use `--runtime-version 8.0.x` and, if needed, `--dotnet-root` to point to its installation.

```text
dependencies/
  chdman.exe
  COPYING
  psxpackager/
    psxpackager.exe
    LICENSE
    Resources/
```

```powershell
python -m pip install Pillow
python .\prepare.py
.\build.ps1
```

Run the generated `PlayStation万能转换姬.exe`. The first launch extracts the tools and runtime under `%LOCALAPPDATA%\CHDConverter\tools\`. Later launches verify and reuse them. The preparation script generates geometric artwork. To use your own artwork, place images you have the rights to use at `assets/playstation.png` and `assets/character.png` before building.

Check the input hashes with `Get-FileHash`. The MAME 0.289 `chdman.exe` used for testing has SHA-256 `8a74468e3b0879698835b57c3b58e88e5a51e4de73bee6ef755c28530b5b040f`. The PSXPackager 1.6.3 `psxpackager.exe` has SHA-256 `8bcb32e399ffd5733decaca2641e64722688360190cba77ee67630c0d1eb8303`.

## Offline checks

```powershell
python -m pip install Pillow pycdlib
python .\tests\fixtures.py
.\tests\run.ps1
.\tests\ps2-iso.ps1 -Fixture .\tests\fixtures\ps2-small.iso
```

Synthetic fixtures exercise format detection, CHD round trips, multi-disc PS1 PBP construction, menu artwork replacement, output collision handling, and PS2 DVD and single-track CD extraction. Real hardware booting, disc switching, and individual emulator compatibility require separate testing. This repository provides source, build instructions, and fixture generators. Supply game images and upstream tool binaries locally, then build the EXE.
