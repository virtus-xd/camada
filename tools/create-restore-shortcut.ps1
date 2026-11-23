<#
.SYNOPSIS
  Masaüstüne "Görev çubuğunu geri getir" kısayolu oluşturur.
.DESCRIPTION
  Kısayol CamAda.exe'yi --restore-taskbar ile çalıştırır: çalışan adayı kapatır
  ve Windows görev çubuğunu eski hâline getirir. Kod bilgisi gerektirmez.
.EXAMPLE
  .\tools\create-restore-shortcut.ps1 -ExePath .\src\CamAda\bin\Debug\net10.0-windows10.0.22621.0\CamAda.exe
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$ExePath
)

$exe = (Resolve-Path -LiteralPath $ExePath).Path
$desktop = [Environment]::GetFolderPath('Desktop')
$shortcutPath = Join-Path $desktop 'Görev çubuğunu geri getir.lnk'

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.Arguments = '--restore-taskbar'
$shortcut.WorkingDirectory = Split-Path -Parent $exe
$shortcut.Description = 'CamAda: adayı kapatır ve Windows görev çubuğunu geri getirir'
$shortcut.Save()

Write-Output "Kısayol oluşturuldu: $shortcutPath"
