#!/usr/bin/env bash
# Resolve the installed Rusty Engine toolchain, prepare operator content, or
# launch the ordinary product.  This is a thin developer wrapper: `rusty`
# remains the host owner and `mm7import` remains the offline importer owner.
set -euo pipefail

script_directory=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
repository_root=$(cd "$script_directory/.." && pwd)
host_project="$repository_root/src/PartyRpg.Host/PartyRpg.Host.csproj"
importer_project="$repository_root/src/MightAndMagic7.Import.Tool/MightAndMagic7.Import.Tool.csproj"
importer_dll="$repository_root/src/MightAndMagic7.Import.Tool/bin/Release/net10.0/mm7import.dll"

usage() {
    cat <<'EOF'
usage:
  scripts/developer-launch.sh doctor
  scripts/developer-launch.sh install
  scripts/developer-launch.sh host [rusty dev options]
  scripts/developer-launch.sh prepare-content --install <game-directory> [--output <content-root>]

doctor
  Resolve the installed `rusty` and .NET tools, then print the pinned pair
  status for this checkout.

install
  Install the checkout's pinned Engine pair through the resolved `rusty`
  command. An already installed pair is a no-op.

host
  Run the ordinary PartyRpg.Host product through the installed, pinned Rusty
  Engine pair. Arguments after `host` are passed to `rusty dev`.

prepare-content
  Build the current offline importer, verify and decode the operator install,
  then write deterministic packs into the content root. The writer is the
  source of generated values; this command does not patch packs by hand.

Environment
  RUSTY_BIN may name an explicitly selected `rusty` executable.
  CRAWLER_MM7_INSTALL may provide --install when it is not passed.
  CRAWLER_IMPORTED_CONTENT_ROOT may provide --output.
EOF
}

fail() {
    echo "developer-launch: $*" >&2
    exit 2
}

resolve_rusty() {
    if [[ -n "${RUSTY_BIN:-}" ]]; then
        [[ -x "$RUSTY_BIN" ]] || fail "RUSTY_BIN is not executable: $RUSTY_BIN"
        printf '%s\n' "$RUSTY_BIN"
        return
    fi

    local discovered
    discovered=$(command -v rusty 2>/dev/null || true)
    if [[ -n "$discovered" && -x "$discovered" ]]; then
        printf '%s\n' "$discovered"
        return
    fi

    if [[ -n "${HOME:-}" && -x "$HOME/.local/bin/rusty" ]]; then
        printf '%s\n' "$HOME/.local/bin/rusty"
        return
    fi

    fail "the Rusty Engine launcher was not found on PATH; install it with the Engine bootstrap, then run \`rusty status\`."
}

resolve_dotnet() {
    local discovered
    discovered=$(command -v dotnet 2>/dev/null || true)
    if [[ -n "$discovered" && -x "$discovered" ]]; then
        canonical_dotnet "$discovered"
        return
    fi

    if [[ -n "${DOTNET_ROOT:-}" && -x "$DOTNET_ROOT/dotnet" ]]; then
        canonical_dotnet "$DOTNET_ROOT/dotnet"
        return
    fi

    if [[ -n "${HOME:-}" && -x "$HOME/.dotnet/dotnet" ]]; then
        canonical_dotnet "$HOME/.dotnet/dotnet"
        return
    fi

    fail "the .NET SDK was not found on PATH; add the installed SDK directory to PATH (or set DOTNET_ROOT), then rerun this command."
}

canonical_dotnet() {
    local candidate=$1
    local canonical=""

    # A PATH entry may be a symlink such as /usr/bin/dotnet. DOTNET_ROOT must
    # name the real SDK directory, or Rusty's pinned supervisor can resolve the
    # wrong runtime despite the executable itself being valid.
    if command -v readlink >/dev/null 2>&1; then
        canonical=$(readlink -f "$candidate" 2>/dev/null || true)
    fi
    if [[ -z "$canonical" ]] && command -v realpath >/dev/null 2>&1; then
        canonical=$(realpath "$candidate" 2>/dev/null || true)
    fi
    if [[ -n "$canonical" && -x "$canonical" ]]; then
        printf '%s\n' "$canonical"
    else
        printf '%s\n' "$candidate"
    fi
}

