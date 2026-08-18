# Contributing to Hostpad

Thanks for taking the time. Bug reports, ideas and patches are all welcome, and
small fixes are as useful as big features.

Hostpad is small on purpose: an address book for remote machines that hands the
work to PuTTY, WinSCP, mstsc and your VNC viewer. Changes that keep it that way
have an easy path in. Changes that turn it into a terminal, a file manager or a
protocol implementation do not, however well written.

## Reporting a bug

Open an issue and include the Hostpad version from **About**, your Windows
version, and what you did, expected and got instead. A screenshot helps for
anything visual.

If Hostpad showed an error dialog, quote it exactly. If it closed on its own,
look in Windows Event Viewer under **Windows Logs → Application** for a .NET
Runtime entry with the stack trace, and paste that.

**Do not put real hostnames, usernames or passwords in an issue.** Invent
replacements; the bug almost never depends on the actual values.

## Reporting a security problem

Do not open a public issue for anything touching the vault, the encryption, the
master password or DPAPI. Use GitHub's private reporting instead: the
**Security** tab of the repository, then **Report a vulnerability**. That opens
a channel only you and the maintainer can read.

## Suggesting a feature

Open an issue and describe the problem before the solution — what you were
trying to do and where Hostpad got in the way. That leaves room for a design
that fits the rest of the app.

Worth knowing before you write a long proposal: connecting is always delegated
to an external tool, credentials never leave the encrypted vault, and the
application stays a single window with no background service.

## Building

You need the .NET 10 **SDK**, which is a different package from the runtime the
application asks users to install:

```bash
winget install Microsoft.DotNet.SDK.10
```

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

Visual Studio 2022 17.14 or later opens `Hostpad.sln` directly; set
`Hostpad.App` as the startup project.

Warnings are errors here, so a build that prints nothing is a build that passed.
Do not silence a warning to get green — fix what it points at, and if it is
genuinely wrong, suppress it narrowly with a comment saying why.

## How the code is arranged

Two projects, and the split is deliberate:

- `Hostpad.Core` — model, storage, cryptography and the command building for
  each tool. No user interface, and no Windows-only API outside the single class
  that talks to DPAPI. This is where the tests live.
- `Hostpad.App` — the WPF layer, using [WPF-UI](https://github.com/lepoco/wpfui)
  and MVVM through the .NET Community Toolkit.

The launchers build a command and return it rather than starting the process
themselves. That is what lets argument building be tested without spawning
anything, so please keep new launchers to the same shape.

## Tests

`Hostpad.Core.Tests` uses xUnit. Anything in `Hostpad.Core` should arrive with
tests: the vault format, the crypto and the argument building are exactly the
places where a silent regression costs someone their connection list.

The WPF layer has no automated tests. Say in the pull request what you clicked
through by hand.

## Branches and commits

`develop` is the default branch and where pull requests go. `master` holds
released code only and moves forward by fast-forward when a version ships;
releases are cut by pushing a `v*` tag, which is what builds the downloads.

Cutting one, in order:

1. Bump `VersionPrefix` in `Directory.Build.props`.
2. In `CHANGELOG.md`, turn `## [Unreleased]` into `## [x.y.z] - <date>`, put a
   fresh empty `Unreleased` above it, and update the compare links at the bottom.
3. Commit on `develop`, then fast-forward `master` and push it.
4. Push the `v*` tag. That is what builds the downloads.
5. When the workflow has finished, bump the winget manifests to the version it
   just published — see `packaging/README.md`. Scoop needs nothing.

The workflow reads the version from the tag and the notes from that changelog
section, so a tag with no matching section still releases, with notes that say
nothing.

Step 5 cannot be folded into the others: a manifest pins the SHA-256 of the file
it installs, and that does not exist until the workflow has built it.

Commit subjects follow what is already in the log: a type, a colon, and the
change in the imperative, lowercase, no full stop.

```
feat: remember which folders were open
fix: open menus where they were asked for
docs: credit the bundled libraries
ci: ship a single-file build
```

Use the body for **why**, not what — the diff already says what. If the reason
is obvious, leave the body out.

The same applies to comments in the code: the codebase explains reasons and
surprises, not mechanics. A comment restating the line below it will be asked
about in review.

## Pull requests

- Branch from `develop` and target `develop`.
- One concern per pull request. Unrelated cleanups in the same branch make the
  actual change hard to judge.
- Match the surrounding style rather than your own preference. The code has a
  voice; keep it.
- CI runs the build and the tests on every pull request, and it must be green.
- Say how you tested. "Builds" is not testing.
- If someone using Hostpad would notice the change, add a line under
  `## [Unreleased]` in `CHANGELOG.md`. Write it for them, not for the reviewer:
  the release workflow copies that section into the release notes, so it is read
  by people who never see this repository. Internal work needs no entry.

Formatting-only churn across files you did not otherwise touch will be asked to
come out.

## Adding a dependency

Please ask in an issue first. Each package is weight in the download, another
licence to honour and another thing that can break on a runtime update, so the
bar is high.

If one does go in, add its notice to `THIRD-PARTY-NOTICES.md`, with the
copyright text taken from the package itself rather than from memory, and add
the project to the *Built with* section of the README. Test-only packages need
neither: they are not part of anything distributed.

The licence must be compatible with the GPLv3. MIT, BSD and Apache-2.0 are;
anything copyleft-incompatible or non-commercial is not.

## Icon and screenshot

`docs/icon.svg` is the source of the icon design. Nothing in WPF can rasterise
an SVG, so the shapes are mirrored in code and the images are generated:

```bash
dotnet run --project Hostpad.App -- --render-icon
```

That writes the PNGs to the temporary directory, from which `hostpad.ico` and
`Assets/hostpad.png` are assembled. Change the SVG and the drawing code
together, or they drift apart.

The README screenshot comes from a throwaway vault of invented servers, so no
one's real machines ever appear in it:

```bash
dotnet run --project Hostpad.App -- --demo
```

That captures the window and exits. Add `--keep` to leave it open instead, for
screenshots taken by hand — a listing on a download site, say — without any risk
of your own machines appearing in one:

```bash
dotnet run --project Hostpad.App -- --demo --keep
```

## Packaging

`packaging/` holds the winget manifests, pinned to a version and to the SHA-256
of the file they install. The release workflow does not touch them, so after a
release they describe the previous one until someone bumps them;
`packaging/README.md` says how, and how they get published.

The Scoop manifest is not here. It lives in
[goodmagma/scoop-bucket](https://github.com/goodmagma/scoop-bucket), where an
action follows new releases and commits the new hash on its own.

Take hashes from the release notes rather than recomputing them. The workflow
computes them from the files it just built, which is the whole reason they are
worth anything.

## Licence

Hostpad is GPLv3 or later. By contributing you agree that your work ships under
that licence. There is no contributor licence agreement to sign.

One thing the licence does not cover: the **name "Hostpad"**. Fork the code
freely, but give your fork its own name so users can tell the projects apart.
