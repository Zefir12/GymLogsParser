#!/bin/sh
# PostToolUse hook for Edit/Write: fixes whitespace in the edited .cs file with
# `dotnet format whitespace --folder` (no project load, so it's quick) if dotnet is on
# the host. Silently does nothing otherwise: this host usually has no dotnet, and the
# frontend has no formatter configured.
command -v dotnet >/dev/null 2>&1 || exit 0
file=$(cat | grep -o '"file_path"[[:space:]]*:[[:space:]]*"[^"]*"' | head -1 | sed 's/.*"\([^"]*\)"$/\1/')
case "$file" in
  */Migrations/*) ;; # generated, or hand-copied from generated code: leave as written
  *.cs) [ -f "$file" ] && dotnet format whitespace "$(dirname "$file")" --folder --include "$file" >/dev/null 2>&1 ;;
esac
exit 0
