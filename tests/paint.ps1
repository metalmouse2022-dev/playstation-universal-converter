param([string]$RunName = 'paint')
$ErrorActionPreference = 'Stop'
$paintRoot = Split-Path $PSScriptRoot -Parent
$sources = @(Get-ChildItem -LiteralPath (Join-Path $paintRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$argsList = @('/nologo','/target:exe','/platform:x64','/optimize+','/utf8output','/main:PaintRegression',('/out:' + (Join-Path $PSScriptRoot 'PaintRegression.exe')),
    '/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
foreach ($name in @('tools.zip','tools.manifest','character.png','playstation.png','app.ico')) { $argsList += '/resource:' + (Join-Path $paintRoot ('assets\' + $name)) + ',' + $name }
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' @argsList @sources (Join-Path $PSScriptRoot 'PaintRegression.cs')
if ($LASTEXITCODE -ne 0) { throw '绘制测试编译失败' }
& (Join-Path $PSScriptRoot 'PaintRegression.exe') (Join-Path $PSScriptRoot $RunName)
if ($LASTEXITCODE -ne 0) { throw '绘制测试失败' }
