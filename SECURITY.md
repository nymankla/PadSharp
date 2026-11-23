# Security policy

## Supported versions

Only the latest release, and the `main` branch, get security fixes.

## Reporting a vulnerability

**Please don't report security problems in public issues.** Use GitHub's private reporting instead: open the
repository's **Security** tab and choose **Report a vulnerability**. Include what you found, how to reproduce it,
and the PadSharp version and operating system.

You'll get a reply within a week. Once a fix is ready, it is released and the report is credited, unless you prefer
to stay anonymous.

## Scope

PadSharp is an offline text editor: it doesn't open network connections or run code from the files it opens. These
are in scope:

- reading or writing files outside what the user opened or saved
- the session and buffer files in the data folder, for example a crafted `session.json` making PadSharp read or
  write files elsewhere
- crashes or hangs caused by opening a crafted file
- the `ed` launcher scripts running anything other than PadSharp
