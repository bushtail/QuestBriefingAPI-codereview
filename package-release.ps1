#requires -Version 5.1
[CmdletBinding()]
param([string]$SPTPath = $env:SPT_PATH, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SPTPath)) {
    if (Test-Path -LiteralPath 'D:\SPT41Dev\EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll' -PathType Leaf) {
        $SPTPath = 'D:\SPT41Dev'
    } else {
        throw "SPT installation not found. Run .\package-release.ps1 -SPTPath 'C:\path\to\SPT' or set the SPT_PATH environment variable."
    }
}
if (-not (Test-Path -LiteralPath (Join-Path $SPTPath 'EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll') -PathType Leaf)) {
    throw "Game references were not found in '$SPTPath'. Pass -SPTPath pointing to your SPT installation root."
}
$SPTPath = (Resolve-Path -LiteralPath $SPTPath).Path
Write-Host "Building against SPT at $SPTPath"
& dotnet restore "$root\QuestBriefingAPI.csproj" --configfile "$root\NuGet.Config" "-p:SPTPath=$SPTPath" -v minimal
if ($LASTEXITCODE) { throw 'Plugin restore failed.' }
& dotnet build "$root\QuestBriefingAPI.csproj" --no-restore -c Release "-p:SPTPath=$SPTPath" -v minimal
if ($LASTEXITCODE) { throw 'Plugin build failed.' }
& dotnet run --project "$root\tests\QuestBriefingAPI.Tests.csproj" -c Release "-p:SPTPath=$SPTPath"
if ($LASTEXITCODE) { throw 'Briefing tests failed.' }
& "$root\tests\Test-GameCompatibility.ps1" -SPTPath $SPTPath
[xml]$project = Get-Content -LiteralPath "$root\QuestBriefingAPI.csproj" -Raw
$version = [string]$project.Project.PropertyGroup.Version
$dist = if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    Join-Path $root 'dist'
} else { [IO.Path]::GetFullPath($OutputDirectory) }
New-Item -ItemType Directory -Path $dist -Force | Out-Null
if ((Get-Item -LiteralPath $dist).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'dist cannot be a link.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Write-VerifiedZip([string]$Name, $Files) {
    $temporary = Join-Path $dist ([Guid]::NewGuid().ToString('N') + '.zip')
    $archive = [IO.Compression.ZipFile]::Open($temporary, 'Create')
    $hashes = @{}
    try {
        foreach ($entry in $Files) {
            $hashes[$entry.Name] = (Get-FileHash -LiteralPath $entry.Path -Algorithm SHA256).Hash
            [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $entry.Path, $entry.Name)
        }
    } finally { $archive.Dispose() }
    $archive = [IO.Compression.ZipFile]::OpenRead($temporary)
    try {
        foreach ($entry in $archive.Entries) {
            $stream = $entry.Open()
            $sha = [Security.Cryptography.SHA256]::Create()
            try { $actual = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '') }
            finally { $stream.Dispose(); $sha.Dispose() }
            if ($actual -ne $hashes[$entry.FullName]) { throw "ZIP hash mismatch: $($entry.FullName)" }
            $hashes.Remove($entry.FullName)
        }
        if ($hashes.Count) { throw 'ZIP is missing files.' }
    } finally { $archive.Dispose() }
    $destination = Join-Path $dist $Name
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        [IO.File]::Replace($temporary, $destination, [NullString]::Value)
    } else {
        Move-Item -LiteralPath $temporary -Destination $destination
    }
}

$dll = "$root\bin\Release\netstandard2.1\QuestBriefingAPI.dll"
$pluginFiles = @(
    @{ Path=$dll; Name='BepInEx/plugins/QuestBriefingAPI/QuestBriefingAPI.dll' },
    @{ Path="$root\bin\Release\netstandard2.1\QuestBriefingAPI.xml"; Name='BepInEx/plugins/QuestBriefingAPI/QuestBriefingAPI.xml' },
    @{ Path="$root\README.md"; Name='QuestBriefingAPI-README.md' },
    @{ Path="$root\LICENSE"; Name='QuestBriefingAPI-LICENSE.txt' }
)
# The sample manifest uses .json.example so discovery ignores it until an author enables it.
foreach ($file in (Get-ChildItem -LiteralPath "$root\examples" -File -Recurse)) {
    $relative = $file.FullName.Substring($root.Length + 1).Replace('\','/')
    $pluginFiles += @{ Path=$file.FullName; Name="BepInEx/plugins/QuestBriefingAPI/$relative" }
}
Write-VerifiedZip "QuestBriefingAPI-$version.zip" $pluginFiles
$sourceFiles = @()
foreach ($file in (Get-ChildItem -LiteralPath $root -File -Force)) {
    if ($file.Extension -in '.ps1','.sln' -or $file.Name -in @('QuestBriefingAPI.csproj','Directory.Build.props','NuGet.Config','.gitignore','README.md','LICENSE')) {
        $sourceFiles += @{ Path=$file.FullName; Name=$file.Name }
    }
}
foreach ($directory in @('QuestBriefingAPIClient','examples','tests')) {
    foreach ($file in (Get-ChildItem -LiteralPath "$root\$directory" -File -Recurse)) {
        $relative = $file.FullName.Substring($root.Length + 1).Replace('\','/')
        if ($relative -match '/(bin|obj)/') { continue }
        $sourceFiles += @{ Path=$file.FullName; Name=$relative }
    }
}
Write-VerifiedZip "QuestBriefingAPI-Source-$version.zip" $sourceFiles
# Retire the previous separate author kit only after the replacement archives are verified.
$oldAuthorKit = Join-Path $dist "QuestBriefingAPI-AuthorKit-$version.zip"
if (Test-Path -LiteralPath $oldAuthorKit -PathType Leaf) { Remove-Item -LiteralPath $oldAuthorKit }
@("QuestBriefingAPI-$version.zip", "QuestBriefingAPI-Source-$version.zip") | ForEach-Object {
    '{0}  {1}' -f (Get-FileHash -LiteralPath (Join-Path $dist $_) -Algorithm SHA256).Hash, $_
} | Set-Content -LiteralPath "$dist\SHA256SUMS.txt" -Encoding ascii
Write-Host "Release with bundled examples and standalone source ZIPs verified in $dist"
