# The Hub development

Read `docs/architecture.md` before changing process lifetime, Windows integration, or extension interfaces. Build and verification commands are in `README.md`.

Keep personal conversation IDs, local paths, logs, and credentials outside tracked files. User configuration belongs in the installed user directory. Keep keyboard callbacks bounded; perform UI Automation away from the keyboard hook and UI thread. Background features must leave normal desktop input working when unavailable.

Verify the affected native interaction and distinguish automated tests from actual Windows Search / Codex end-to-end checks. Preserve existing connection settings during updates. Development changes happen in this repository; the earlier Windows Companion prototype is superseded.
