#!/usr/bin/env bash
#
# Provisions a cloud container for Directive 47.
#
# Kept under version control and invoked by .claude/hooks/session-start.sh so
# provisioning is reviewable and diffable, rather than pasted into a web form.
# Safe to run by hand, and safe to run more than once.

set -euo pipefail

log() { printf '\n==> %s\n' "$1"; }

log "Refreshing apt package lists"
# The base image ships third-party PPAs (deadsnakes, ondrej/php) that fail to
# refresh, so apt-get update exits non-zero even when the Ubuntu archive
# updated cleanly. That is not our failure and must not abort provisioning --
# the SDK check below is what actually decides whether this worked.
if ! sudo apt-get update -qq; then
  echo "    apt-get update reported errors (expected: unrelated third-party" \
       "PPAs). Continuing; the SDK install is verified below."
fi

log "Installing .NET SDK 10"
# apt, not dotnet-install.sh: the network policy blocks
# builds.dotnet.microsoft.com, and apt is faster regardless.
sudo apt-get install -y -qq dotnet-sdk-10.0

log "Verifying"
if ! command -v dotnet >/dev/null; then
  echo "FAILED: dotnet is not on PATH after install" >&2
  exit 1
fi

sdk_version="$(dotnet --version)"
case "$sdk_version" in
  10.*) ;;
  *) echo "FAILED: expected a 10.x SDK, got $sdk_version" >&2; exit 1 ;;
esac

echo "    .NET SDK $sdk_version"

log "Ready"
