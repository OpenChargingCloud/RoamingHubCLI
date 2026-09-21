#!/bin/bash
#
# Start the roaming hub with whatever was passed here, e.g.
#
#   ./run.sh --any --verbose
#
# --help lists the switches.

set -e

cd "$(dirname "$0")"

dotnet run --project RoamingHubCLI -- "$@"
