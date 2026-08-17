# Packaging

Manifests that let package managers install Hostpad. They are kept in this
repository so that a release and the description of that release move together;
publishing them is a separate, manual step described below.

Both manifests package `Hostpad-<version>-win-x64.exe`, the self-contained
build. It is much the largest download, and it is still the right one here: a
package manager that installed the framework-dependent build would leave people
with an application that refuses to start until they find the .NET runtime
themselves.

Neither package needs to preserve anything on upgrade. Hostpad keeps its vault
and settings in `%USERPROFILE%\.hostpad`, outside whatever directory the package
manager owns.

## Scoop

`scoop/hostpad.json`. Scoop installs from a *bucket*, which is a Git repository
of manifests, so the file has to be reachable from one of those.

The quickest route, needing no new repository, is a direct install from a raw
URL:

```bash
scoop install https://raw.githubusercontent.com/goodmagma/Hostpad/master/packaging/scoop/hostpad.json
```

That works, but it does not put Hostpad in anyone's search results, and Scoop
will not update it. For that, create a repository named `scoop-bucket` under the
same account, copy this file into its `bucket/` directory, and the install
becomes:

```bash
scoop bucket add goodmagma https://github.com/goodmagma/scoop-bucket
```

```bash
scoop install goodmagma/hostpad
```

The manifest already carries `checkver` and `autoupdate`, so a bucket repository
with the standard `excavator` action will follow new GitHub releases on its own:
it reads the latest tag, rewrites `version` and the URL, downloads the file and
computes the new hash. No manual edit per release.

## winget

`winget/<version>/`, three files as the schema requires: version, installer and
`en-US` locale.

Validate any change before submitting:

```bash
winget validate --manifest packaging/winget/1.0.2
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
wingetcreate update goodmagma.Hostpad --version 1.0.3 --urls https://github.com/goodmagma/Hostpad/releases/download/v1.0.3/Hostpad-1.0.3-win-x64.exe --submit
```

## Updating for a new release

The hashes here are the SHA-256 of the published assets, and the release notes
print the same values — the workflow computes them from the files it just built.
Take them from the release page rather than recomputing by hand, and the two
places cannot disagree.
