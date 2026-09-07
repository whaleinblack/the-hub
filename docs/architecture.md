# Architecture decision: native C# host

The Hub is a Windows desktop entry point for quick user actions, AI-assisted requests, and optional system customization. Its first release targets Windows 11 x64 and uses C#, WinForms, Win32 interop, and the inbox .NET Framework 4.8-compatible runtime. The build uses the system C# compiler and has no package restore step.

## Why this starting point

- WinForms provides native desktop controls, tray support, and a message loop. Win32 interop supplies narrowly scoped keyboard hooks and window placement.
- The Framework runtime is already installed on supported Windows 11 systems. Shipping the application does not require bundling a browser or a separate self-contained runtime.
- C# keeps Windows integration and maintenance manageable. Rust or C++ could reduce runtime overhead further, but increase the amount of windowing, accessibility, and lifetime code to maintain. Use them for a measured hotspot or a specialized isolated extension, rather than a speculative rewrite.
- Modern .NET with WinForms is the upgrade path if future dependencies or SDK requirements justify the deployment cost. This decision is reversible; avoid coupling domain commands to Framework-only types.

Sources: [Windows Forms overview](https://learn.microsoft.com/en-us/dotnet/desktop/winforms/overview/), [.NET Framework support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-framework).

## Current modules

`HubContext` owns the desktop-session lifetime, single instance, tray, UI dispatch, search capture, and feedback. A second process signals the existing instance to show the window. Startup uses `--background`; the Start menu uses `--show`.

`SearchShortcut` is the Windows input adapter. It handles Alt+Enter only when an official SystemApps search process owns the foreground window. The keyboard callback never performs UI Automation. `SearchReader` reads a unique visible non-password Edit control in a background task; timeout leads to manual input. Only one capture task can exist at once, so a stuck provider cannot cause unbounded worker creation.

`QueueDelivery.Send` is the primary delivery module. It invokes the installed Codex CLI's `queue --thread ... --message ...` through a hidden process with `UseShellExecute=false`, explicit Windows argument quoting, bounded output waits, and acknowledgement checks. A background task prevents blocking desktop input. A timeout is an uncertain delivery, never an automatic retry. The CLI talks to the shared daemon and starts or queues work in the existing desktop conversation. No UI typing is needed.

`CodexLauncher.Deliver` is the explicit clipboard fallback. It validates before side effects, copies only query text, and opens the existing conversation link. Clipboard and shell opening are injected at this seam for tests. Both paths preserve the existing thread's model and permissions.

`HubWindow` is the native user interface. `HintWindow` docks above the search panel and draws transient status. It never chooses the right-side companion panel area. The hint is non-activating and click-through, and hides if there is no room above. Windows DPI virtualization keeps geometry in a common coordinate space; mixed-DPI movement still needs desktop validation.

Idle polling runs roughly three times per second, speeding up only during feedback or capture. No network server, file indexer, background model, or disk scan runs at startup. Private settings and bounded metadata-only logs live next to the per-user installed executable; source control excludes these files.

## Extension direction

The first version has built-in features, not a public plugin loader. `IQuickFeature` covers tray activation and disposal; do not mistake this for a versioned SDK.

When adding the first real extension, introduce a shared command descriptor with a stable ID, display name, input schema, result schema, required capabilities, and cancellation. The native UI and a future agent adapter should invoke the same command interface.

1. **User actions:** on-demand app launchers, settings links, text tools, file actions. Prefer explicit targets and typed arguments.
2. **Agent actions:** a local CLI or user-restricted named pipe, then an MCP adapter if needed. Do not open an unauthenticated HTTP port. Validate peer identity, arguments, and capabilities. Tool discovery should be cheap.
3. **System customization:** a separately enabled worker with version checks, rollback, and narrowly requested elevation. Do not inject third-party code into the always-running core. Disable unsupported customizations after Windows changes rather than repeatedly retrying them.

Load extension workers on demand, terminate idle workers, bound concurrent work, and report failures without taking down global input. Add a transport or abstraction only when a real consumer needs it.

## Performance and verification

Measure idle CPU over a fixed interval, working set after settling, executable size, launch latency, and handle count. Report the workload and machine context; do not promise zero cost. Tests cover delivery failure semantics, Unicode, input bounds, process filtering, negative-coordinate monitor placement, and UI Automation against a synthetic Edit control. A synthetic control does not prove Windows Search compatibility.

The official deep link only navigates; primary delivery uses the newer local CLI `queue` interface, confirmed by the installed CLI's `queue --help` and a real fixed-thread reply. Older CLIs may not support it. Source for navigation: [desktop commands](https://learn.chatgpt.com/docs/reference/commands#deep-links).

Packaged desktop host processes can virtualize AppData and Start menu writes. The installer checks package identity and requires an ordinary unpackaged Windows PowerShell environment; it does not change system virtualization settings. Source: [MSIX desktop behavior](https://learn.microsoft.com/en-us/windows/msix/desktop/desktop-to-uwp-behind-the-scenes).
