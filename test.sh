#!/bin/bash
# Build, run tests/TestRunner.cs with a time limit, then fail on a bad exit code or an error in the log
# (Godot often exits 0 after an error, and only warns about a stale uid).
GODOT="${GODOT:-/c/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe}"
dotnet build --nologo -v q || exit 1
[ -d .godot/imported ] || timeout -k 5 600 "$GODOT" --headless --import > /dev/null 2>&1 # a fresh copy
timeout -k 5 300 "$GODOT" --headless --fixed-fps 60 --path . -s res://tests/TestRunner.cs > .godot/test.log 2>&1
code=$?
cat .godot/test.log
# Not counted: the editor plugin, which a fresh copy doesn't have, and what's left over on quitting (a song still loading).
errors=$(sed 's/\x1b\[[0-9;]*m//g' .godot/test.log | grep -E '^(SCRIPT )?ERROR:|invalid UID' | grep -vcE 'godot_ai|_mcp_game_helper|at exit')
echo "exit=$code error_lines=$errors"
[ "$code" -eq 0 ] && [ "$errors" -eq 0 ]
