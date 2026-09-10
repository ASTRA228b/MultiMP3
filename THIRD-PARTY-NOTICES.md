# Third-party components

MultiMP3 source code and its original artwork are MIT licensed. This license
does not replace the licenses of independently distributed dependencies.

## Included in the public application ZIP

The self-contained Windows build includes the Microsoft .NET runtime and WPF
desktop runtime. The release carries the license and third-party notice files
from the exact runtime packs used to build it in `licenses/`. See
[.NET licensing information](https://github.com/dotnet/core/blob/main/license-information.md).

## Installed separately by the user

The public ZIP does **not** redistribute yt-dlp, FFmpeg, FFprobe, or Node.js.
`Setup-Engine.cmd` downloads their upstream distributions to the app's local
`tools` directory, verifies the publishers' SHA-256 checksums, and retains
license information. Users can also provide compatible binaries on PATH.

| Component | Purpose | Source and licensing information |
| --- | --- | --- |
| yt-dlp | Public media metadata and retrieval | [Source and license](https://github.com/yt-dlp/yt-dlp#license). Standalone executable licensing also covers its bundled dependencies. |
| FFmpeg / FFprobe | Audio conversion and metadata inspection | [FFmpeg legal information](https://ffmpeg.org/legal.html); [Gyan Windows builds](https://www.gyan.dev/ffmpeg/builds/). The selected essentials build has its own GPL license. |
| Node.js 22 | JavaScript runtime used by yt-dlp | [Node.js license and third-party notices](https://github.com/nodejs/node/blob/main/LICENSE) |

These tools run as separate processes. MultiMP3 does not modify them. If you
redistribute a package containing these tools, review and fulfill their
respective redistribution and corresponding-source obligations.

YouTube and Amazon Music are trademarks of their respective owners. MultiMP3
is not affiliated with or endorsed by either service.
