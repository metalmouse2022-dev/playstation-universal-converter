$ErrorActionPreference = 'Stop'
$buildRoot = $PSScriptRoot
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $buildRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
$output = Join-Path $buildRoot 'PlayStation万能转换姬.exe'
$arguments = @('/nologo', '/target:winexe', '/platform:x64', '/optimize+', '/utf8output', ('/out:' + $output),
    '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll',
    ('/win32icon:' + (Join-Path $buildRoot 'assets\app.ico')))
foreach ($name in @('tools.zip','tools.manifest','character.png','playstation.png','app.ico')) {
    $arguments += '/resource:' + (Join-Path $buildRoot ('assets\' + $name)) + ',' + $name
}
& $compiler @arguments @sources
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Get-Item -LiteralPath $output | Select-Object Name,Length
