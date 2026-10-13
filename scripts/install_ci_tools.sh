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

if ! command -v htpasswd &> /dev/null; then
  echo "Install htpasswd"
  # apt keeps its lists in a directory of the job, which is what lets it run without root.
  apt_dir="${TOOLS_HOME}/apt"
  mkdir -p "${apt_dir}/lists/partial" "${apt_dir}/cache/archives/partial" "${apt_dir}/root"
  apt_options=(-o "Dir::State::Lists=${apt_dir}/lists" -o "Dir::Cache=${apt_dir}/cache" -o "Dir::Cache::Archives=${apt_dir}/cache/archives" -o Debug::NoLocking=1 -o "APT::Sandbox::User=$(id -un)")
  apt-get "${apt_options[@]}" update > /dev/null
  (cd "${apt_dir}" && apt-get "${apt_options[@]}" download apache2-utils libapr1t64 libaprutil1t64 > /dev/null)
  for package in "${apt_dir}"/*.deb; do dpkg-deb -x "$package" "${apt_dir}/root"; done
  printf '#!/usr/bin/env bash\nLD_LIBRARY_PATH="%s/usr/lib/x86_64-linux-gnu" exec "%s/usr/bin/htpasswd" "$@"\n' "${apt_dir}/root" "${apt_dir}/root" > "${TOOLS_HOME}/bin/htpasswd"
  chmod +x "${TOOLS_HOME}/bin/htpasswd"
fi
