#!/usr/bin/env bash
#
# scripts/release.sh -- cut and publish a GhostSpectator release.
#
# Usage:
#   scripts/release.sh <patch|minor|major>
#   scripts/release.sh --dry-run <patch|minor|major>
#
# Why local: PEAK has no public stripped-game-libs package, so CI can't
# compile the mod (the workflows have no Assembly-CSharp.dll). This script
# owns build + version bump + commit + tag locally. The tag push then
# triggers .forgejo/workflows/publish.yml, which (a) uploads the prebuilt
# zip from releases/ to Thunderstore, and (b) reconciles forgejo / github
# releases with the same zip attached.
#
# Order is deliberate:
#   1. Preflight (branch, clean tree, tag uniqueness, CHANGELOG sanity)
#   2. PatchValidator CLI against local PEAK -- abort if any target missing
#   3. Compute next version, promote CHANGELOG, bump csproj + README compat
#   4. dotnet build -c Release -> produces Thunderstore zip
#   5. Copy zip to releases/ (tracked, gitignored covers artifacts/)
#   6. Commit, tag annotated with the CHANGELOG section, push commit + tag

set -euo pipefail

# Locate repo root regardless of cwd.
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
cd "${REPO_ROOT}"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

die() { echo "error: $*" >&2; exit 1; }
info() { echo "[release] $*"; }

# BSD-sed compatibility (macOS). Use `.bak` suffix then remove.
sed_inplace() {
    local pattern="$1" file="$2"
    sed -i.bak -E "${pattern}" "${file}"
    rm -f "${file}.bak"
}

usage() {
    cat <<EOF
Usage: $0 [--dry-run] <patch|minor|major>

Cuts a GhostSpectator release: validates patches, promotes CHANGELOG,
bumps csproj, updates README compatibility line, builds the Thunderstore
zip, commits, tags, and pushes. The tag push triggers the publish workflow.

  --dry-run    Run all steps EXCEPT commit/tag/push. Leaves the working
               tree dirty for inspection. Revert with 'git restore .' or
               'git stash'.

Examples:
  $0 patch
  $0 --dry-run minor
EOF
}

# ---------------------------------------------------------------------------
# Parse args
# ---------------------------------------------------------------------------

DRY_RUN=0
BUMP=""

while [ $# -gt 0 ]; do
    case "$1" in
        --dry-run) DRY_RUN=1; shift ;;
        -h|--help) usage; exit 0 ;;
        patch|minor|major) BUMP="$1"; shift ;;
        *) die "unknown arg: $1 (try --help)" ;;
    esac
done

[ -n "${BUMP}" ] || { usage; exit 1; }

# ---------------------------------------------------------------------------
# Preflight: branch, working tree, sync with origin
# ---------------------------------------------------------------------------

info "preflight: branch / working tree / sync"

BRANCH="$(git rev-parse --abbrev-ref HEAD)"
[ "${BRANCH}" = "main" ] || die "must be on main (currently on ${BRANCH})"

if ! git diff --quiet || ! git diff --cached --quiet; then
    die "working tree has uncommitted changes; commit or stash first"
fi

git fetch origin --tags --quiet
LOCAL_HEAD="$(git rev-parse HEAD)"
REMOTE_HEAD="$(git rev-parse origin/main)"
[ "${LOCAL_HEAD}" = "${REMOTE_HEAD}" ] \
    || die "local main is not in sync with origin/main (rebase/pull first)"

# ---------------------------------------------------------------------------
# Compute next version
# ---------------------------------------------------------------------------

PREV_TAG="$(git tag -l 'v*.*.*' --sort=-v:refname | head -n1 || true)"
if [ -z "${PREV_TAG}" ]; then
    PREV_VERSION="0.0.0"
else
    PREV_VERSION="${PREV_TAG#v}"
fi

IFS=. read -r MAJOR MINOR PATCH <<< "${PREV_VERSION}"
case "${BUMP}" in
    patch) PATCH=$((PATCH + 1)) ;;
    minor) MINOR=$((MINOR + 1)); PATCH=0 ;;
    major) MAJOR=$((MAJOR + 1)); MINOR=0; PATCH=0 ;;
esac
VERSION="${MAJOR}.${MINOR}.${PATCH}"
TAG="v${VERSION}"
DATE="$(date -u +%Y-%m-%d)"

info "version: ${PREV_VERSION} -> ${VERSION} (${TAG})"

# CHANGELOG sanity
grep -qE '^## \[Unreleased\]' CHANGELOG.md \
    || die "CHANGELOG.md missing '[Unreleased]' section"
if grep -qE "^## \[${VERSION}\]" CHANGELOG.md; then
    die "CHANGELOG already has a [${VERSION}] section"
