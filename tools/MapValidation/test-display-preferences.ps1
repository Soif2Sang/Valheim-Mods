$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'DisplayPreferences.ps1')
$path='Software\IronGate\MapDisplayTest-' + [guid]::NewGuid().ToString('N')
$key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($path)
try {
    $key.SetValue('Screenmanager Test',1,[Microsoft.Win32.RegistryValueKind]::DWord)
    $key.SetValue('Unrelated','keep')
    $before=Get-MapDisplayPreferences -RegistryPath $path
    $key.SetValue('Screenmanager Test',3,[Microsoft.Win32.RegistryValueKind]::DWord)
    $key.SetValue('Screenmanager Added',512,[Microsoft.Win32.RegistryValueKind]::DWord)
    Restore-MapDisplayPreferences -Snapshot $before
    if($key.GetValue('Screenmanager Test') -ne 1 -or $null -ne $key.GetValue('Screenmanager Added') -or $key.GetValue('Unrelated') -ne 'keep'){throw 'Display restoration failed'}
    Write-Output 'PASS: display values restored, new display values removed, unrelated preferences preserved.'
} finally {$key.Dispose();[Microsoft.Win32.Registry]::CurrentUser.DeleteSubKey($path)}
