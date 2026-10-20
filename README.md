# A2 Pet Tracking by Lacu

Eigene lokale Windows-Version 1.0.4, basierend auf PetTracker 0.1.51, Commit a9d3cbf1faf48fe68dc463a4ae5a640648678831.

Der offizielle Updater samt Velopack, Herausgeberschluesseln und GitHub-Automatisierung wurde entfernt. Keine Update-Abfrage, kein Browser-Start, kein Entwicklerkonto und keine Fernzugriffsfunktion. Einzige Download-Ausnahme ist der festgelegte offizielle Npcap-Installer, falls die Startpruefung fehlendes Npcap erkennt oder der Nutzer das Npcap-Setup gezielt oeffnet. Angebote, Sammlung und Einstellungen bleiben lokal.

## Starten

Im obersten Projektordner die Verknuepfung A2Pet Tracking.lnk oeffnen (alternativ Code/Starten.cmd). Das X im Tracker-Hauptfenster beendet die gesamte Anwendung und den Capture-Prozess. Das Pfotensymbol im Tray bietet ebenfalls Einstellungen und Beenden. Fuer den ersten Live-Abgleich zuerst den Tracker, danach den AION-2-Charakter starten.

Die Startpruefung kontrolliert die Programmdateien anhand ihrer SHA-256-Pruefsummen, den Datenordner, den Npcap-Treiber und die Adapter. .NET und Bilder sind bereits in den EXE-Dateien enthalten. Npcap muss als Windows-Treiber installiert sein. Fehlt es, wird Version 1.89 direkt von https://npcap.com/dist/npcap-1.89.exe geladen, gegen die hinterlegte SHA-256-Pruefsumme geprueft und im sichtbaren Setup gestartet. Administratorabfrage und Setup-Schritte bestaetigen. Keine stille Installation, keine Weitergabe des Npcap-Installers. Funktionsfaehiges Npcap wird nicht erneut installiert. Abbruch, fehlende Dateien oder ein erforderlicher Neustart verhindern den Tracker-Start.

Fuer Freunde erzeugt tools/Pack-Friends.ps1 ein ZIP mit der EXE oben und Code daneben. Immer das gesamte ZIP entpacken. Das Paket enthaelt einen leeren Datenordner, Lizenzhinweise und den zugehoerigen Quellcode; keine eigenen Nutzerdaten und kein SDK. Zum Nutzen ist keine separate .NET-Installation erforderlich. Fuer eigene Builds benoetigt man das .NET SDK 10.0.402 und die in den Projektdateien aufgefuehrten Pakete; der Standardcompiler ist nicht Teil des kompakten Weitergabepakets.

## Ordner

Alle technischen Ordner liegen unter Code. Im obersten Ordner bleiben Code und A2Pet Tracking.lnk.

Anwendung = selbst kompilierte EXE mit .NET-Laufzeit. Daten = echte Sammlung, Einstellungen und lokale Backups. Quellcode = bearbeitbare Quellen mit eigenem lokalem Git-Verlauf. Werkzeuge = portables SDK, lokaler NuGet-Vorrat und Build-Dateien. Pruefung = kuenstliche Testdaten und Vorschauen. Sicherungen = vorherige eigene Programmversionen.

## Eigene Updates

Anwendung beenden, Quellcode anpassen und Build-Local.ps1 ausfuehren. Die eigene Versionsnummer steht in Directory.Build.props. Standardmaessig laufen Kern- und Oberflaechentests. Der Build nutzt ausschliesslich den lokalen Paketvorrat und sichert eine vorhandene Programmversion. Daten werden nicht ersetzt. Code/Neu bauen.cmd startet diesen Vorgang in der bestehenden lokalen Installation.

Neue Drittbibliotheken werden erst nach bewusster Pruefung zum lokalen Paketvorrat hinzugefuegt. Mit dem bestehenden Vorrat braucht ein Neubau keine Internetverbindung. Ein eigenes GitHub-Repository dient nur der Quellcodeverwaltung: Die Anwendung ruft es nicht ab und veroeffentlicht nichts automatisch. Fuer einen frischen Clone siehe docs/BUILDING.md und docs/GITHUB.md.

## Fenstergroesse

Das Hauptfenster laesst sich an allen Raendern und Ecken vergroessern oder verkleinern. Die Titelleiste unterstuetzt Verschieben, Doppelklick zum Maximieren/Wiederherstellen sowie Minimieren und Maximieren per Button. Die Fensterposition, Groesse und der maximierte Zustand werden gespeichert. Lange Pet-Listen scrollen innerhalb des Fensters; Titel und Reiter bleiben erreichbar. Einstellungen > Overlay > Reset window size stellt die automatische Hoehe wieder her. Fixed position und Click-through sperren weiterhin die manuelle Groessenaenderung.

## Hide Shortcut und Merchant

Einstellungen > Hide Shortcut zeigt den frei belegbaren Button, standardmaessig Alt + D. Anklicken, Kombination druecken; Escape bricht ab. Der Shortcut blendet das Trackerfenster ein oder aus. Konflikte mit anderen Programmen werden dort angezeigt. Das X beendet weiterhin die gesamte Anwendung.

Merchant neben Nearby zeigt Special und Pets mit zuletzt beobachteten Angeboten, eigenem Sammlungs-Rank und Kina-Preis. Beide Reiter einmal im Spiel oeffnen, damit der lokale Decoder Angebote und den exakten Refresh-Zeitpunkt lesen kann. Der Countdown nutzt ausschliesslich diese Spielzeit. Nach Ablauf, Verbindungswechsel oder einer unklaren Shop-Aenderung werden alte Angebote verworfen. Der Tracker fragt den Shop nicht selbst ab; nach einem Refresh im Spiel erneut oeffnen.

## Datenschutz und Grenzen

Spielpakete werden lokal ueber Npcap gelesen und den konfigurierten AION-2-Prozessen zugeordnet. Kein Senden oder Aendern von Spielpaketen. Sammlung und Einstellungen sind lokale JSON-Dateien. Diagnoseberichte speichern bereinigte Zaehler; rohe Paket-Ausschnitte werden nicht aufbewahrt. Keine automatische Uebertragung und keine Cloud-Synchronisation.

Eine eigene Version aendert nicht die Regeln des Spielbetreibers. Nach Spielpatches kann eine Anpassung des Decoders erforderlich sein. Eine Simulation ersetzt keinen Live-Test im Spiel.

## Lizenz

Der Originalcode von 0Fox2Give und Mitwirkenden bleibt GPL-3.0-only; siehe LICENSE und NOTICE. Bei Weitergabe sind die Lizenzpflichten sowie gesonderte Rechte an Drittbibliotheken, Grafiken und Spieldaten zu beachten. Details in THIRD-PARTY-NOTICES.txt, licenses und LEGAL.md.
