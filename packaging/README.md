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

Validate any change before submitting:

```bash
winget validate --manifest packaging/winget/1.0.3
```

Publishing means opening a pull request against
[microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs), copying the
three files to `manifests/g/goodmagma/Hostpad/<version>/`. Automated validation
runs on the pull request and a moderator reviews it; the first submission for a
new publisher takes longer than later ones.

Once accepted:

```bash
winget install goodmagma.Hostpad
```

`wingetcreate update` can produce the next version's manifests from the
published ones, which is less error-prone than editing three files by hand:

```bash
wingetcreate update goodmagma.Hostpad --version 1.0.4 --urls https://github.com/goodmagma/Hostpad/releases/download/v1.0.4/Hostpad-1.0.4-win-x64.exe --submit
```

## Updating for a new release

Scoop looks after itself. winget does not, so after a release the manifests here
describe the previous one until someone bumps them: rename the directory, change
`PackageVersion` in all three files, and update the URL, the hash and
`ReleaseDate` in the installer manifest.

Take the hash from the release notes rather than recomputing it. The workflow
computes it from the file it just built, which is the only reason it is worth
anything — a hash produced afterwards from the same download proves nothing
except that the download did not change while you were looking at it.
