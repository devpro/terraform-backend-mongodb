#!/usr/bin/env bash
set -euo pipefail

# Installs what the tests need without sudo, which a container runner lacks, into the temporary directory of the job.
# A tool already on the path, as on a GitHub-hosted runner, is left as it is.

TOOLS_HOME="${RUNNER_TEMP:-$(mktemp -d)}"
mkdir -p "${TOOLS_HOME}/bin"
export PATH="${TOOLS_HOME}/bin:${PATH}"
[ -n "${GITHUB_PATH:-}" ] && echo "${TOOLS_HOME}/bin" >> "$GITHUB_PATH"

if ! command -v terraform &> /dev/null; then
  echo "Install Terraform"
  curl -fsSL -o "${TOOLS_HOME}/terraform.zip" https://releases.hashicorp.com/terraform/1.16.5/terraform_1.16.5_linux_amd64.zip
  # The image has no unzip, and PowerShell is on both kinds of runner.
  pwsh -NoProfile -Command "Expand-Archive -Force -Path '${TOOLS_HOME}/terraform.zip' -DestinationPath '${TOOLS_HOME}/bin'"
  chmod +x "${TOOLS_HOME}/bin/terraform"
fi
