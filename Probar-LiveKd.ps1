param([ValidateSet('Verify','ESP')][string]$Mode='Verify')
$ErrorActionPreference='Stop'
$resultPath=Join-Path $PSScriptRoot 'launcher-status.json'
$report=[ordered]@{timestampUtc=[DateTime]::UtcNow.ToString('o');status='checking';mode=$Mode;gameMemoryWritten=$false}
$launchMutex=$null;$lockHeld=$false
try {
    $manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'procedencia-livekd.json') -Raw | ConvertFrom-Json
    $probe=Join-Path $PSScriptRoot 'ESP-LiveKd.exe'
    if((Get-FileHash -LiteralPath $probe).Hash -ne $manifest.espSha256){throw 'Overlay hash mismatch. Rebuild with Recompilar.ps1 after changing sources.'}
    foreach($file in $manifest.runtimeFiles){
        if((Get-FileHash -LiteralPath (Join-Path (Join-Path $PSScriptRoot 'runtime') $file.name)).Hash -ne $file.sha256){throw ('Dependency hash mismatch: '+$file.name)}
    }
    # Verify only loads the library and resolves exports; it never initializes
    # the acquisition device, starts LiveKD, or sends mouse input.
    if($Mode -eq 'Verify'){
        $check=Start-Process -FilePath $probe -ArgumentList '--verify' -WorkingDirectory $PSScriptRoot -WindowStyle Hidden -PassThru -Wait
        if($check.ExitCode -ne 0){throw 'Native-export verification failed. Check the local x64 Visual C++ runtime.'}
        $report.status='verified';$report.acquisitionStarted=$false
    } else {
        $launchMutex=[Threading.Mutex]::new($false,'Local\ARC.ESP.LiveKd')
        try{$lockHeld=$launchMutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$lockHeld=$true}
        if(-not $lockHeld){throw 'Another launcher is active.'}
        $configPath=Join-Path $PSScriptRoot 'local-prerequisites.json'
        if(-not(Test-Path -LiteralPath $configPath)){$configPath=Join-Path $env:LOCALAPPDATA 'ESP-Assist\local-prerequisites.json'}
        if(-not(Test-Path -LiteralPath $configPath)){throw 'Configure local-prerequisites.json using the example and README.md. LiveKD and Windows symbols are not distributed in this archive.'}
        $local=Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
        $exe=[IO.Path]::GetFullPath($local.LiveKdPath)
        $symbolDirectory=[IO.Path]::GetFullPath($local.KernelSymbolDirectory)
        if((Get-FileHash -LiteralPath $exe).Hash -ne $manifest.prerequisites.livekdSha256){throw 'This release requires the pinned local LiveKD 5.65 executable.'}
        if(-not(Test-Path -LiteralPath $symbolDirectory -PathType Container) -or -not @(Get-ChildItem -LiteralPath $symbolDirectory -Filter '*.pdb' -File).Count){throw 'Provide a local folder containing the kernel PDB matching your Windows build.'}
        if($symbolDirectory.Contains(';') -or $symbolDirectory.Contains('*')){throw 'KernelSymbolDirectory must be a local directory, not symbol-server syntax.'}
        $debuggingDirectory=if($local.DebuggingToolsDirectory){[IO.Path]::GetFullPath($local.DebuggingToolsDirectory)}else{Split-Path $exe -Parent}
        foreach($name in @('dbghelp.dll','symsrv.dll')){if(-not(Test-Path -LiteralPath (Join-Path $debuggingDirectory $name))){throw ('Missing local x64 Debugging Tools component: '+$name)}}
        if(Get-Process -Name '*LiveKd*','DumpIt','ESP','Medir-ESP' -ErrorAction SilentlyContinue){throw 'Close the existing reader before launching another one.'}
        if(Test-Path -LiteralPath (Join-Path $env:SystemRoot 'livekd.dmp')){throw 'livekd.dmp already exists; it will not be modified.'}
        if(Get-Service -Name 'LiveKd','LiveKdd' -ErrorAction SilentlyContinue){throw 'A LiveKD service already exists; it will not be modified.'}
        $os=Get-CimInstance Win32_OperatingSystem
        if($os.FreePhysicalMemory -lt 524288){throw 'At least 512 MB of free physical memory is required.'}
        $admin=[Security.Principal.WindowsPrincipal]::new([Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
        if(-not $admin){throw 'Launch Abrir-ESP.exe and accept its administrator prompt.'}
        $previousPath=$env:PATH;$previousSymbols=$env:_NT_SYMBOL_PATH;$previousAlternate=$env:_NT_ALT_SYMBOL_PATH
        try {
            # Use only the explicitly configured local helpers and local PDB.
            $env:PATH=$debuggingDirectory+';'+$env:PATH
            $env:_NT_SYMBOL_PATH=$symbolDirectory;$env:_NT_ALT_SYMBOL_PATH=''
            $report.status='running';$report | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
            $process=Start-Process -FilePath $exe -WorkingDirectory $PSScriptRoot -ArgumentList ('-accepteula -b -vsym -k "'+$probe+'"') -WindowStyle Hidden -PassThru -Wait -RedirectStandardOutput (Join-Path $PSScriptRoot 'livekd-output.txt') -RedirectStandardError (Join-Path $PSScriptRoot 'livekd-error.txt')
            $report.status='finished';$report.exitCode=$process.ExitCode
        } finally {$env:PATH=$previousPath;$env:_NT_SYMBOL_PATH=$previousSymbols;$env:_NT_ALT_SYMBOL_PATH=$previousAlternate}
    }
} catch {
    $report.status='failed';$report.error=$_.Exception.Message
    if($Mode -eq 'ESP'){Add-Type -AssemblyName System.Windows.Forms;[Windows.Forms.MessageBox]::Show($report.error,'ESP launcher',[Windows.Forms.MessageBoxButtons]::OK,[Windows.Forms.MessageBoxIcon]::Information) | Out-Null}
    throw
} finally {
    if($lockHeld){$launchMutex.ReleaseMutex()}
    if($launchMutex){$launchMutex.Dispose()}
    $report | ConvertTo-Json | Set-Content -LiteralPath $resultPath -Encoding UTF8
}