fi
if git rev-parse -q --verify "refs/tags/${TAG}" >/dev/null 2>&1; then
    die "tag ${TAG} already exists"
fi

# ---------------------------------------------------------------------------
# PatchValidator preflight
# ---------------------------------------------------------------------------

info "running PatchValidator CLI against local PEAK install"

# Source ManagedDir. Prefer env override, fall back to parsing
# Config.Build.user.props for PEAKGameRootDir.
if [ -z "${ManagedDir:-}" ]; then
    if [ ! -f Config.Build.user.props ]; then
        die "Config.Build.user.props not found; copy from .template and set PEAKGameRootDir"
    fi
    PEAK_ROOT="$(awk -F'[<>]' '/<PEAKGameRootDir>/ { print $3 }' Config.Build.user.props)"
    [ -n "${PEAK_ROOT}" ] || die "could not parse PEAKGameRootDir from Config.Build.user.props"
    # Trailing slash is optional in props; normalize both forms.
    PEAK_ROOT="${PEAK_ROOT%/}"
    ManagedDir="${PEAK_ROOT}/PEAK_Data/Managed/"
fi

[ -d "${ManagedDir}" ] || die "ManagedDir does not exist: ${ManagedDir}"

# Build the CLI first (release config). dotnet handles caching.
dotnet build src/GhostSpectator.PatchValidatorCli -c Release --nologo -v q >/dev/null

VALIDATOR_OUT="$(dotnet run --project src/GhostSpectator.PatchValidatorCli \
    -c Release --no-build -- --managed-dir "${ManagedDir}" 2>&1)" || {
    echo "${VALIDATOR_OUT}" >&2
    die "PatchValidator failed -- one or more patch targets missing"
}
echo "${VALIDATOR_OUT}"

PEAK_BUILDID="$(echo "${VALIDATOR_OUT}" | sed -nE 's/^PEAK buildid: (.+)$/\1/p' | head -n1)"
[ -n "${PEAK_BUILDID}" ] && [ "${PEAK_BUILDID}" != "<unknown>" ] \
    || die "could not extract PEAK buildid from validator output"

info "PEAK buildid: ${PEAK_BUILDID}"

# ---------------------------------------------------------------------------
# Detect dependency commits since previous tag (for CHANGELOG promotion)
# ---------------------------------------------------------------------------

if [ -z "${PREV_TAG}" ]; then
    DEPS_RANGE="HEAD"
else
    DEPS_RANGE="${PREV_TAG}..HEAD"
fi
DEPS="$(git log "${DEPS_RANGE}" --pretty=format:'%s' \
    | grep -E '^(chore|fix)\(deps\):' || true)"

# ---------------------------------------------------------------------------
# Promote [Unreleased] in CHANGELOG; inject Validated-against + Dependencies
# ---------------------------------------------------------------------------

info "promoting CHANGELOG section"

# awk handles the heading replacement + inserts Validated-against block
# above whatever sub-sections [Unreleased] already contains, and Dependencies
# below them. Uses ENVIRON[] to avoid awk's -v escape processing.
export VERSION DATE PEAK_BUILDID DEPS
awk '
    BEGIN {
        in_section = 0
        validated_emitted = 0
        deps_emitted = 0
        ver = ENVIRON["VERSION"]
        date = ENVIRON["DATE"]
        buildid = ENVIRON["PEAK_BUILDID"]
        deps = ENVIRON["DEPS"]
    }
    /^## \[Unreleased\]/ {
        print "## [Unreleased]"
        print ""
        print "## [" ver "] - " date
        print ""
        print "### Validated against"
        print ""
        print "- PEAK build " buildid
        validated_emitted = 1
        in_section = 1
        next
    }
    in_section && /^## \[/ {
        if (!deps_emitted && deps != "") {
            print "### Dependencies"
            print ""
            n = split(deps, lines, "\n")
            for (i = 1; i <= n; i++) if (lines[i] != "") print "- " lines[i]
            print ""
            deps_emitted = 1
        }
        in_section = 0
        print
        next
    }
    { print }
    END {
        if (in_section && !deps_emitted && deps != "") {
            print ""
            print "### Dependencies"
            print ""
            n = split(deps, lines, "\n")
            for (i = 1; i <= n; i++) if (lines[i] != "") print "- " lines[i]
        }
    }
' CHANGELOG.md > CHANGELOG.md.new
mv CHANGELOG.md.new CHANGELOG.md

# ---------------------------------------------------------------------------
# Bump <Version> in csproj
# ---------------------------------------------------------------------------

info "bumping <Version> in csproj"

