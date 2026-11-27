<#
.SYNOPSIS
  Windows geliştirici modunu yalnız paket denemesi süresince açar ya da kapatır (yalnız geliştirici).
.DESCRIPTION
  Yönetici olarak çalışmalıdır. Kullanıcıya dönük kurulumda asla kullanılmaz; yalnız tools/test-package.ps1 denemesi
  için, kullanıcı onayıyla açılır ve deneme bitince kapatılır. -Off değeri siler (bu bilgisayarda başlangıçta değer yoktu).
.EXAMPLE
  Start-Process powershell -Verb RunAs -Wait -ArgumentList '-NoProfile','-ExecutionPolicy','Bypass','-File','tools\dev-mode.ps1','-On'
#>
[CmdletBinding()]
param(
    [switch]$On,
    [switch]$Off
)
$ErrorActionPreference = 'Stop'
if ($On -eq $Off) { throw 'Yalnız biri: -On ya da -Off.' }

$principal = New-Object Security.Principal.WindowsPrincipal ([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Yönetici olarak çalıştırın.' }

$key = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock'
if ($On) {
    if (-not (Test-Path $key)) { New-Item -Path $key -Force | Out-Null }
    New-ItemProperty -Path $key -Name AllowDevelopmentWithoutDevLicense -PropertyType DWord -Value 1 -Force | Out-Null
} else {
    Remove-ItemProperty -Path $key -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue
}
$now = (Get-ItemProperty $key -Name AllowDevelopmentWithoutDevLicense -ErrorAction SilentlyContinue).AllowDevelopmentWithoutDevLicense
Write-Output "Geliştirici modu değeri: $(if ($null -eq $now) { 'yok (kapalı)' } else { $now })"
