# Hostpad

A connection manager for Windows. Keep your servers in one list and launch SSH,
SFTP, FTP, RDP or VNC sessions with a double click.

> **Status:** early development. Not yet usable.

## What it does

- One address book for every remote machine, organised in folders and tags
- Launches your existing tools — PuTTY, WinSCP, Remote Desktop, a VNC viewer
- Reusable profiles, so "PuTTY with this key and this post-login command" is
  configured once instead of per server
- SSH jump hosts as real fields, not encoded inside the username
- Connect to several machines at once
- Credentials encrypted at rest, optionally behind a master password
- Windows 11 look with light and dark themes

## Requirements

- Windows 10 or 11
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- The client tools you intend to use (PuTTY, WinSCP, a VNC viewer). Remote
  Desktop ships with Windows.

## Building

```bash
dotnet build
dotnet test
dotnet run --project Hostpad.App
```

Requires the .NET 10 SDK.

## Migrating from AutoPuTTY

Hostpad imports an existing `autoputty.xml` on first run, including encrypted
passwords and the `proxyuser@proxyhost:port#user` jump syntax. The original file
is left untouched.

## About this project

Hostpad began as a fork of [AutoPuTTY](https://github.com/r4dius/AutoPuTTY) by
r4dius, but shares no code with it: the application was rewritten from scratch
in .NET 10 and WPF. Thanks to r4dius for the original idea and for a tool that
did the job for many years.

## Name and official builds

The source is free software under the GPL — fork it, modify it, redistribute it.
The **name "Hostpad" is not covered by that licence**: please rename your fork
so users can tell the projects apart.

The only official releases are published at
<https://github.com/goodmagma/Hostpad>. Copies distributed elsewhere, in
particular paid ones, are not from us.

## Licence

GNU General Public License v3.0 or later — see [LICENSE](LICENSE).