sed_inplace \
    "s|<Version>[0-9]+\\.[0-9]+\\.[0-9]+</Version>|<Version>${VERSION}</Version>|" \
    src/GhostSpectator/GhostSpectator.csproj

# tcli's `publish --file` mode reads version from the zip's manifest.json
# (which ThunderPipe regenerates from csproj <Version> each build), so the
# toml's versionNumber is technically inert. But we keep it in sync anyway:
# (a) so anyone reading the repo at HEAD sees consistent metadata,
# (b) so a future invocation of tcli without --file picks up the right
#     version instead of stale 0.1.0.
info "bumping versionNumber in thunderstore.toml"

sed_inplace \
    "s|^versionNumber = \"[0-9]+\\.[0-9]+\\.[0-9]+\"|versionNumber = \"${VERSION}\"|" \
    thunderstore.toml

# ---------------------------------------------------------------------------
# Update README Compatibility block (sentinel-marked region)
# ---------------------------------------------------------------------------

info "updating README compatibility block"

if grep -q '<!-- COMPAT:start -->' README.md; then
    # awk-based replace of the sentinel-bracketed block. sed -i with /,/ ranges
    # is fragile in BSD sed; awk handles state cleanly.
    awk -v ver="${VERSION}" -v date="${DATE}" -v buildid="${PEAK_BUILDID}" '
        /<!-- COMPAT:start -->/ {
            print
            print "Validated against PEAK build **" buildid "** as of " date " (GhostSpectator " ver ")."
            skip = 1
            next
        }
        /<!-- COMPAT:end -->/ {
            skip = 0
            print
            next
        }
        !skip { print }
    ' README.md > README.md.new
    mv README.md.new README.md
else
    info "  (no COMPAT sentinels in README; skipping -- add <!-- COMPAT:start --> / <!-- COMPAT:end --> to enable)"
fi

# ---------------------------------------------------------------------------
# Build the Thunderstore zip
# ---------------------------------------------------------------------------

info "building Thunderstore zip"

dotnet build src/GhostSpectator -c Release --nologo -v q >/dev/null

BUILT_ZIP="artifacts/thunderstore/release/SisyphusMD-GhostSpectator-${VERSION}.zip"
[ -f "${BUILT_ZIP}" ] || die "expected zip not found at ${BUILT_ZIP}"

RELEASE_ZIP="releases/SisyphusMD-GhostSpectator-${VERSION}.zip"
cp "${BUILT_ZIP}" "${RELEASE_ZIP}"
info "  -> ${RELEASE_ZIP}"

# ---------------------------------------------------------------------------
# Extract the new CHANGELOG section for use as the tag annotation
# ---------------------------------------------------------------------------

# Escape regex metacharacters in the version (the dots) before passing to awk.
V_RE="$(printf '%s' "${VERSION}" | sed 's/\./\\./g')"
SECTION_FILE="$(mktemp -t ghostspec-release.XXXXXX)"
awk -v v="${V_RE}" '
    $0 ~ "^## \\[" v "\\]" { in_section=1; next }
    in_section && /^## \[/ { exit }
    in_section { print }
' CHANGELOG.md > "${SECTION_FILE}"
trap 'rm -f "${SECTION_FILE}"' EXIT

# ---------------------------------------------------------------------------
# Show diff + confirm
# ---------------------------------------------------------------------------

echo
echo "==================== DIFF ===================="
git --no-pager diff -- CHANGELOG.md src/GhostSpectator/GhostSpectator.csproj README.md || true
echo "==============================================="
echo
echo "New release: ${TAG}  (PEAK build ${PEAK_BUILDID})"
echo "Zip:         ${RELEASE_ZIP}  ($(wc -c < "${RELEASE_ZIP}" | tr -d ' ') bytes)"
echo
echo "Tag message preview:"
echo "---"
cat "${SECTION_FILE}"
echo "---"
echo

if [ "${DRY_RUN}" -eq 1 ]; then
    info "--dry-run: stopping before commit. Run 'git restore .' + 'rm ${RELEASE_ZIP}' to revert."
    exit 0
fi

# ---------------------------------------------------------------------------
# Commit, tag, push
# ---------------------------------------------------------------------------

info "committing"
git add CHANGELOG.md src/GhostSpectator/GhostSpectator.csproj thunderstore.toml README.md "${RELEASE_ZIP}"
git commit -m "release: ${VERSION}"

info "tagging (annotated, with CHANGELOG section as message)"
git tag -a "${TAG}" -F "${SECTION_FILE}"

info "pushing commit"
git push origin main

info "pushing tag (triggers publish workflow)"
git push origin "${TAG}"

info "done. tag ${TAG} pushed; publish workflow should fire on forgejo shortly."
