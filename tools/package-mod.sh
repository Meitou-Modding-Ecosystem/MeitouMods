#!/usr/bin/env bash
# Builds one mod into a zip: the mod's folder as it goes into Kenshi/mods/, holding only the mod's own files (the game supplies its
# assemblies). A mod without a <Mod>.mod in its build output gets an empty one, since the game loads a code mod through its data mod.
# Usage: tools/package-mod.sh <mod folder> <zip to write> [extra dotnet arguments]
set -euo pipefail

folder="$1"
zip="$(realpath -m "$2")"
shift 2
out="package/$folder"

rm -rf "$out" "$zip"
dotnet publish "$folder" -c Release -o "$out" "$@"

find "$out" -mindepth 1 -maxdepth 1 -type d -exec rm -rf {} +
find "$out" -maxdepth 1 -type f ! -name "$folder.dll" ! -name "$folder.pdb" ! -name "$folder.mod" ! -name meitou.json -delete

if [ ! -f "$out/$folder.mod" ]; then
    dotnet run --project tools/EmptyMod -c Release "$@" -- "$out/$folder.mod"
fi

(cd package && zip -r -q "$zip" "$folder")
echo "Packed $zip:"
unzip -l "$zip"
