# Packaging

How package managers install Hostpad. Each pins a version and the SHA-256 of the
file it installs, so a manifest can only be written after the release it
describes: the hash does not exist until the workflow has built the file.

Both package `Hostpad-<version>-win-x64.exe`, the self-contained build. It is
much the largest download, and it is still the right one here: a package manager
that installed the framework-dependent build would leave people with an
application that refuses to start until they find the .NET runtime themselves.

Neither needs to preserve anything on upgrade. Hostpad keeps its vault and
settings in `%USERPROFILE%\.hostpad`, outside whatever directory the package
manager owns.

## Scoop

The manifest does **not** live here. It is in
[goodmagma/scoop-bucket](https://github.com/goodmagma/scoop-bucket), because
Scoop installs from a *bucket*, which is a Git repository of manifests.

```bash
scoop bucket add goodmagma https://github.com/goodmagma/scoop-bucket
```

```bash
scoop install goodmagma/hostpad
```

There was a copy in this directory. It is gone on purpose: the bucket runs the
`excavator` action, which reads the manifest's `checkver` block, notices a new
release, downloads the file, computes the hash and commits it. A second copy
here would not receive any of that, and would quietly serve an old version to
anyone who found it.

So a release needs nothing done for Scoop. Within a day the bucket has followed
it.

## winget

`winget/<version>/`, three files as the schema requires: version, installer and
`en-US` locale.

Publishing means a pull request against
[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs), which puts
the three files under `manifests/g/goodmagma/Hostpad/<version>/`. Do not clone
that repository to do it: it holds manifests for hundreds of thousands of
packages. `wingetcreate` works through the API instead and never checks anything
out.

```bash
winget install Microsoft.WingetCreate
```

```bash
wingetcreate token --store
```

`token --store` with no `--token` after it starts a device login. Do not pass a
personal access token on the command line, as the tool itself warns: it would
land in the shell history.

Validate, then submit:

```bash
winget validate --manifest packaging/winget/1.0.3
```

```bash
wingetcreate submit packaging/winget/1.0.3
```

Two things will stop the first attempt, both once only. If you already have a
fork of `winget-pkgs`, submitting fails with *the forked repository is behind by
too many commits* — open the fork on GitHub, **Sync fork**, and run it again. And
a Microsoft repository needs its contributor licence agreement signed: a bot
comments on the pull request asking you to reply with a single line agreeing to
it. Signing is per account and lasts.

After that a pipeline downloads the installer, checks it against the hash in the
manifest and installs it in a sandbox. A new package then waits for a human
moderator, which is the slow part. Later versions of a package that already
exists are merged without one.

Once accepted:

```bash
winget install goodmagma.Hostpad
```

## Updating for a new release

Scoop looks after itself. winget does not, so after a release the manifests here
describe the previous one until someone bumps them.

`wingetcreate update` does the whole thing — it fetches the published manifests,
rewrites the version, downloads the file to compute its hash, and opens the pull
request:

```bash
wingetcreate update goodmagma.Hostpad --version 1.0.4 --urls https://github.com/goodmagma/Hostpad/releases/download/v1.0.4/Hostpad-1.0.4-win-x64.exe --submit
```

Copy what it produces back into `packaging/winget/<version>/` so this directory
keeps matching what was submitted.

By hand instead: rename the directory, change `PackageVersion` in all three
files, and update the URL, the hash and `ReleaseDate` in the installer manifest.
Take the hash from the release notes rather than recomputing it. The workflow
computes it from the file it just built, which is the only reason it is worth
anything — a hash produced afterwards from the same download proves nothing
except that the download did not change while you were looking at it.
