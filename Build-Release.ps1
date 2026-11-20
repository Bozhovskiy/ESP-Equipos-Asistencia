param([string]$OutputPath=(Join-Path (Split-Path $PSScriptRoot -Parent) 'ESP-Equipos-Asistencia.zip'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$rootFiles=@('Abrir-ESP.cmd','Abrir-ESP.cs','Abrir-ESP.exe','Caja-Compartida.cs','Compiler.ps1','Entidades-live.cs','ESP-LiveKd.cs','ESP-LiveKd.exe','Menu-ESP.cs','Objetivos.cs','Proyeccion.cs','Recompilar.ps1','Probar-LiveKd.ps1','Build-Release.ps1','configuracion-esp.json','perfil-objetivos.json','local-prerequisites.example.json','procedencia-livekd.json','README.md','SOURCE-GUIDE.md','NATIVE-SOURCES.md','SOURCE-NOTICE.txt','VALIDATION.md','validation-results.json','FORUM-POST.txt','UC-UPLOAD.txt','release-files.sha256')
$files=@($rootFiles | ForEach-Object {
    $path=Join-Path $PSScriptRoot $_
    if(-not(Test-Path -LiteralPath $path -PathType Leaf)){throw ('Required release file missing: '+$_)}
    Get-Item -LiteralPath $path
})
foreach($folder in @('runtime','pruebas','ajuste-nativo','third-party')){$files+=Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $folder) -Recurse -File}
$root=(Resolve-Path -LiteralPath $PSScriptRoot).Path
$indexLines=@($files | Where-Object Name -ne 'release-files.sha256' | Sort-Object FullName | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName).Hash,$_.FullName.Substring($root.Length+1).Replace('\','/')
})
[IO.File]::WriteAllLines((Join-Path $root 'release-files.sha256'),$indexLines,(New-Object Text.UTF8Encoding($false)))
$fullOutput=[IO.Path]::GetFullPath($OutputPath)
if([IO.Path]::GetExtension($fullOutput) -ne '.zip'){throw 'OutputPath must end in .zip.'}
$temporary=$fullOutput+'.tmp'
if(Test-Path -LiteralPath $temporary){throw 'A temporary archive already exists; resolve it before rebuilding.'}
$stream=[IO.File]::Open($temporary,[IO.FileMode]::CreateNew)
$archive=New-Object IO.Compression.ZipArchive($stream,[IO.Compression.ZipArchiveMode]::Create,$false)
try {
    foreach($file in $files | Sort-Object FullName){
        $relative=$file.FullName.Substring($root.Length+1).Replace('\','/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,$relative,[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally {$archive.Dispose();$stream.Dispose()}
if((Get-Item -LiteralPath $temporary).Length -gt 30000000){throw 'Archive exceeds the conservative 30 MB upload limit; the temporary file was preserved.'}
if(Test-Path -LiteralPath $fullOutput){[IO.File]::Replace($temporary,$fullOutput,$null)}else{[IO.File]::Move($temporary,$fullOutput)}
Get-Item -LiteralPath $fullOutput | Select-Object Name,Length
