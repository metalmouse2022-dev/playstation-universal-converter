# PlayStation 万能转换姬

[English](README.en.md)

适用于 Windows 10/11 x64 的 PS1、PSP、PS2 镜像转换工具。拖入镜像即可识别平台，按用途选择输出格式。内置 PSP 菜单图标和背景编辑器。当前源码版本为 **2.0.3**，制作署名为 **bilibili: metalmouse**。

## 功能

| 输入 | 模拟器用途 | 实机或镜像提取 |
| --- | --- | --- |
| PS1 镜像 | CHD，支持已有未加密 PS1 PBP。多碟可生成各碟 CHD 和 M3U | 为 PSP / PSV 生成单碟或多碟 `EBOOT.PBP`。单个 PS1 CHD 可导出 BIN + CUE |
| PSP 镜像 | CHD | ISO。输入已是 ISO 时可直接使用 |
| PS2 镜像 | 确认 CD 或 DVD 后生成 CHD。已有 CHD 可直接使用 | DVD CHD 或符合下述条件的单数据轨 CD CHD 可还原为 ISO |

支持识别 ISO、BIN/CUE、CHD 和未加密的 PS1 PBP。多轨光盘请优先打开原始 CUE。PS1 合盘一次选择 2–5 个带有连续盘号的文件，例如 `Game (CD1).cue` 和 `Game (CD2).cue`。转换默认输出到原镜像旁，遇到同名文件会提示另存。

PS2 的 CD、DVD 分别使用 chdman 的 CD、DVD 命令。仅凭后缀或较小的 2048 字节扇区 ISO 无法确认介质时，程序会说明原因。PS2 CHD 还原时，DVD 直接提取 ISO，CD 只支持能安全提取 2048 字节用户数据的单数据轨。遇到音轨、Form 2、非零数据轨起点或未知介质时会拒绝这次还原。ISO 实机使用需要支持镜像加载的环境。

## PSP 图标和背景

打开「PSP图标/背景设计」，导入 PNG、JPEG、BMP 或 GIF，添加或导入 UTF-8 文本，再调整裁切、位置、字号和颜色。界面实时预览 PSP 菜单效果。

- 图标输出 `ICON0.PNG`，144 × 80。背景输出 `PIC1.PNG`，480 × 272。
- 可打开 PSP ISO 或 EBOOT.PBP 读取原图，导出 PNG 或将编辑后的图片写入另存的游戏副本。某些带有额外目录树、分段记录或目录空间不足的 ISO 无法直接保存。
- 文字会绘制进图片。动态菜单图标仍取自原游戏的 `ICON1.PMF`。CHD 请先转回 ISO 再进入设计页。

## 基于的软件和组件

| 组件 | 在本工具中的用途 | 上游和许可 |
| --- | --- | --- |
| MAME chdman 0.289 | 读取 CHD 信息，创建或提取 PS1、PSP、PS2 的 CD、DVD 和 UMD 镜像 | [MAME 0.289](https://github.com/mamedev/mame/releases/tag/mame0289) · [许可说明](https://github.com/mamedev/mame/blob/mame0289/COPYING) · [命令文档](https://docs.mamedev.org/tools/chdman.html) |
| PSXPackager 1.6.3 CLI | 将 PS1 光盘镜像打包成单碟或多碟 EBOOT.PBP | [PSXPackager 1.6.3](https://github.com/RupertAvery/PSXPackager/releases/tag/v1.6.3) · [MIT 许可](https://github.com/RupertAvery/PSXPackager/blob/v1.6.3/LICENSE) |
| Windows Forms / .NET Framework | 桌面界面、图片处理及主程序运行时 | [Microsoft Windows Forms 文档](https://learn.microsoft.com/zh-cn/dotnet/desktop/winforms/) |
| Microsoft .NET Runtime 8 x64 | 便携包内供 PSXPackager 使用的运行库。本地验证使用 8.0.30 | [.NET 8 下载](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) · [运行库源码与许可](https://github.com/dotnet/runtime) |

镜像识别与分流、合盘流程、界面和封面编辑由本项目实现。PBP 资源结构参考 [PSPSDK 的 pack-pbp](https://github.com/pspdev/pspsdk/blob/master/tools/pack-pbp.c)。上游组件各自适用其许可证。本项目源码尚未声明开源许可证。

## 在 Windows 上构建

准备 Windows 10/11 x64、Windows PowerShell 或 PowerShell 7、系统 .NET Framework 和 Python 3.10 及以上。然后在仓库根目录操作：

1. 从上表的官方发布下载 Windows x64 版 chdman 和 PSXPackager 命令行版。取出 chdman 和 MAME 的 `COPYING`，再将 PSXPackager CLI 的可执行文件、`LICENSE` 与完整的 `Resources` 目录放到以下位置。
2. 安装或解压 Microsoft .NET 8 x64 Runtime。默认读取 `%ProgramFiles%\dotnet` 下的 8.0.30。使用其他 8.0.x 补丁版时，传入 `--runtime-version 8.0.x`，必要时加 `--dotnet-root` 指向实际安装目录。

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

双击根目录生成的 `PlayStation万能转换姬.exe`。首次运行将工具和运行时释放到 `%LOCALAPPDATA%\CHDConverter\tools\`，后续校验并复用。构建脚本生成默认几何图标与背景，也可在构建前将有权使用的自定义图像放入 `assets/playstation.png` 和 `assets/character.png`。

验证输入版本可用 `Get-FileHash`。用于本仓库复测的 MAME 0.289 `chdman.exe` SHA-256 为 `8a74468e3b0879698835b57c3b58e88e5a51e4de73bee6ef755c28530b5b040f`，PSXPackager 1.6.3 `psxpackager.exe` SHA-256 为 `8bcb32e399ffd5733decaca2641e64722688360190cba77ee67630c0d1eb8303`。

## 离线验证

```powershell
python -m pip install Pillow pycdlib
python .\tests\fixtures.py
.\tests\run.ps1
.\tests\ps2-iso.ps1 -Fixture .\tests\fixtures\ps2-small.iso
```

合成样例覆盖平台识别、CHD 往返、PS1 多碟 PBP、菜单图片写入、同名保护，以及 PS2 DVD 和单轨 CD 的 ISO 还原。实机启动、换盘和各模拟器的兼容性需另行验证。公开仓库提供源码、构建步骤和合成测试脚本。游戏镜像、第三方工具二进制、宣传视频和本机版 EXE 均需自行准备或本地生成。
