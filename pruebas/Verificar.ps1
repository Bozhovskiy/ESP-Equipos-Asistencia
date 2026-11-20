param(
    [string]$CompilerPath='',
    [string]$OutputDirectory=(Join-Path ([IO.Path]::GetTempPath()) ('ESP-Mejorado-tests-'+[Guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference='Stop'
. (Join-Path (Split-Path $PSScriptRoot -Parent) 'Compiler.ps1')
$CompilerPath=Resolve-EspCompiler $CompilerPath
$package=Split-Path $PSScriptRoot -Parent
$sources=@('Entidades-live.cs','ESP-LiveKd.cs','Proyeccion.cs','Menu-ESP.cs','Caja-Compartida.cs','Objetivos.cs') | ForEach-Object {Join-Path $package $_}
$tests=@('SceneDiscoveryTests','TeamFilterTests','TargetValidationTests','TargetReaderTests','CameraDiscoveryTests','FrameFreshnessTests','RefreshCadenceTests','ReaderRetryTests','ProjectionTests','FovAxisTests','WorldPositionTests','RadarTests','EdgeIndicatorTests','RadarLayoutTests','TimerResolutionTests')
$results=@()
foreach($name in $tests){
    $folder=Join-Path $OutputDirectory $name
    New-Item -ItemType Directory -Path $folder -Force | Out-Null
    $exe=Join-Path $folder ($name+'.exe')
    & $CompilerPath /nologo /target:exe /platform:x64 /langversion:latest ('/main:'+$name) ('/out:'+$exe) /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Web.Extensions.dll @sources (Join-Path $PSScriptRoot ($name+'.cs'))
    if($LASTEXITCODE -ne 0){throw ('No compila '+$name)}
    $output=@(& $exe)
    $code=$LASTEXITCODE
    $output | Write-Output
    $results+=[pscustomobject]@{name=$name;exitCode=$code;output=$output}
}
$results | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $OutputDirectory 'resultados.json') -Encoding UTF8
if(@($results | Where-Object exitCode -ne 0).Count){throw 'Hay suites fallidas; consulta resultados.json'}
Write-Output ('PASS: '+$tests.Count+' suites. Sin lectura del juego ni movimiento real.')
