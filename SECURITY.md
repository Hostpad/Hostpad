# Security policy

## Reporting

Do not open a public issue. Use GitHub's private reporting: the **Security** tab
of this repository, then **Report a vulnerability**. That opens a channel only
you and the maintainer can read.

Please include the Hostpad version, what an attacker would gain, and the steps
to reproduce it. A proof of concept helps; invented hostnames and passwords are
fine and preferred.

Hostpad is maintained by one person in their own time. You will get an
acknowledgement as soon as it is read, and an honest estimate rather than a
promised deadline. There is no bounty programme.

Once a fix ships you will be credited in the release notes, unless you would
rather not be.

## Supported versions

Only the latest release. Fixes go into a new version rather than backwards into
old ones.

## In scope

- Anything that exposes vault contents — stored passwords, private key paths,
  hostnames — to a party that should not read them
- Weaknesses in the vault encryption, the master password handling, or the use
  of DPAPI
- Credentials leaking outside the vault: into command lines visible to other
  processes, log files, crash dumps, temporary files or the clipboard beyond
  what the user asked for
- Import of an `autoputty.xml` being made to do something other than import
- Any path where Hostpad launches something other than the tool that was
  configured

## Out of scope

These are known, documented and not treated as vulnerabilities:

- **SmartScreen warnings.** The builds are not code-signed. The warning is
  expected. What stands in for a signature is provenance you can check: every
  release is built by the workflow in this repository from the tagged commit,
  and that workflow prints the SHA-256 of each file it built into the release
  notes. Compare it with `Get-FileHash <file> -Algorithm SHA256`, as
  [the readme](README.md#verifying-your-download) describes. A file whose hash
  does *not* match the release page did not come from here, and that is worth
  reporting.
- **An `autoputty.xml` without a master password.** AutoPuTTY encrypts those
  with a key published in its own source, so anyone can read them. Hostpad can
  therefore read them too. This is a property of the file you are importing, not
  of Hostpad; treat such files as plaintext.
- **An attacker who already has your Windows session.** DPAPI protection is
  bound to that session by design. Code running as you, with your desktop
  unlocked, is outside the model.
- Weaknesses in PuTTY, WinSCP, mstsc or your VNC viewer. Report those to their
  projects. If Hostpad hands them arguments that make them behave unsafely, that
  part is ours and is in scope.
