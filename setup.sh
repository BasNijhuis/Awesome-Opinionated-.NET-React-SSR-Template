#!/usr/bin/env bash
# Thin wrapper for the cross-platform template setup script (macOS / Linux).
# Requires the .NET 10 SDK (prerequisite #1). All arguments are forwarded.
#   ./setup.sh                 # interactive
#   ./setup.sh --dry-run       # preview only
#   ./setup.sh --yes           # accept defaults
set -euo pipefail
cd "$(dirname "$0")"
exec dotnet run setup.cs -- "$@"
