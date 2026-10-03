#!/usr/bin/env bash
# Routine repository verification for rusty-crawler.
#
# It installs the pinned Engine pair, installs the UI dependencies and runs the
# DOM companion suite over the compiled companion, builds every project in
# Release, writes the operator's packs and checks them when the install is
# present, runs every suite, and stages the CoreCLR product.
#
# Every step runs even when an earlier one fails, so one broken step cannot hide
# the results of the rest; the script ends with a summary and exits non-zero if
# any step failed. A step that cannot run for want of operator data says it was
# skipped rather than passing.
#
# Add each project to `product_projects`, each suite to `test_projects`, and each
# library the suites share to `test_support_projects`; the explicit lists are
# deliberate, because a discovery-based loop silently stops covering a project
# whose csproj moved or was renamed, and
# tests/PartyRpg.Architecture.Tests fails when a checked-in project is missing
# from its list.
#
# NativeAOT is a separate fidelity/release target and stays opt-in through
# --aot; the ordinary development loop does not need it.
set -uo pipefail

# Service managers and Den brokers may start with a PATH that omits the
# installer's user directories. Discover the normal user-local locations
# without naming a machine or assessor account; the Rusty CLI itself uses the
# same dotnet-on-PATH contract for the pinned pair.
if ! command -v rusty >/dev/null 2>&1 && [[ -n "${HOME:-}" && -x "$HOME/.local/bin/rusty" ]]; then
  export PATH="$HOME/.local/bin:$PATH"
fi
if [[ -n "${DOTNET_ROOT:-}" && -x "$DOTNET_ROOT/dotnet" ]]; then
  export PATH="$DOTNET_ROOT:$PATH"
elif ! command -v dotnet >/dev/null 2>&1 && [[ -n "${HOME:-}" && -x "$HOME/.dotnet/dotnet" ]]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$DOTNET_ROOT:$PATH"
fi

aot=false
for argument in "$@"; do
  case "$argument" in
    --aot) aot=true ;;
    -h|--help)
      echo "usage: scripts/verify.sh [--aot]"
      echo "  no arguments  pinned pair install, UI suite, builds, operator-data checks, suites and CoreCLR staging"
      echo "  --aot         also run the NativeAOT fidelity publish"
      exit 0
      ;;
    *)
      echo "Unknown argument: $argument (supported: --aot)" >&2
      exit 2
      ;;
  esac
done

repo_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
cd "$repo_root"

passed=()
failed=()
skipped=()

# Runs one named step, records its outcome, and never stops the script.
step() {
  local name=$1
  shift
  echo
  echo "==> $name"
  if "$@"; then
    passed+=("$name")
  else
    failed+=("$name (exit $?)")
  fi
}

skip() {
  echo
  echo "==> $1: skipped ($2)"
  skipped+=("$1: $2")
}

# Every project this repository builds and runs, and every suite it executes.
# The lists are explicit on purpose: a discovery-based loop silently stops
# covering a project whose csproj moved or was renamed.
product_projects=(
  src/PartyRpg.Kit/PartyRpg.Kit.csproj
  src/PartyRpg.Rulesets.MightAndMagic7/PartyRpg.Rulesets.MightAndMagic7.csproj
  src/PartyRpg.Host/PartyRpg.Host.csproj
  src/MightAndMagic7.Import/MightAndMagic7.Import.csproj
  src/MightAndMagic7.Import.Tool/MightAndMagic7.Import.Tool.csproj
  tools/portable-assets-example/PortableExample.csproj
)
# The suites' shared support is a library, not a suite: it is built so a break in it
# is reported as its own, and every suite that uses it references it.
test_support_projects=(
  tests/PartyRpg.Testing/PartyRpg.Testing.csproj
)
test_projects=(
  tests/PartyRpg.Architecture.Tests/PartyRpg.Architecture.Tests.csproj
  tests/PartyRpg.Kit.Tests/PartyRpg.Kit.Tests.csproj
  tests/PartyRpg.Host.Tests/PartyRpg.Host.Tests.csproj
  tests/PartyRpg.Rulesets.MightAndMagic7.Tests/PartyRpg.Rulesets.MightAndMagic7.Tests.csproj
  tests/MightAndMagic7.Import.Tests/MightAndMagic7.Import.Tests.csproj
)
host_project="src/PartyRpg.Host/PartyRpg.Host.csproj"
importer="src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll"

# Installs the pinned pair when it is missing (a no-op offline once installed).
step "Engine pair install" rusty install
pair_version=$(sed -n 's|.*<RustyEnginePackageVersion>\([^<]*\)</RustyEnginePackageVersion>.*|\1|p' Directory.Build.props)

# The product UI is a Node-built DOM companion: its dependencies, then the DOM
# contract suite over the companion compiled the way the host build compiles it.
step "UI dependencies" npm ci
step "UI suite" npm run test:ui

for project in "${product_projects[@]}" "${test_support_projects[@]}"; do
  step "build $project" dotnet build "$project" --configuration Release
done

# The operator's own game data is not part of the repository and is not required
# to build. When it is present, the importer's readers are checked against the
# recorded inventory, every map is decoded, and the packs are written twice into
# a root of this run's own and compared byte for byte; the suites that check
# this game's policy against the shipped tables then read that root and nothing
# else. When it is absent those checks and cases report themselves skipped.
operator_install="${CRAWLER_MM7_INSTALL:-/home/research/old-games/game-mm7}"
imported_root=""
if [[ -d "$operator_install" ]]; then
  step "importer inventory" dotnet "$importer" verify --install "$operator_install"
  step "importer map decoding" dotnet "$importer" maps --install "$operator_install"
  imported_root=$(mktemp -d)
  trap 'rm -rf "$imported_root"' EXIT
  step "importer pack write (determinism)" \
    dotnet "$importer" write --install "$operator_install" --output "$imported_root/partyrpg/imports" --check-determinism
else
  skip "operator data checks" "no install at $operator_install"
fi

for suite in "${test_projects[@]}"; do
  if [[ -n "$imported_root" ]]; then
    step "suite $suite" env CRAWLER_IMPORTED_CONTENT="$imported_root" dotnet test "$suite" --configuration Release
  else
    step "suite $suite" dotnet test "$suite" --configuration Release
  fi
done

step "CoreCLR staging" dotnet msbuild "$host_project" -t:StageRustyEngineCoreClrProduct -p:Configuration=Release

if [[ "$aot" == true ]]; then
  step "NativeAOT publish" dotnet msbuild "$host_project" -t:VerifyRustyEngineAot -p:Configuration=Release
fi

echo
echo "==> Summary for Engine pair ${pair_version}"
for name in "${passed[@]}"; do echo "  passed   $name"; done
for name in "${skipped[@]}"; do echo "  skipped  $name"; done
for name in "${failed[@]}"; do echo "  FAILED   $name"; done
if [[ "$aot" != true ]]; then echo "  (NativeAOT not run; use --aot for the fidelity publish)"; fi

if (( ${#failed[@]} > 0 )); then
  echo "${#failed[@]} step(s) failed."
  exit 1
fi
echo "All steps passed."
