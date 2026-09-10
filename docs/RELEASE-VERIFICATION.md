# Release verification

Version comes from `src/MultiMP3/MultiMP3.csproj`, currently 1.3.0. The About and sidebar labels read the SDK-generated AssemblyInformationalVersion attribute, excluding the source revision suffix.

## Repeatable checks

Run the build and executable regression commands in README. Providing a tools directory enables real synthesized-audio conversion, metadata, exact-copy and collision tests. Optional live tests require explicitly supplied URLs in environment variables and depend on upstream availability.

For UI checks, build with `-p:EnableUiTests=true`, set `MULTIMP3_DATA_DIR` to a fresh disposable directory and `MULTIMP3_SMOKE_DIR` to a separate output directory, and launch the test executable with media tools available. The harness exercises link entry, invalid input, album assignment, quality controls, settings persistence, collection import and cancellation, mode switching, local folder scanning, exact MP3 copying and About navigation. Its result file records pass/failure.

For documentation captures, use another fresh data directory and `MULTIMP3_PREVIEW_DIR`. The harness renders real WPF views with synthetic sample data at 1280 × 860 and the minimum 1040 × 720 window size. It does not download sample URLs. Inspect captures for clipping and personal information before committing them.

Normal production builds exclude both harnesses. Build the public ZIP using Package-Release.ps1; inspect the archive for media, state, debug files and credentials. Confirm upstream runtime licenses, setup scripts, version, ZIP checksum and published links. Test setup against an extracted copy before publishing.

The project has no separate JavaScript lint or TypeScript commands; C# compilation and its regression executable are the applicable checks.

## First public release results

- Release compilation: passed, zero warnings/errors.
- Regression executable with real media tools: all 50 checks passed.
- Isolated WPF UI smoke: passed, including About navigation/version and exact local MP3 copying.
- Normal/minimum-size WPF captures: inspected at 1280 × 860 and 1040 × 720.
- Production ZIP: extracted successfully; application launched; opt-in test harness remained excluded.
- Native folder picker: Browse opened the Windows dialog; selecting a disposable destination persisted the chosen folder, verified through UI Automation and isolated library state.
- First-run Setup-Engine from extracted ZIP: passed published checksum validation and installed all four media tools.
- Source and ZIP privacy scan: no personal collection links, credentials, media, history, debug symbols or bundled media executables found.
- External ecosystem link: verified against the supplied ChudGPT landing page.

Live media access remains dependent on upstream services. Local conversion, queue behavior and source preservation were tested without requiring a user's personal music library.
