param([Parameter(Mandatory=$true)][string]$OutputDirectory, [string]$Runtime)
$ErrorActionPreference = 'Stop'
$source = Split-Path $PSScriptRoot -Parent
$project = Split-Path $source -Parent
if (!$Runtime) { $Runtime = Join-Path $project 'Anwendung' }
$Runtime = [IO.Path]::GetFullPath($Runtime)
$output = [IO.Path]::GetFullPath($OutputDirectory)
[xml]$properties = Get-Content -LiteralPath (Join-Path $source 'Directory.Build.props')
$version = [string]$properties.Project.PropertyGroup.Version
$manifest = Get-Content -LiteralPath (Join-Path $Runtime 'startpruefung.json') -Raw | ConvertFrom-Json
if ($manifest.Version -ne $version -or !$manifest.BundledRuntime) { throw 'Das Programm wurde noch nicht als aktuelles Bundle gebaut.' }
if ((Get-FileHash -LiteralPath (Join-Path $Runtime 'A2Pet Tracking.exe') -Algorithm SHA256).Hash -ne $manifest.MainProgramSHA256) { throw 'Die Programmdatei stimmt nicht mit dem geprueften Build ueberein.' }
foreach ($entry in $manifest.Files.PSObject.Properties) {
    $path = [IO.Path]::GetFullPath((Join-Path $Runtime $entry.Name))
    if (!$path.StartsWith($Runtime + '\', [StringComparison]::OrdinalIgnoreCase) -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $entry.Value) { throw ('Ungueltige Paketdatei: ' + $entry.Name) }
}
if (@(git -C $source status --porcelain).Count) { throw 'Vor der Weitergabe zuerst den fertigen Quellcode lokal committen.' }
$stage = Join-Path $project ('Werkzeuge\Paket-Build\' + [guid]::NewGuid().ToString('N'))
$package = Join-Path $stage ('A2Pet Tracking ' + $version)
$program = Join-Path $package 'Code\Anwendung'
New-Item -ItemType Directory -Path $program -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $package 'Code\Daten') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $Runtime 'A2Pet Tracking.exe') -Destination (Join-Path $package 'A2Pet Tracking.exe')
foreach ($item in Get-ChildItem -LiteralPath $Runtime) {
    if ($item.Name -notin @('A2Pet Tracking.exe','dateipruefsummen.json')) { Copy-Item -LiteralPath $item.FullName -Destination (Join-Path $program $item.Name) -Recurse }
}
& git -C $source archive --format=zip ('--output=' + (Join-Path $package 'Code\Quellcode.zip')) HEAD
if ($LASTEXITCODE -ne 0) { throw 'Quellcode-Archiv fehlgeschlagen.' }
@'
A2Pet Tracking - portable Ausgabe fuer Freunde

Das gesamte ZIP in einen eigenen Ordner entpacken, zum Beispiel unter Dokumente.
Danach A2Pet Tracking.exe oeffnen. Die EXE allein reicht nicht: Code muss daneben liegen.
Windows 10/11 auf einem x64-PC wird benoetigt. .NET und Bilder sind enthalten.

Vor jedem Start werden Programmdateien, Datenordner, Npcap-Treiber und Adapter geprueft.
Fehlt Npcap, wird der festgelegte offizielle Installer von npcap.com heruntergeladen,
gegen seine SHA-256-Pruefsumme geprueft und als sichtbares Setup gestartet.
Administratorabfrage und Setup bestaetigen; WinPcap-Kompatibilitaet aktiviert lassen.
Gegebenenfalls Windows neu starten. Danach prueft der Tracker erneut.
Eine vorhandene funktionsfaehige Npcap-Installation wird nicht erneut installiert.
Internet wird nur fuer einen benoetigten Npcap-Download verwendet; kein Online-Updater.

Nach dem Start den AION-2-Charakter einloggen. Alt+D blendet das Fenster ein/aus.
Das X beendet die gesamte Anwendung. Eigene Daten entstehen unter Code\Daten.
Fenster an Raendern/Ecken anpassen oder ueber die Titelleiste maximieren/minimieren.
Groesse und Zustand werden gespeichert. Overlay > Reset window size setzt sie zurueck.
Special und Pets im Spiel oeffnen, um Merchant-Angebote zu beobachten.

Dieses Paket enthaelt keine Sammlung, Einstellungen oder Diagnosedaten des Erstellers.
Quellcode und Build-Skripte: Code\Quellcode.zip. Zum Neubauen ist das .NET SDK noetig;
die Anwendung selbst benoetigt keine separate .NET-Installation.
Lizenz und Rechtehinweise stehen unter Code\Anwendung\LICENSE, NOTICE und licenses.
Npcap wird separat vom Originalanbieter bezogen und nicht in diesem ZIP mitgeliefert.
'@ | Set-Content -LiteralPath (Join-Path $package 'Code\LIESMICH.txt') -Encoding UTF8
New-Item -ItemType Directory -Force -Path $output | Out-Null
$zip = Join-Path $output ('A2Pet-Tracking-' + $version + '-Freunde.zip')
if (Test-Path -LiteralPath $zip) { throw 'Das Zielpaket existiert bereits.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
$stream = [IO.File]::Open($zip, [IO.FileMode]::CreateNew)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create, $false)
try {
    # .NET Framework uses backslashes in CreateFromDirectory on Windows.
    # Explicit portable ZIP paths work in both Windows PowerShell and pwsh.
    foreach ($file in Get-ChildItem -LiteralPath $package -Recurse -File) {
        $entry = $file.FullName.Substring($package.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $entry, [IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
    $archive.CreateEntry('Code/Daten/') | Out-Null
} finally { $archive.Dispose(); $stream.Dispose() }
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
($hash + '  ' + (Split-Path $zip -Leaf)) | Set-Content -LiteralPath ($zip + '.sha256.txt') -Encoding ASCII
[ordered]@{Version=$version;Archive=$zip;SHA256=$hash;SourceRevision=(git -C $source rev-parse HEAD);ContainsPersonalData=$false;NpcapRedistributed=$false;BundledDotnet=$true;StagedFolder=$package} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output ('A2Pet-Tracking-' + $version + '-Paketinfo.json')) -Encoding UTF8
Write-Output ('Weitergabepaket: ' + $zip)
