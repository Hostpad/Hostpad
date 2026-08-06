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

Building needs the .NET 10 **SDK**, which is a different package from the
runtime listed above. Install it with winget:

```bash
winget install Microsoft.DotNet.SDK.10
```

Open a new terminal afterwards, so the updated PATH is picked up, and check the
SDK is visible:

```bash
dotnet --list-sdks
```

The output must include a `10.x` entry. If `dotnet` is found but no 10.x SDK is
listed, only the runtime is installed and the build will fail on the target
framework.

Then, from the repository root:

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project Hostpad.App
```

Visual Studio 2022 17.14 or later opens `Hostpad.sln` directly. Set
`Hostpad.App` as the startup project.

Warnings are errors in this repository, so a build that prints nothing is a
build that passed.

## Migrating from AutoPuTTY

Use **Import from AutoPuTTY** in the settings menu and point it at an existing
`autoputty.xml`. The original file is left untouched.

Passwords and comments come across, and two AutoPuTTY conventions become real
structure: a name like `Customer: server` turns into a folder plus a name, and
the `proxyuser@proxyhost:port#user` jump syntax becomes a jump host with its own
fields. Tool paths and options can be imported too, if you want them.

If the list was protected by an AutoPuTTY master password, Hostpad asks for it.
Otherwise it opens the file with AutoPuTTY's built-in key.

> Note that an `autoputty.xml` without a master password is encrypted with a key
> published in AutoPuTTY's own source, so anyone can read it. Treat such files as
> plaintext: they hold hostnames, usernames and passwords.

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
