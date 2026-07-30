# Changelog

All notable changes to YenSTT are documented here. The project follows semantic versioning.

## [Unreleased]

## [1.0.1] - 2026-07-30

### Added

- The Windows installer now offers `tiny`, `base`, `small`, `medium`, and `large-v3` with quality, storage, and memory guidance; `small` is recommended by default.
- Added model selection to the application settings and persisted the installer choice into YenSTT configuration.
- Added a worker command for preloading any supported model into the YenSTT cache.

## [1.0.0] - 2026-07-28

### Fixed

- Fixed UTF-8 communication with the bundled worker, which prevented transcription on Windows.
- Preserved Cyrillic text returned by faster-whisper.

### Changed

- Simplified settings for the fixed local `small` model and removed obsolete cloud/Python options.
- Removed the unreliable recording chunk mode and hidden legacy insertion flags.
- Removed migrated API credential files that YenSTT no longer uses.

### Added

- Local speech-to-text for Windows using faster-whisper `small` on CPU `int8`.
- Global push-to-talk and toggle hotkey modes.
- Automatic text insertion into the previously active window.
- Russian recognition and automatic language detection.
- Local term hints, punctuation commands, recording overlay, logs, and optional audio storage.
- Self-contained faster-whisper runtime with no Python installation required.
- Windows installer, portable ZIP, SHA-256 checksums, and GitHub Actions release workflow.

### Privacy

- Removed OpenAI Whisper API transcription and cloud LLM post-processing.
- Audio remains on the local computer.

[Unreleased]: https://github.com/yensepov/YenSTT/compare/v1.0.1...HEAD
[1.0.1]: https://github.com/yensepov/YenSTT/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/yensepov/YenSTT/releases/tag/v1.0.0
