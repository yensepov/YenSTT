# Changelog

All notable changes to YenSTT are documented here. The project follows semantic versioning.

## [Unreleased]

### Fixed

- Fixed UTF-8 communication with the bundled worker, which prevented transcription on Windows.
- Preserved Cyrillic text returned by faster-whisper.

## [1.0.0] - 2026-07-28

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

[Unreleased]: https://github.com/yensepov/YenSTT/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/yensepov/YenSTT/releases/tag/v1.0.0
