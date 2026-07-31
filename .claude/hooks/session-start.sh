#!/usr/bin/env bash
#
# SessionStart hook: make a cloud container ready to build and test.
#
# Runs synchronously, so the session cannot start before the SDK is present.

set -euo pipefail

# Cloud containers only. On a Windows dev box there is no apt and the SDK is
# already installed, so there is nothing to do.
if [ "${CLAUDE_CODE_REMOTE:-}" != "true" ]; then
  exit 0
fi

# Invoked through bash rather than executed directly, so the hook works even if
# the file's executable bit did not survive however it reached this machine.
exec bash "${CLAUDE_PROJECT_DIR:-.}/scripts/setup-cloud.sh"
