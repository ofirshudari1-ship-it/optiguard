#requires -version 5
<#
  sign.ps1 — documented template for code-signing the OptiGuard installer.

  NOT run automatically by build\build.ps1, and Claude/an agent must never run this
  script or touch certificates on your behalf — signing is a manual step only
  you can do, since it requires a hardware-backed private key you control.

  Context (see _AUDIT/STANDARDS.md section 11.2 for sources):
  - EV certificates no longer grant instant SmartScreen reputation (changed
    2024, reconfirmed 2026) — both EV and OV must build reputation from real
    downloads/runs over time. There's no reason to pay the EV premium on the
    assumption it "skips" the SmartScreen warning immediately; it doesn't.
  - As of March 1 2026, public code-signing certificates are capped at 458
    days of validity (CA/Browser Forum rule) — plan renewal well under two
    years, not on the old multi-year assumption.
  - The private key MUST live in hardware (a USB token or HSM) — required
    since June 2023; signtool.exe will prompt for the token's PIN/password
    when it runs.
  - Always timestamp the signature (RFC 3161) so it stays valid after the
    certificate itself expires.

  Usage (after you have a certificate + USB token from a CA):
    .\build\sign.ps1 -Thumbprint "<your cert thumbprint>"

  Find your certificate's thumbprint with:
    Get-ChildItem Cert:\CurrentUser\My | Format-Table Subject, Thumbprint
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Thumbprint,

    # Not hardcoded to a version - resolves the current installer name from
    # version.json (the single source of truth, see build\build.ps1) so this
    # script doesn't go stale the next time the version bumps.
    [string]$FilePath = $(
        $v = (Get-Content "$PSScriptRoot\..\version.json" -Raw | ConvertFrom-Json).version
        "$PSScriptRoot\..\OptiGuard-Setup-$v.exe"
    ),

    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$signtool = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1

if (-not $signtool) {
    throw "signtool.exe not found. Install the Windows SDK (includes signtool) first."
}

if (-not (Test-Path $FilePath)) {
    throw "File not found: $FilePath. Run .\build\build.ps1 first to produce it."
}

& $signtool.FullName sign /sha1 $Thumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 /v $FilePath

Write-Host "Verify the signature with:" -ForegroundColor Cyan
Write-Host "  & `"$($signtool.FullName)`" verify /pa /v `"$FilePath`""
