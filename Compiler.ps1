function Resolve-EspCompiler([string]$RequestedPath) {
    if($RequestedPath){
        if(-not(Test-Path -LiteralPath $RequestedPath -PathType Leaf)){throw 'CompilerPath does not identify a compiler.'}
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }
    $installed=Get-Command csc.exe -ErrorAction SilentlyContinue
    if($installed){return $installed.Source}
    $vswhere=Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if(Test-Path -LiteralPath $vswhere){
        $found=@(& $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\Roslyn\csc.exe')
        foreach($path in $found){if(Test-Path -LiteralPath $path){return $path}}
    }
    throw 'Install Visual Studio Build Tools with the .NET desktop tools, or pass -CompilerPath with a modern Roslyn csc.exe.'
}
