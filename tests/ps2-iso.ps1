param([Parameter(Mandatory=$true)][string]$Fixture)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$harness = Join-Path $PSScriptRoot 'Ps2IsoTest.exe'
& $compiler /nologo /target:exe /platform:x64 /utf8output "/out:$harness" /r:System.dll /r:System.Core.dll /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'Ps2IsoTest.cs')
if ($LASTEXITCODE -ne 0) { throw '测试编译失败' }
$run = Join-Path $PSScriptRoot ('ps2-iso-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
& $harness (Join-Path $root 'PlayStation万能转换姬.exe') $Fixture $run
if ($LASTEXITCODE -ne 0) { throw 'PS2 ISO 最小验证失败' }
