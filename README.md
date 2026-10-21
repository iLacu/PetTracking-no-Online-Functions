# A2 Pet Tracking

Custom local Windows version **1.0.4**, based on **PetTracker 0.1.51**  
Upstream commit: `a9d3cbf1faf48fe68dc463a4ae5a640648678831`

A local AION-2 pet tracker with collection tracking and merchant offers. Collection data, settings, and backups stay on your computer.

The official updater, Velopack, publisher keys, and GitHub automation have been removed. There are no update checks, browser launches, developer account requirements, remote access features, or cloud synchronization. The only download is the official Npcap installer when needed or explicitly requested.

## Getting Started

1. Extract the **entire ZIP**.
2. Open `A2Pet Tracking.lnk` in the top-level folder, or run `Code\Starten.cmd`.
3. For the first live synchronization, start the tracker **before** your AION-2 character.

No separate .NET installation is required: the runtime and image assets are bundled.

Startup checks verify application SHA-256 checksums, the data folder, Npcap, and network adapters. If Npcap is missing, the application downloads [Npcap 1.89](https://npcap.com/dist/npcap-1.89.exe), verifies its stored SHA-256 checksum, and opens the visible installer. Confirm the administrator prompt and complete setup.

Npcap is never installed silently or bundled for redistribution. Working installations are left unchanged. Cancellation, missing files, or a required restart prevents startup.

## Controls and Window

- **Close:** The X exits the entire application, including packet capture. The system tray paw icon also provides Settings and Exit.
- **Hide/show:** Configure the shortcut under **Settings > Hide Shortcut**; default: **Alt + D**. Click the shortcut button and press a combination, or Escape to cancel. Conflicts are displayed.
- **Resize:** Drag any edge or corner. Standard title-bar controls and double-click maximize/restore are supported.
- **Remembered layout:** Window position, size, and maximized state are saved. Long lists scroll while the title and tabs remain accessible.
- **Reset:** **Settings > Overlay > Reset window size** restores automatic height. **Fixed position** and **Click-through** prevent manual resizing.

## Merchant

The **Merchant** tab beside Nearby displays **Special** and **Pets** offers, your collection rank, and Kina prices.

Open both shop tabs in the game to capture offers and their exact refresh time. The countdown uses only that captured in-game time. Expired offers are discarded, as are offers after connection changes or ambiguous shop changes.

The tracker never queries the shop itself. Reopen the shop in the game after each refresh.

## Folder Layout

The top-level folder contains `Code` and `A2Pet Tracking.lnk`. Technical folders are inside `Code`:

| Folder | Contents |
|--------|----------|
| `Anwendung` | Compiled application and bundled .NET runtime |
| `Daten` | Collection, settings, and local backups |
| `Quellcode` | Editable source and local Git history |
| `Werkzeuge` | Portable SDK, local NuGet cache, and build files |
| `Pruefung` | Synthetic test data and previews |
| `Sicherungen` | Previous application versions |

## Building and Sharing

Close the application, edit the source, then run `Build-Local.ps1`, or use `Code\Neu bauen.cmd` in an existing installation.

- Set the custom version in `Directory.Build.props`.
- Builds require **.NET SDK 10.0.402** and the packages listed in the project files.
- Core and UI tests run by default.
- Builds use only the local package cache, back up the existing application, and preserve user data.
- New dependencies require deliberate review before entering the cache. Rebuilds work offline with the existing cache.

`tools\Pack-Friends.ps1` creates a shareable ZIP containing the EXE, `Code`, an empty data folder, license notices, and corresponding source code—without personal data, the SDK, or compiler.

GitHub is used only for source control; the application never accesses the repository or publishes automatically. For fresh clones, see `docs\BUILDING.md` and `docs\GITHUB.md`.

## Privacy and Limitations

Game packets are read locally through Npcap and associated with configured AION-2 processes. **No game packets are sent or modified.**

Collection data and settings are local JSON files. Diagnostics retain sanitized counters, not raw packet excerpts. Nothing is uploaded automatically.

The game operator's rules still apply. Game patches may require decoder updates, and simulations do not replace live in-game testing.
