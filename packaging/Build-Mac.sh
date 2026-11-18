#!/bin/zsh
set -euo pipefail

repo="${0:A:h:h}"
dotnet_bin="${DOTNET_BIN:-dotnet}"
publish="$repo/artifacts/mac/publish"
app="$repo/artifacts/mac/MultiMP3.app"

"$dotnet_bin" publish "$repo/src/MultiMP3.Mac/MultiMP3.Mac.csproj" \
  --configuration Release --runtime osx-arm64 --self-contained true \
  -p:PublishSingleFile=false -o "$publish"
rm -rf "$app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -R "$publish/"* "$app/Contents/MacOS/"
cp "$repo/packaging/Info.Mac.plist" "$app/Contents/Info.plist"
chmod +x "$app/Contents/MacOS/MultiMP3.Mac"
codesign --force --deep --sign - "$app"
codesign --verify --deep --strict "$app"
echo "$app"
