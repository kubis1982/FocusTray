# Changelog

All notable changes to FocusTray will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Planned
- Integration tests for JIRA service
- Additional timer presets (5min, 10min, 15min, 30min, 45min, 60min)
- Dark mode support
- Session history and statistics
- Export session data to CSV
- Keyboard shortcuts

## [0.1.0] - TBD

### Added
- Initial release of FocusTray
- System tray integration with dynamic icons (idle/active states)
- Customizable focus sessions (1 minute to 24 hours)
- Automatic worklog submission to JIRA issues
- JIRA issue selection from assigned, non-completed tasks
- Configurable JQL filters for JIRA issue queries
- Native Windows toast notifications
- Real-time session countdown in tray tooltip
- Session status dialog with analog clock visualization
- Session management (start, pause, resume, stop)
- Settings persistence in %LocalAppData%\FocusTray\settings.json
- Self-contained deployment (.NET 10.0 embedded)
- Clean Architecture (UI, Core, Infrastructure layers)
- Unit tests with xUnit v3 and AwesomeAssertions
- MIT License

### Technical Details
- .NET 10.0 with WPF
- CommunityToolkit.Mvvm for MVVM pattern
- H.NotifyIcon.Wpf for system tray
- Serilog for structured logging
- Single-file executable deployment

[Unreleased]: https://github.com/kubis1982/FocusTray/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/kubis1982/FocusTray/releases/tag/v0.1.0
