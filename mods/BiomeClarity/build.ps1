param([Parameter(Mandatory=$true)][string]$GamePath, [string]$OutputDirectory = (Join-Path $PSScriptRoot '../../dist'))
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$managed = Join-Path $GamePath 'valheim_Data/Managed'
$core = Join-Path $GamePath 'BepInEx/core'
$output = [IO.Path]::GetFullPath((Join-Path $OutputDirectory 'BiomeClarity.dll'))
New-Item -ItemType Directory -Force (Split-Path $output) | Out-Null
$references = @(
    "$managed/netstandard.dll", "$managed/mscorlib.dll", "$managed/System.dll", "$managed/System.Core.dll",
    "$core/BepInEx.dll", "$core/0Harmony.dll", "$managed/assembly_valheim.dll",
    "$managed/UnityEngine.dll", "$managed/UnityEngine.CoreModule.dll"
)
$argsList = @('/noconfig', '/nologo', '/nostdlib+', '/target:library', '/optimize+', "/out:$output")
foreach ($reference in $references) { $argsList += "/reference:$reference" }
& $compiler @argsList (Join-Path $PSScriptRoot Plugin.cs) (Join-Path $PSScriptRoot Palette.cs)
if ($LASTEXITCODE -ne 0) { throw 'Plugin compilation failed' }
$testExe = Join-Path $OutputDirectory 'PaletteTests.exe'
& $compiler /nologo /target:exe "/out:$testExe" (Join-Path $PSScriptRoot Palette.cs) ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot tests/PaletteTests.cs)))
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw 'Palette tests failed' }
Add-Type -Path (Join-Path $core 'Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($output))
try {
    $forbidden = $assembly.MainModule.GetTypeReferences() | Where-Object {
        $_.Namespace -eq 'AerialMap' -or $_.Namespace -like 'UnityEngine.UI*' -or $_.Namespace -eq 'TMPro' -or
        $_.Name -in @('Camera', 'RenderTexture', 'Light', 'KeyboardShortcut', 'Input', 'AerialAtlas')
    }
    if ($forbidden) { throw 'Color DLL contains aerial, UI or input dependencies' }
    Write-Output 'PASS: colors-only DLL contains no aerial, camera, UI or keyboard dependencies.'
}
finally { $assembly.Dispose() }
Get-FileHash $output -Algorithm SHA256

if (Test-Path (Join-Path $PSScriptRoot '../../upstream/LivingMap/build/LivingMap.dll')) { & (Join-Path $PSScriptRoot 'tests/BridgeChecks.ps1') -GamePath $GamePath -ModPath $output }

