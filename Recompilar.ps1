param([string]$CompilerPath='')
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'Compiler.ps1')
$compiler=Resolve-EspCompiler $CompilerPath
$sources=@('Entidades-live.cs','ESP-LiveKd.cs','Proyeccion.cs','Menu-ESP.cs','Caja-Compartida.cs','Objetivos.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /deterministic+ /langversion:latest /main:EspProgram ('/out:'+(Join-Path $PSScriptRoot 'ESP-LiveKd.exe')) /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll @sources
if($LASTEXITCODE -ne 0){throw 'ESP compilation failed.'}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /deterministic+ /langversion:latest /main:AbrirEsp ('/out:'+(Join-Path $PSScriptRoot 'Abrir-ESP.exe')) /r:System.Windows.Forms.dll (Join-Path $PSScriptRoot 'Abrir-ESP.cs')
if($LASTEXITCODE -ne 0){throw 'Launcher compilation failed.'}
$path=Join-Path $PSScriptRoot 'procedencia-livekd.json'
$manifest=Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
$manifest.espSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'ESP-LiveKd.exe')).Hash
$manifest.launcherSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'Abrir-ESP.exe')).Hash
$manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding UTF8
Write-Output 'Built the x64 overlay and launcher. Manifest hashes updated; no acquisition started.'
