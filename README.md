A2 Pet Tracking
Custom local Windows version 1.0.4, based on PetTracker 0.1.51, commit a9d3cbf1faf48fe68dc463a4ae5a640648678831.

The official updater, including Velopack, publisher keys, and GitHub automation, has been removed. No update checks, browser launches, developer account, or remote access functionality. The only download exception is the specified official Npcap installer, used if the startup check detects that Npcap is missing or the user explicitly opens the Npcap setup. Offers, collection data, and settings remain local.

Getting Started
Open the A2Pet Tracking.lnk shortcut in the top-level project folder (alternatively, run Code\Starten.cmd). Clicking the X in the tracker's main window closes the entire application and the capture process. The paw icon in the system tray also provides access to settings and an exit option. For the first live synchronization, start the tracker first, then your AION-2 character.

The startup check verifies the application files against their SHA-256 checksums, checks the data folder, and checks the Npcap driver and network adapters. .NET and image assets are already bundled in the EXE files. Npcap must be installed as a Windows driver. If it is missing, version 1.89 is downloaded directly from https://npcap.com/dist/npcap-1.89.exe, verified against the stored SHA-256 checksum, and launched in a visible setup window. Confirm the administrator prompt and complete the setup steps. No silent installation and no redistribution of the Npcap installer. A working Npcap installation is not reinstalled. Cancellation, missing files, or a required restart will prevent the tracker from starting.

To share the application with friends, tools\Pack-Friends.ps1 creates a ZIP with the EXE at the top level and Code alongside it. Always extract the entire ZIP. The package includes an empty data folder, license notices, and the corresponding source code; it contains no personal user data or SDK. No separate .NET installation is required to use it. Building it yourself requires .NET SDK 10.0.402 and the packages listed in the project files; the standard compiler is not included in the compact distribution package.

Folders
All technical folders are located under Code. Only Code and A2Pet Tracking.lnk remain in the top-level folder.

Anwendung = locally compiled EXE with the .NET runtime.
Daten = actual collection data, settings, and local backups.
Quellcode = editable source code with its own local Git history.
Werkzeuge = portable SDK, local NuGet package cache, and build files.
Pruefung = synthetic test data and previews.
Sicherungen = previous custom application versions.
Custom Updates
Close the application, modify the source code, and run Build-Local.ps1. The custom version number is defined in Directory.Build.props. Core and UI tests run by default. The build uses only the local package cache and backs up any existing application version. User data is not replaced. Code\Neu bauen.cmd starts this process within the existing local installation.

New third-party libraries are added to the local package cache only after deliberate review. With the existing cache, rebuilding requires no internet connection. A personal GitHub repository is used solely for source code management: the application does not access it or publish anything automatically. For a fresh clone, see docs\BUILDING.md and docs\GITHUB.md.

Window Size
The main window can be resized from any edge or corner. The title bar supports dragging, double-clicking to maximize or restore, and minimizing or maximizing using the buttons. The window's position, size, and maximized state are saved. Long pet lists scroll within the window, keeping the title and tabs accessible. Settings > Overlay > Reset window size restores automatic height adjustment. Fixed position and Click-through continue to prevent manual resizing.

Hide Shortcut and Merchant
Settings > Hide Shortcut displays the configurable shortcut button, which defaults to Alt + D. Click it, then press the desired key combination; Escape cancels. The shortcut shows or hides the tracker window. Conflicts with other applications are displayed there. Clicking the X still closes the entire application.

Merchant, next to Nearby, displays Special and Pets with the most recently observed offers, your collection rank, and the Kina price. Open both tabs once in the game so the local decoder can read the offers and the exact refresh time. The countdown uses only this in-game time. When the countdown expires, the connection changes, or an ambiguous shop change occurs, old offers are discarded. The tracker does not query the shop itself; open it again in the game after a refresh.

Privacy and Limitations
Game packets are read locally through Npcap and associated with the configured AION-2 processes. No game packets are sent or modified. Collection data and settings are stored in local JSON files. Diagnostic reports store sanitized counters; raw packet excerpts are not retained. No automatic uploads or cloud synchronization.

Using a custom version does not change the game operator's rules. The decoder may need adjustments after game patches. A simulation is not a substitute for live testing in the game.
