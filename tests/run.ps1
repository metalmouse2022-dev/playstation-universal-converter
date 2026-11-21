$ErrorActionPreference = 'Stop'
$testRoot = Split-Path $PSScriptRoot -Parent
$sources = @(Get-ChildItem -LiteralPath (Join-Path $testRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$argsList = @('/nologo','/target:exe','/platform:x64','/optimize+','/utf8output','/main:TestHarness',('/out:' + (Join-Path $testRoot 'tests\TestHarness.exe')),
    '/r:System.dll','/r:System.Core.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
foreach ($name in @('tools.zip','tools.manifest','character.png','playstation.png','app.ico')) { $argsList += '/resource:' + (Join-Path $testRoot ('assets\' + $name)) + ',' + $name }
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' @argsList @sources (Join-Path $PSScriptRoot 'TestHarness.cs')
if ($LASTEXITCODE -ne 0) { throw '测试程序编译失败' }
& (Join-Path $PSScriptRoot 'TestHarness.exe') $testRoot
if ($LASTEXITCODE -ne 0) { throw '测试失败' }
