#!/usr/bin/env bash
# Creates or updates a dedicated, detached checkout of M3Undle at the commit pinned in
# build/m3undle-core.ref. The Analyzer builds M3Undle.Core from that checkout.
# The M3Undle repository is public: no credentials are needed or used.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
sha="$(tr -d '[:space:]' < "$repo_root/build/m3undle-core.ref")"
dest="${M3UNDLE_CORE_DIR:-$repo_root/../M3Undle-core}"
remote="${M3UNDLE_CORE_REMOTE:-https://github.com/Sydney-Elvis/M3Undle.git}"

if [[ ! "$sha" =~ ^[0-9a-f]{40}$ ]]; then
  echo "error: build/m3undle-core.ref must contain a 40-character commit SHA" >&2
  exit 1
fi

if [[ -e "$dest" && ! -d "$dest/.git" ]]; then
  echo "error: $dest exists but is not a git checkout; refusing to touch it" >&2
  exit 1
fi

marker="m3u-analyzer-managed"

if [[ ! -d "$dest/.git" ]]; then
  git clone --quiet --filter=blob:none --no-checkout "$remote" "$dest"
  touch "$dest/.git/$marker"
else
  # Safety: only ever manage a clean checkout that this script created (marker file inside .git).
  if [[ ! -f "$dest/.git/$marker" ]]; then
    echo "error: $dest was not created by this script; it looks like a working checkout. Refusing to change it." >&2
    echo "       Point M3UNDLE_CORE_DIR at a different directory." >&2
    exit 1
  fi
  if [[ -n "$(git -C "$dest" status --porcelain)" ]]; then
    echo "error: $dest has local changes; refusing to change it" >&2
    exit 1
  fi
fi

git -C "$dest" fetch --quiet origin "$sha"
git -C "$dest" -c advice.detachedHead=false checkout --quiet "$sha"

actual="$(git -C "$dest" rev-parse HEAD)"
if [[ "$actual" != "$sha" ]]; then
  echo "error: expected $sha but $dest is at $actual" >&2
  exit 1
fi

echo "M3Undle.Core source ready: $dest @ $sha"