#!/usr/bin/env bash
set -euo pipefail

print_usage() {
  cat <<'EOF'
Build release artifacts for testprog.

Usage:
  scripts/build-artifacts.sh [options] [RID ...]

Options:
  -h, --help                 Show help
  -o, --output DIR           Output root directory (default: artifacts/output)
  -c, --configuration CFG    Build configuration (default: Release)
  --suffix VALUE             Output suffix (default: current date YYYY-MM-DD)
  --postfix VALUE            Alias for --suffix
  --self-contained           Publish server-cli with bundled runtime (default)
  --framework-dependent      Publish server-cli without bundled runtime

Behavior:
  - Builds/publishes class library DLL outputs for:
      messenger, client, server
  - Creates one final library ZIP package
  - Publishes server-cli executable for each RID
  - Creates one ZIP package per RID
  - Restores runtime assets per RID right before publish

Default RIDs (when no RID is passed):
  linux-x64 win-x64 osx-x64 osx-arm64

Examples:
  scripts/build-artifacts.sh
  scripts/build-artifacts.sh linux-x64
  scripts/build-artifacts.sh --framework-dependent win-x64 osx-arm64
  scripts/build-artifacts.sh --output out --configuration Release linux-x64
  scripts/build-artifacts.sh --suffix v1.0.0
EOF
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

CONFIGURATION="Release"
OUTPUT_DIR="$REPO_ROOT/artifacts/output"
MODE="self-contained"
SUFFIX=""
DEFAULT_RIDS=(linux-x64 win-x64 osx-x64 osx-arm64)

while (( "$#" )); do
  case "$1" in
    -h|--help)
      print_usage
      exit 0
      ;;
    -o|--output)
      OUTPUT_DIR="$2"
      shift 2
      ;;
    --output=*)
      OUTPUT_DIR="${1#*=}"
      shift
      ;;
    -c|--configuration)
      CONFIGURATION="$2"
      shift 2
      ;;
    --configuration=*)
      CONFIGURATION="${1#*=}"
      shift
      ;;
    --suffix|--postfix)
      SUFFIX="$2"
      shift 2
      ;;
    --suffix=*|--postfix=*)
      SUFFIX="${1#*=}"
      shift
      ;;
    --self-contained)
      MODE="self-contained"
      shift
      ;;
    --framework-dependent)
      MODE="framework-dependent"
      shift
      ;;
    --*)
      echo "Unknown option: $1" >&2
      print_usage
      exit 1
      ;;
    *)
      break
      ;;
  esac
done

if (( "$#" > 0 )); then
  RIDS=("$@")
else
  RIDS=("${DEFAULT_RIDS[@]}")
fi

if [[ -z "$SUFFIX" ]]; then
  SUFFIX="$(date +%Y-%m-%d)"
fi

RUN_OUTPUT_DIR="$OUTPUT_DIR/$SUFFIX"
LIB_OUTPUT_DIR="$RUN_OUTPUT_DIR/libs"
CLI_OUTPUT_DIR="$RUN_OUTPUT_DIR/server-cli"
LIB_BUNDLE_PATH="$RUN_OUTPUT_DIR/testprog-libs-$SUFFIX.zip"

if ! command -v zip >/dev/null 2>&1; then
  echo "Missing required command: zip" >&2
  exit 1
fi

echo "==> Cleaning output: $RUN_OUTPUT_DIR"
rm -rf "$RUN_OUTPUT_DIR"
mkdir -p "$LIB_OUTPUT_DIR" "$CLI_OUTPUT_DIR"

echo "==> Restoring solution"
dotnet restore "$REPO_ROOT/testprog.sln"

publish_library() {
  local name="$1"
  local project_path="$2"
  local output_path="$LIB_OUTPUT_DIR/$name"

  echo "==> Publishing library: $name"
  dotnet publish "$REPO_ROOT/$project_path" \
    --configuration "$CONFIGURATION" \
    -p:GenerateDocumentationFile=true \
    --no-restore \
    --output "$output_path"
}

publish_library "messenger" "messenger/messenger.csproj"
publish_library "client" "client/client.csproj"
publish_library "server" "server/server.csproj"

zip_dir_contents() {
  local source_dir="$1"
  local zip_path="$2"

  rm -f "$zip_path"
  (
    cd "$source_dir"
    shopt -s nullglob dotglob
    files=(*)
    if (( ${#files[@]} == 0 )); then
      echo "Nothing to zip in: $source_dir" >&2
      exit 1
    fi
    zip -r "$zip_path" "${files[@]}" >/dev/null
  )
}

echo "==> Packaging libraries: $LIB_BUNDLE_PATH"
zip_dir_contents "$LIB_OUTPUT_DIR" "$LIB_BUNDLE_PATH"

for rid in "${RIDS[@]}"; do
  output_path="$CLI_OUTPUT_DIR/$rid"
  rid_zip_path="$RUN_OUTPUT_DIR/server-cli-$rid-$SUFFIX.zip"

  echo "==> Restoring server-cli for RID: $rid"
  dotnet restore "$REPO_ROOT/server-cli/server-cli.csproj" \
    --runtime "$rid"

  echo "==> Publishing server-cli for RID: $rid ($MODE)"

  if [[ "$MODE" == "self-contained" ]]; then
    dotnet publish "$REPO_ROOT/server-cli/server-cli.csproj" \
      --configuration "$CONFIGURATION" \
      --runtime "$rid" \
      --self-contained true \
      -p:UseAppHost=true \
      -p:PublishSingleFile=true \
      -p:IncludeNativeLibrariesForSelfExtract=true \
      -p:PublishTrimmed=false \
      -p:GenerateDocumentationFile=true \
      --no-restore \
      --output "$output_path"
  else
    dotnet publish "$REPO_ROOT/server-cli/server-cli.csproj" \
      --configuration "$CONFIGURATION" \
      --runtime "$rid" \
      --self-contained false \
      -p:UseAppHost=true \
      -p:GenerateDocumentationFile=true \
      --no-restore \
      --output "$output_path"
  fi

  echo "==> Packaging server-cli RID zip: $rid_zip_path"
  zip_dir_contents "$output_path" "$rid_zip_path"
done

echo
echo "Build completed."
echo "Artifacts:"
echo "  Run suffix: $SUFFIX"
echo "  Output root: $RUN_OUTPUT_DIR"
echo "  Libraries (raw): $LIB_OUTPUT_DIR"
echo "  Libraries (zip): $LIB_BUNDLE_PATH"
echo "  server-cli (raw): $CLI_OUTPUT_DIR/<rid>"
echo "  server-cli (zip): $RUN_OUTPUT_DIR/server-cli-<rid>-$SUFFIX.zip"
