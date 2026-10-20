param([string]$ProjectRoot, [string]$Runtime)
$ErrorActionPreference='Stop'
if (!$ProjectRoot) { $ProjectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent }
$root=[IO.Path]::GetFullPath($ProjectRoot)
$app=if ($Runtime) { [IO.Path]::GetFullPath($Runtime) } else { Join-Path $root 'Anwendung' }
$results=Join-Path $root 'Pruefung\Abschluss'
New-Item -ItemType Directory -Force -Path $results | Out-Null
$preview=Join-Path $results ('Vorschau-'+(Get-Date -Format 'yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,6))
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE='false'
function Run-Checked([string]$file,[string[]]$arguments) {
    $process=Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -PassThru
    if (!$process.WaitForExit(45000)) { $process.Kill(); throw ('Testprozess hat nicht beendet: '+$file) }
    if ($process.ExitCode -ne 0) { throw ('Testprozess fehlgeschlagen: '+$file+' Code '+$process.ExitCode) }
}
$npcap=Join-Path $results 'npcap-check.json'
Run-Checked (Join-Path $app 'A2PetTracking.Capture.exe') @('--check-npcap',('"'+$npcap+'"'))
Get-Content -LiteralPath $npcap
Run-Checked (Join-Path $app 'A2Pet Tracking.exe') @('--render',('"'+$preview+'"'))
$required=@('tracker.png','settings-tracking.png','settings-overlay.png','settings-account.png','settings-updates.png','settings-help.png','startup-ready.png','startup-npcap-missing.png')
foreach ($file in $required) { if (!(Test-Path -LiteralPath (Join-Path $preview $file))) { throw ('Vorschau fehlt: '+$file) } }
$deps=Get-Content -LiteralPath (Join-Path $app 'Abhaengigkeiten.json') -Raw | ConvertFrom-Json
if ($deps -match 'Velopack|PetTracker.Updates') { throw 'Updater-Abhaengigkeit gefunden' }
$sources=Get-ChildItem -LiteralPath (Join-Path $root 'Quellcode\src\App'),(Join-Path $root 'Quellcode\src\Core'),(Join-Path $root 'Quellcode\src\Worker') -Recurse -File -Filter *.cs | Where-Object FullName -notmatch '\\(obj|bin)\\'
$installer=Join-Path $root 'Quellcode\src\App\NpcapSetup.cs'
$bad=$sources | Where-Object FullName -ne $installer | Select-String -Pattern 'HttpClient|WebClient|HttpWebRequest|TcpClient|UdpClient|TcpListener|WebSocket|SendPacket|ProcessStartInfo\(url|ProcessStartInfo\("https?://'
if ($bad) { $bad; throw 'Netzwerkaktive Anwendungslogik gefunden' }
$installerText=Get-Content -LiteralPath $installer -Raw
if (!$installerText.Contains('https://npcap.com/dist/npcap-1.89.exe') -or !$installerText.Contains('AllowAutoRedirect = false') -or !$installerText.Contains('ExpectedSHA256')) { throw 'Npcap-Download nicht auf die festgelegte offizielle Datei begrenzt' }
$startup=Join-Path $results 'startpruefung.json'
Run-Checked (Join-Path $app 'A2Pet Tracking.exe') @('--check-startup',('"'+$startup+'"'),'--data',('"'+(Join-Path $preview 'checkup-data')+'"'))
if (!(Get-Content -LiteralPath $startup -Raw | ConvertFrom-Json).CanStart) { throw 'Startpruefung fehlgeschlagen' }
$manifest=Get-Content -LiteralPath (Join-Path $app 'dateipruefsummen.json') -Raw | ConvertFrom-Json
$count=0
foreach ($entry in $manifest.PSObject.Properties) { if ((Get-FileHash -LiteralPath (Join-Path $app $entry.Name) -Algorithm SHA256).Hash -ne $entry.Value) { throw ('Datei geaendert: '+$entry.Name) }; $count++ }
Write-Output ('Bundled EXE render and startup check passed; '+$required.Count+' views; '+$count+' package hashes verified; no updater; the only network exception is the explicit Npcap dependency installer.')
[pscustomobject]@{CheckedUtc=[DateTime]::UtcNow.ToString('o');StandaloneRenderPassed=$true;StartupCheckPassed=$true;PreviewFolder=$preview;Views=$required.Count;PackageHashesVerified=$count;NoUpdaterDependency=$true;NetworkException='Official pinned Npcap installer, only when needed'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $results 'Verifikation.json') -Encoding UTF8
