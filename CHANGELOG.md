# Changelog

What changed in each release, written for the people who use Hostpad. The commit
log covers the rest.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and
the versions follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
The section for a version is what the release workflow puts at the top of the
release notes, so it is written once and read in both places.

## [Unreleased]

## [1.0.3] - 2026-08-17

### Added

- Every release now publishes the SHA-256 of each file it contains, computed by
  the workflow that built them. The builds are not code-signed, so this is what
  you can check a download against; the readme explains how.
- Scoop and winget manifests. Scoop verifies the hash before installing, which
  does that check for you.

### Fixed

- Opening a vault written by a newer version of Hostpad asked for the master
  password first and only then admitted it could not read the file. It now says
  so straight away.

## [1.0.2] - 2026-08-07

### Added

- The licences of the bundled libraries travel with the software: the notices
  are inside every download, and **About** opens them.

## [1.0.1] - 2026-08-06

### Added

- A third download, `Hostpad.exe`: one file like the standalone build, but using
  an installed .NET runtime instead of carrying its own. A tenth of the size.

### Changed

- New icon. The chevron said nothing about what the application does; the mark
  is now an H whose crossbar runs past both posts — a link between two machines.

## [1.0.0] - 2026-08-06

First release.

### Added

- Connections in nested folders, with drag and drop, rename in place, and a
  search across name, hostname, username and notes.
- Six connection types: PuTTY, Remote Desktop, VNC, and WinSCP in its SFTP, SCP
  and FTP modes. Double click uses the type you chose; the right-click menu opens
  the same host with any of the others.
- SSH jump hosts, private keys, post-login commands and X11 forwarding as real
  fields rather than strings encoded inside other fields.
- Screen size, mounted drives, multiple monitors and admin sessions for Remote
  Desktop; full screen and view-only for VNC; passive mode for FTP.
- The connection list is always encrypted with AES-256-GCM. Without a master
  password the vault is tied to the Windows account through DPAPI; with one it
  also opens on another computer. Changing the password rewraps a key instead of
  re-encrypting, so it is instant at any size.
- Import from AutoPuTTY, including passwords, notes, jump hosts, and folders
  recovered from name prefixes. Import merges rather than replaces.
- Export a password-protected copy, with a choice of whether the saved
  credentials go with it.
- Saves atomically and keeps a backup, so a crash mid-save cannot truncate the
  connection list.
- Follows the Windows light and dark theme; remembers window position, size and
  pane widths, and refuses to restore onto a monitor that is no longer there.

[Unreleased]: https://github.com/goodmagma/Hostpad/compare/v1.0.3...develop
[1.0.3]: https://github.com/goodmagma/Hostpad/compare/v1.0.2...v1.0.3
[1.0.2]: https://github.com/goodmagma/Hostpad/compare/v1.0.1...v1.0.2
[1.0.1]: https://github.com/goodmagma/Hostpad/compare/v1.0.0...v1.0.1
[1.0.0]: https://github.com/goodmagma/Hostpad/releases/tag/v1.0.0