prepare_environment() {
    rusty_command=$(resolve_rusty)
    dotnet_command=$(resolve_dotnet)
    rusty_directory=$(dirname "$rusty_command")
    dotnet_root=$(dirname "$dotnet_command")

    # `rusty dev` sets DOTNET_ROOT from dotnet on PATH when it is unset. Make
    # the same supported discovery explicit for service managers and brokers,
    # whose PATH often omits the user SDK directory.
    export DOTNET_ROOT="$dotnet_root"
    export PATH="$rusty_directory:$dotnet_root:$PATH"
}

print_environment() {
    echo "developer-launch: repository $repository_root"
    echo "developer-launch: rusty $rusty_command"
    echo "developer-launch: dotnet $dotnet_command"
    echo "developer-launch: DOTNET_ROOT $DOTNET_ROOT"
}

doctor() {
    [[ $# -eq 0 ]] || fail "doctor takes no arguments"
    prepare_environment
    print_environment
    "$rusty_command" status --project "$host_project"
}

install_pair() {
    [[ $# -eq 0 ]] || fail "install takes no arguments"
    prepare_environment
    print_environment
    exec "$rusty_command" install --project "$host_project"
}

host() {
    prepare_environment
    print_environment
    exec "$rusty_command" dev --project "$host_project" "$@"
}

prepare_content() {
    local install_root="${CRAWLER_MM7_INSTALL:-}"
    local output_root="${CRAWLER_IMPORTED_CONTENT_ROOT:-$repository_root/content/partyrpg/imports}"

    while (($#)); do
        case "$1" in
            --install)
                (($# >= 2)) || fail "--install needs a directory"
                install_root=$2
                shift 2
                ;;
            --output)
                (($# >= 2)) || fail "--output needs a directory"
                output_root=$2
                shift 2
                ;;
            -h|--help)
                usage
                return 0
                ;;
            *)
                fail "unknown prepare-content option: $1"
                ;;
        esac
    done

    [[ -n "$install_root" ]] || fail "an operator installation is required; pass --install <directory> or set CRAWLER_MM7_INSTALL. No machine-specific default is selected."
    [[ -d "$install_root" ]] || fail "operator installation does not exist: $install_root"
    [[ -n "$output_root" ]] || fail "content output directory cannot be empty"

    prepare_environment
    print_environment
    echo "developer-launch: checking the pinned Engine pair"
    "$rusty_command" status --project "$host_project"
    "$rusty_command" install --project "$host_project"

    echo "developer-launch: building the current offline importer"
    "$dotnet_command" build "$importer_project" --configuration Release
    [[ -f "$importer_dll" ]] || fail "importer build produced no executable: $importer_dll"

    mkdir -p "$output_root"
    echo "developer-launch: verifying operator data"
    "$dotnet_command" "$importer_dll" verify --install "$install_root"
    echo "developer-launch: decoding operator maps"
    "$dotnet_command" "$importer_dll" maps --install "$install_root"
    echo "developer-launch: writing current packs with determinism checking"
    "$dotnet_command" "$importer_dll" write \
        --install "$install_root" \
        --output "$output_root" \
        --check-determinism
    echo "developer-launch: content preparation completed at $output_root"
}

cd "$repository_root"
command_name="${1:-}"
if (($#)); then shift; fi

case "$command_name" in
    doctor) doctor "$@" ;;
    install) install_pair "$@" ;;
    host) host "$@" ;;
    prepare-content) prepare_content "$@" ;;
    -h|--help|help) usage ;;
    '') usage >&2; exit 2 ;;
    *) fail "unknown command: $command_name (use --help)" ;;
esac
