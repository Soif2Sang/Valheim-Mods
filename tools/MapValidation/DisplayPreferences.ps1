function Get-MapDisplayPreferences {
    param([string]$RegistryPath = 'Software\IronGate\Valheim')
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($RegistryPath)
    $values = @()
    try {
        if ($key) { foreach ($name in $key.GetValueNames() | Where-Object { $_ -like 'Screenmanager *' }) {
            $values += [pscustomobject]@{ Name=$name; Kind=$key.GetValueKind($name); Value=$key.GetValue($name) }
        } }
    } finally { if ($key) { $key.Dispose() } }
    [pscustomobject]@{ RegistryPath=$RegistryPath; Values=$values }
}

function Restore-MapDisplayPreferences {
    param([Parameter(Mandatory)]$Snapshot)
    $key = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($Snapshot.RegistryPath)
    try {
        foreach ($name in $key.GetValueNames() | Where-Object { $_ -like 'Screenmanager *' }) {
            if ($name -notin @($Snapshot.Values.Name)) { $key.DeleteValue($name, $false) }
        }
        foreach ($value in $Snapshot.Values) {
            $key.SetValue($value.Name, $value.Value, [Microsoft.Win32.RegistryValueKind]$value.Kind)
        }
    } finally { $key.Dispose() }
}
