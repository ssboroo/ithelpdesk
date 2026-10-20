# Third-party components

RAINY Remote is an independent Windows UI that invokes the unmodified RustDesk executable as a separate process. It is not AnyDesk and is not affiliated with AnyDesk or RustDesk.

RustDesk is not bundled in this installer or portable binary. On the user's request, the client downloads the official executable directly from its publisher, verifies the pinned SHA256, and stores it locally. The publisher's original UI, branding, permission prompts and license remain unchanged.

- Version: 1.5.0, Windows x64
- Binary: https://github.com/rustdesk/rustdesk/releases/download/1.5.0/rustdesk-1.5.0-x86_64.exe
- SHA256: 8555777215510d83d2d61c9dc984e4fcc838bd7e79f9d18a42585431f5e8bb47
- Source: https://github.com/rustdesk/rustdesk/tree/1.5.0
- License: GNU Affero General Public License v3.0 (see upstream LICENSE)
- License text: https://github.com/rustdesk/rustdesk/blob/1.5.0/LICENCE
- Python and Tcl/Tk are included by PyInstaller; their license files are retained in the generated runtime.

The download pin is not updated silently. Updating the engine requires reviewing the official release's digest and source, updating remote.py, running tests, and publishing a new client.
