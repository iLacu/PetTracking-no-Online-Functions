param([switch]$SkipTests, [switch]$PrepareOnly)
$ErrorActionPreference = 'Stop'
$source = $PSScriptRoot
[xml]$properties = Get-Content -LiteralPath (Join-Path $source 'Directory.Build.props')
$version = [string]$properties.Project.PropertyGroup.Version
$project = Split-Path $source -Parent
$tools = Join-Path $project 'Werkzeuge'
$dotnet = Join-Path $tools 'dotnet\dotnet.exe'
if (!(Test-Path -LiteralPath $dotnet)) { throw 'Das lokale .NET SDK fehlt im Ordner Werkzeuge.' }
$env:DOTNET_ROOT = Split-Path $dotnet -Parent
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_NOLOGO = '1'
$env:MSBUILDDISABLENODEREUSE = '1'
$env:DOTNET_CLI_USE_MSBUILD_SERVER = '0'
$env:NUGET_CERT_REVOCATION_MODE = 'offline'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = '1'
$env:DOTNET_CLI_HOME = Join-Path $tools 'cli-home'
$env:NUGET_PACKAGES = Join-Path $tools 'nuget-packages'
$app = [IO.Path]::GetFullPath((Join-Path $project 'Anwendung'))
if ($app -ne (Join-Path ([IO.Path]::GetFullPath($project)) 'Anwendung')) { throw 'Ungueltiger Programmordner.' }
foreach ($process in Get-Process -Name A2PetTracking,'A2Pet Tracking',A2PetTracking.Capture -ErrorAction SilentlyContinue | Where-Object { !$PrepareOnly }) {
    if ($process.Path -and $process.Path.StartsWith($app + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Bitte A2 Pet Tracking im Tray beenden, bevor du neu baust.' }
}
$stamp = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0,6)
$results = Join-Path $project ('Pruefung\' + $stamp)
if (!$SkipTests) { & (Join-Path $source 'tools\Test.ps1') -Dotnet $dotnet -Results $results }
$build = Join-Path $tools ('Build\' + $stamp)
$runtime = Join-Path $build 'Anwendung'
$worker = Join-Path $build 'Capture'
New-Item -ItemType Directory -Force -Path $build | Out-Null
& $dotnet publish (Join-Path $source 'src\App\App.csproj') -c Release -r win-x64 --self-contained true -o $runtime --nologo -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:EnableSingleFileAnalyzer=false -p:UseSharedCompilation=false -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build der Anwendung fehlgeschlagen.' }
& $dotnet publish (Join-Path $source 'src\Worker\Worker.csproj') -c Release -r win-x64 --self-contained true -o $worker --nologo -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:EnableSingleFileAnalyzer=false -p:UseSharedCompilation=false -m:1
if ($LASTEXITCODE -ne 0) { throw 'Build des Capture-Prozesses fehlgeschlagen.' }
foreach ($file in Get-ChildItem -LiteralPath $worker -File) {
    $destination = Join-Path $runtime $file.Name
    if (Test-Path -LiteralPath $destination) {
        if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath $destination).Hash -and $file.Name -notin @('Microsoft.VisualBasic.dll','System.Drawing.dll','WindowsBase.dll')) { throw ('Unterschiedliche gemeinsam genutzte Abhaengigkeit: ' + $file.Name) }
    } else { Copy-Item -LiteralPath $file.FullName -Destination $destination }
}
[IO.File]::Move((Join-Path $runtime 'A2PetTracking.exe'), (Join-Path $runtime 'A2Pet Tracking.exe'))
Copy-Item -LiteralPath (Join-Path $source 'assets\PetTracker.ico') -Destination (Join-Path $runtime 'PetTracker.ico')
$libraries = @()
foreach ($depsFile in @('src\App\bin\Release\net10.0-windows\win-x64\A2PetTracking.deps.json','src\Worker\bin\Release\net10.0\win-x64\A2PetTracking.Capture.deps.json')) {
    $deps = Get-Content -LiteralPath (Join-Path $source $depsFile) -Raw | ConvertFrom-Json
    $libraries += $deps.libraries.PSObject.Properties.Name
}
if ($libraries -match 'Velopack|PetTracker.Updates') { throw 'Unerwartete Updater-Abhaengigkeit im Bundle.' }
@($libraries | Sort-Object -Unique) | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runtime 'Abhaengigkeiten.json') -Encoding UTF8
foreach ($name in @('LICENSE','NOTICE','THIRD-PARTY-NOTICES.txt','LOCAL-CHANGES.md')) { Copy-Item -LiteralPath (Join-Path $source $name) -Destination (Join-Path $runtime $name) }
Copy-Item -LiteralPath (Join-Path $source 'licenses') -Destination (Join-Path $runtime 'licenses') -Recurse
if (Get-ChildItem -LiteralPath $runtime -Recurse -File | Where-Object Name -match '^(Velopack|Squirrel|PetTracker\.Updates).*\.(dll|exe)$|^Update\.exe$|^publisher\.json$') { throw 'Unerwartete Updater-Datei im Programm.' }
$startupFiles = [ordered]@{}
foreach ($file in Get-ChildItem -LiteralPath $runtime -File -Recurse | Where-Object Name -ne 'A2Pet Tracking.exe') { $startupFiles[$file.FullName.Substring($runtime.Length + 1)] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
[ordered]@{Format=1;Version=$version;BundledRuntime=$true;MainProgramSHA256=(Get-FileHash -LiteralPath (Join-Path $runtime 'A2Pet Tracking.exe') -Algorithm SHA256).Hash;Files=$startupFiles} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runtime 'startpruefung.json') -Encoding UTF8
$hashes = [ordered]@{}
foreach ($file in Get-ChildItem -LiteralPath $runtime -File -Recurse) { $hashes[$file.FullName.Substring($runtime.Length + 1)] = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash }
$hashes | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $runtime 'dateipruefsummen.json') -Encoding UTF8
if ($PrepareOnly) {
    [ordered]@{Version=$version;PreparedUtc=[DateTime]::UtcNow.ToString('o');Runtime=$runtime;TestsRun=(!$SkipTests);TestResults=$results;SelfContained=$true;LocalOnly=$true;SourceRevision=(git -C $source rev-parse HEAD)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $project 'Vorbereitetes-Update.json') -Encoding UTF8
    Write-Output ('Vorbereitet, laufende Anwendung unveraendert: ' + $runtime)
    return
}
if (Test-Path -LiteralPath $app) {
    $backup = Join-Path $project ('Sicherungen\Programm-' + $stamp)
    New-Item -ItemType Directory -Force -Path (Split-Path $backup -Parent) | Out-Null
    if (!(Split-Path $backup -Parent).StartsWith([IO.Path]::GetFullPath($project) + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Ungueltige Sicherung.' }
    [IO.Directory]::Move($app, $backup)
}
try {
    [IO.Directory]::Move($runtime, $app)
} catch {
    if ($backup -and (Test-Path -LiteralPath $backup) -and !(Test-Path -LiteralPath $app)) { [IO.Directory]::Move($backup, $app) }
    throw
}
New-Item -ItemType Directory -Force -Path (Join-Path $project 'Daten') | Out-Null
$receipt = [ordered]@{ Product='A2 Pet Tracking by Lacu'; Version=$version; BuildUtc=[DateTime]::UtcNow.ToString('o'); BaseCommit='a9d3cbf1faf48fe68dc463a4ae5a640648678831'; SDK='10.0.402'; Runtime='10.0.13'; DataStoredLocally=$true; NetworkException='Official pinned Npcap dependency installer when needed'; RemoteUpdater=$false; UpstreamRemote=$false; ExternalBrowserLinks=$false; RawPacketSamples=$false; SelfContained=$true; SingleFileExecutables=$true; TestsRun=(!$SkipTests); TestResults=$results; Program=$app; ProgramSHA256=(Get-FileHash -LiteralPath (Join-Path $app 'A2Pet Tracking.exe') -Algorithm SHA256).Hash }
$receipt | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $project 'Build-Bericht.json') -Encoding UTF8
Write-Output ('Fertig: ' + (Join-Path $app 'A2Pet Tracking.exe'))
