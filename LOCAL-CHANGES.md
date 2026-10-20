# A2 Pet Tracking by Lacu

Private local edition, version 1.0.0, based on PetTracker commit a9d3cbf1faf48fe68dc463a4ae5a640648678831 (0.1.51).

Changes: remote updater and Velopack dependency removed; publisher keys, GitHub automation and release scripts removed; browser launch actions removed; own app identity and IPC names; portable data folder; redacted on-disk connection report; no retained raw packet samples; .NET SDK 10.0.402/runtime 10.0.13; offline local NuGet source.

Original copyright and GPL-3.0-only license remain in NOTICE and LICENSE. Third-party rights remain unchanged. Updates are made by editing this source and running Build-Local.ps1. The application does not fetch source repositories or publish updates.

## Local version 1.0.1
Observed mob names now appear in parentheses below the pet name when different. Applies to Recent and Nearby, groups distinct observed variants, suppresses duplicate and unknown labels, and preserves rank/progress layout and expiry animations. Build-Local.ps1 -PrepareOnly builds and tests a candidate without replacing the running application.
The tracker X and window-close action now save pending settings, request the capture worker to save and exit, close all app windows and remove the tray process. The public launcher is A2Pet Tracking.lnk, the executable is A2Pet Tracking.exe, and all technical files and saved data live beneath the single Code folder.

## Local version 1.0.2

- Added Hide Shortcut settings with a focused shortcut recorder, Alt+D default, conflict feedback, disable and reset. Uses RegisterHotKey; no keyboard hook or keystroke recording. The previous fixed Ctrl+Alt shortcut preference is retained only for file compatibility.
- Added a Merchant main tab with Special/Pets sub-tabs, exact decoded refresh countdown, collection rank and observed Kina prices. Displays every decoded pet offer, including maxed and farmable pets.
- Added separate, session-scoped last-observed inventory until the server refresh timestamp. Changing shop tabs preserves both observations; unknown mutations, malformed replacements, capture gaps and character/connection changes invalidate them. This inventory is never written to disk and does not change collection totals. Expired offers disappear; the next rotation requires opening the shop again.
- Kept the existing two-minute recommendation freshness policy independently of the dedicated inventory page. No external inventory service or updater was added.

## Local version 1.0.3

- Added a startup prerequisite window before capture or normal UI startup: package SHA-256 integrity, embedded-runtime marker, data-folder write access, actual Npcap driver state, library loading, adapter enumeration and admin-only access configuration.
- Main application and capture worker are published as compressed self-contained single-file executables. Icon and portraits are embedded. A recipient needs no separately installed .NET SDK/runtime.
- Confirmed missing Npcap automatically downloads the fixed official 1.89 installer over HTTPS and checks exact length and SHA-256 before launching the visible Windows setup. User approves UAC and installer steps. No silent-install switches, no bundled Npcap installer, no latest-version lookup. Redirects are disabled. Only this explicit dependency-install path can use HTTP; normal tracking and updates remain local.
- Installation cancellation, checksum mismatch, inaccessible package/data files, broken existing Npcap and a required reboot do not start tracking. A healthy installation does not trigger another download or install. The probe only enumerates adapters and does not capture packets.
- Added a portable friends package layout (main EXE plus Code) with an empty data directory, matching source archive and preserved licenses. Existing personal data and the local top-level Code/shortcut layout remain intact.
- Added read-only diagnostic modes --check-startup (JSON, no installation or capture) and --startup-check-only (prerequisite UI only). Added installer/driver failure simulations and a positive check of the original signed reference download without executing it.

## Local version 1.0.4

- Added native Windows edge/corner resizing, caption movement and double-click maximization through WPF WindowChrome. Added minimize, maximize and restore controls and a taskbar entry while retaining the translucent tracker.
- Save completed move/resize gestures, normal restore bounds and maximized state. Polling preserves active gestures and minimized windows. The hide shortcut restores a minimized tracker.
- Keep the title and tabs visible while the contents scroll. Added Reset window size in Overlay settings. Fixed-position and click-through modes still disable manual resizing.
- Added native HWND hit tests, work-area/taskbar maximize checks, size persistence/restart checks and settings compatibility checks. Prepared source-only GitHub export with private data and local equipment excluded.
