#!/bin/bash
# The cloud onboarding installs Mono, Xvfb and Roslyn outside the checkout.
set -euo pipefail
repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
source /workspace/.cloud-setup/activate.sh
bash /workspace/.cloud-setup/build.sh
output=/workspace/.cloud-setup/build/Release
mono /workspace/.cloud-setup/roslyn/tasks/net472/csc.exe /nologo \
  /r:"$output/DigimonStorySaveEditor.exe" \
  /r:"$CLOUD_SYSROOT/usr/lib/mono/4.7.2-api/System.Windows.Forms.dll" \
  /r:"$CLOUD_SYSROOT/usr/lib/mono/4.7.2-api/System.Drawing.dll" \
  /out:"$output/SaveEditorTests.exe" "$repo_root/tests/SaveEditorTests.cs"
xvfb-run -a mono "$output/SaveEditorTests.exe"
