# Thin wrapper for the cross-platform template setup script (Windows / PowerShell).
# Requires the .NET 10 SDK (prerequisite #1). All arguments are forwarded.
#   .\setup.ps1                 # interactive
#   .\setup.ps1 --dry-run       # preview only
#   .\setup.ps1 --yes           # accept defaults
$ErrorActionPreference = 'Stop'
Set-Location -Path $PSScriptRoot
& dotnet run setup.cs -- @args
exit $LASTEXITCODE
