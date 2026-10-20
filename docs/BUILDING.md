# Lokaler Build

Build-Local.ps1 nutzt das daneben gespeicherte portable .NET SDK 10.0.402 mit Laufzeit 10.0.13 und ausschliesslich die lokale Quelle Werkzeuge/NuGetFeed. Der Build erzeugt Windows-x64-Programme mit eigener Laufzeit. Es wird nichts systemweit installiert. SDK-Telemetrie und die automatische Erzeugung eines Entwicklungszertifikats sind abgeschaltet.

Aufruf aus Windows PowerShell: & '.\Build-Local.ps1'

Standardmaessig werden alle lokalen Kern- und WPF-Tests mit getrennten Testdaten ausgefuehrt. -SkipTests ueberspringt diese bewusst. Die vorherige Anwendung wird in Sicherungen abgelegt. Sammlung und Einstellungen im Nachbarordner Daten bleiben erhalten. Eigene Versionen werden in Directory.Build.props gepflegt.

Npcap ohne Mitschnitt pruefen: Anwendung/A2PetTracking.Capture.exe --check-npcap Pruefung/npcap-check.json

Seit 1.0.3 werden Hauptprogramm und Worker als komprimierte Single-File-EXEs mit eingebetteter Laufzeit gebaut. Icon und Portraits sind eingebettete Ressourcen. startpruefung.json enthaelt die SHA-256-Pruefsummen fuer den Startcheck; Abhaengigkeiten.json dokumentiert die im Bundle enthaltenen Bibliotheken. Eine separate .NET-Installation wird zur Ausfuehrung nicht benoetigt.

Vollstaendige Startpruefung ohne Erfassung und ohne Installation: "Anwendung/A2Pet Tracking.exe" --check-startup Pruefung/start.json --data Pruefung/Testdaten. Exit-Code 0 bedeutet bereit, 2 bedeutet eine fehlende Voraussetzung. Der normale Start bietet bei fehlendem Npcap den automatischen Download mit sichtbarem Setup; nur diese eine festgelegte offizielle Datei darf geladen werden.

Weitergabepaket nach sauberem lokalem Git-Commit: tools/Pack-Friends.ps1 -OutputDirectory <Zielordner>. Optional -Runtime <vorbereitetes Programmverzeichnis>. Es enthaelt keine Daten aus Daten, keine Build-Werkzeuge und keinen Npcap-Installer. Quellcode.zip enthaelt den zugehoerigen Git-Stand; zum Bauen werden SDK und Paketvorrat separat benoetigt.

Oberflaeche mit kuenstlichen Daten rendern: "Anwendung/A2Pet Tracking.exe" --render Pruefung/Vorschau

## Nach einem frischen GitHub-Clone

Das Repository enthaelt Quellen und Ressourcen, keine EXE, kein SDK und keinen NuGet-Vorrat. Empfohlene Anordnung: Code/Quellcode fuer den Clone, Code/Werkzeuge/dotnet fuer das entpackte Windows-x64-SDK 10.0.402 und Code/Werkzeuge/NuGetFeed fuer die .nupkg-Dateien. Den bereits lokal vorhandenen Werkzeugordner kann der Besitzer separat behalten und wiederverwenden.

SDK nur aus dem offiziellen .NET-Download von Microsoft beziehen. Fuer eine neue Entwicklungsumgebung werden die Pakete aus den eingecheckten Projekt- und packages.lock.json-Dateien sowie die Windows-x64-Runtime-/Host-Packs 10.0.13 benoetigt. docs/OFFLINE-PACKAGES.json listet den getesteten lokalen Vorrat samt SHA-256 auf. Die Pakete koennen separat aus der offiziellen NuGet-Quelle bezogen und anhand der Liste geprueft werden. NuGet.Config bleibt absichtlich auf ../Werkzeuge/NuGetFeed beschraenkt; es gibt keinen automatischen Download beim lokalen Neubau.

Danach Build-Local.ps1 aus Windows PowerShell ausfuehren. Ohne diese separat vorbereiteten Werkzeuge ist ein frischer Clone noch nicht baubar. Die fertige Anwendung und das Freunde-Paket benoetigen sie nicht.

src/App = Oberflaeche; src/Worker = passiver Mitschnitt und lokale Kommunikation; src/Core = Decoder, Fortschritt, Speicherung. src/Setup bleibt nur fuer sichere Entpacktests erhalten und wird nicht ausgeliefert. Der urspruengliche Updater wurde entfernt.
