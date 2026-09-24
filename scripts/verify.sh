#!/usr/bin/env bash
# Routine repository verification for rusty-crawler.
#
# It verifies the installed Engine pair identity, installs the UI dependencies
# and runs the DOM companion tests, builds every product project in Release, runs
# every suite, and stages the CoreCLR product. Add each landed project to
# `product_projects` and each suite to `test_projects`; the explicit lists are
# deliberate, because a discovery-based loop silently stops covering a project
# whose csproj moved or was renamed, and
# tests/PartyRpg.Architecture.Tests fails when a checked-in project is missing
# from either list.
#
# NativeAOT is a separate fidelity/release target and stays opt-in through
# --aot; the ordinary development loop does not need it.
set -euo pipefail

aot=false
for argument in "$@"; do
  case "$argument" in
    --aot) aot=true ;;
    -h|--help)
      echo "usage: scripts/verify.sh [--aot]"
      echo "  no arguments  pair identity, UI dependencies, product build, suites and CoreCLR staging"
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

if [[ ! -x .runtime/verify-pair.sh ]]; then
  echo "Rusty Engine C# pair is not installed. Run ./scripts/install-engine-pair.sh first." >&2
  exit 1
fi

# verify-pair.sh requires a root holding nothing but the manifest's payload, but
# .runtime also holds runtime persistence and any superseded packs. Verify a
# hardlinked view of exactly the payload so live state cannot fail artifact
# accounting.
pair_view=$(mktemp -d "$repo_root/.runtime/.pair-view.XXXXXX")
trap 'rm -rf -- "$pair_view"' EXIT
jq -r '.payload[].path' .runtime/pair-manifest.json | while IFS= read -r payload_path; do
  mkdir -p -- "$pair_view/$(dirname "$payload_path")"
  ln -- "$repo_root/.runtime/$payload_path" "$pair_view/$payload_path"
done
cp -- .runtime/pair-manifest.json "$pair_view/pair-manifest.json"
"$pair_view/verify-pair.sh" --directory "$pair_view"
pair_version=$(sed -n 's|.*<RustyEnginePackageVersion>\([^<]*\)</RustyEnginePackageVersion>.*|\1|p' Directory.Build.props)
pair_source_revision=$(sed -n 's|.*<RustyEnginePairSourceRevision>\([^<]*\)</RustyEnginePairSourceRevision>.*|\1|p' Directory.Build.props)
[[ -n "$pair_version" && -n "$pair_source_revision" ]] || {
  echo "Directory.Build.props must declare the Rusty Engine package version and pair source revision." >&2
  exit 1
}
jq -e --arg package_version "$pair_version" --arg source_revision "$pair_source_revision" \
  '.package.id == "Rusty.Engine" and .package.version == $package_version and .sourceRevision == $source_revision' \
  .runtime/pair-manifest.json >/dev/null || {
  echo "Installed Engine pair does not match Directory.Build.props. Run ./scripts/install-engine-pair.sh." >&2
  exit 1
}

# The product UI is a Node-built DOM companion: its dependencies, and the DOM
# contract tests that exercise the companion without a browser.
npm ci
node --test tests/PartyRpg.Ui.Tests/*.test.mjs

# Every project this repository builds and runs, and every suite it executes.
# The lists are explicit on purpose: a discovery-based loop silently stops
# covering a project whose csproj moved or was renamed.
product_projects=(
  src/PartyRpg.Kit/PartyRpg.Kit.csproj
  src/PartyRpg.Rulesets.MightAndMagic7/PartyRpg.Rulesets.MightAndMagic7.csproj
  src/PartyRpg.Host/PartyRpg.Host.csproj
  src/MightAndMagic7.Import/MightAndMagic7.Import.csproj
  src/MightAndMagic7.Import.Tool/MightAndMagic7.Import.Tool.csproj
)
test_projects=(
  tests/PartyRpg.Architecture.Tests/PartyRpg.Architecture.Tests.csproj
  tests/PartyRpg.Kit.Tests/PartyRpg.Kit.Tests.csproj
  tests/PartyRpg.Host.Tests/PartyRpg.Host.Tests.csproj
  tests/MightAndMagic7.Import.Tests/MightAndMagic7.Import.Tests.csproj
)
host_project="src/PartyRpg.Host/PartyRpg.Host.csproj"

for project in "${product_projects[@]}"; do
  dotnet restore "$project"
  dotnet build "$project" --configuration Release --no-restore
done
for suite in "${test_projects[@]}"; do
  dotnet test "$suite"
done

# Compiling projects is not the same as exercising them, and a green build says
# nothing about a suite nobody ran, so every checked suite above is executed.

dotnet msbuild "$host_project" -t:StageRustyEngineCoreClrProduct -p:Configuration=Release

# The importer's readers are checked against the recorded inventory of the operator's own game data.
# That data is not part of the repository and is not required to build, so the check reports plainly
# when it is absent instead of pretending the readers were exercised.
operator_install="${CRAWLER_MM7_INSTALL:-/home/research/old-games/game-mm7}"
if [[ -d "$operator_install" ]]; then
  dotnet src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll verify --install "$operator_install"
else
  echo "Operator game data is not present at $operator_install; the extraction inventory check was skipped."
fi

if [[ "$aot" == true ]]; then
  dotnet msbuild "$host_project" -t:VerifyRustyEngineAot -p:Configuration=Release
  echo "Verified Engine pair ${pair_version} (${pair_source_revision}): CoreCLR and NativeAOT."
else
  echo "Verified Engine pair ${pair_version} (${pair_source_revision}): CoreCLR. Use --aot for the NativeAOT fidelity publish."
fi
