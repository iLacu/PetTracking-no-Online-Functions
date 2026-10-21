# A2 Pet Tracking

Local Windows version **1.0.5**, based on PetTracker 0.1.51.

## Start

Extract the **entire ZIP** and open **A2Pet Tracking.exe**. Keep the **Code** folder beside it. An existing local installation can use **A2Pet Tracking.lnk**.

Requires Windows x64 and Npcap; .NET and images are bundled. Startup checks help if Npcap is missing. For the first collection sync, start the tracker before logging into your AION-2 character.

## Features

- Pet ranks, soul progress, recent activity and nearby mob observations.
- **Merchant reminder:** At each full hour of your local system clock, the Merchant tab flashes yellow five times and shows a notice for 30 seconds. Disable it under **Settings → Tracking → Black Cloud Merchant**. This reminder does not query shop stock.
- **Languages:** System language by default, with manual selection under **Settings → Language**. Includes German, English, Spanish, French, Italian and Polish.
- Resizable window with saved position and size. **Alt + D** hides or shows it; **X** exits the application.

Collection and settings stay local. No online updater or automatic data uploads. The only download exception is the official Npcap installer when needed.

## Local builds

Edit the source and run **Code/Neu bauen.cmd**. Existing user data is preserved. See [build instructions](docs/BUILDING.md).

## License

Original code by 0Fox2Give and contributors: GPL-3.0-only. See [LICENSE](LICENSE), [NOTICE](NOTICE) and [third-party notices](THIRD-PARTY-NOTICES.txt).
