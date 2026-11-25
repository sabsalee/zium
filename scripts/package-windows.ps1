param(
    [string]$PublishDirectory = 'artifacts/publish',
    [string]$OutputDirectory = 'artifacts/installer',
    [string]$Version,
    [switch]$ReusePublish
)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (-not $Version) {
        [xml]$project = Get-Content src/Zium.Windows/Zium.Windows.csproj
        $Version = [string]$project.Project.PropertyGroup.Version
    }
    if (-not $ReusePublish) {
        dotnet publish src/Zium.Windows/Zium.Windows.csproj -c Release -r win-x64 --self-contained true -o $PublishDirectory
        if ($LASTEXITCODE -ne 0) { throw 'Zium Windows publish failed.' }
        $licenseTarget = Join-Path $PublishDirectory 'licenses'
        New-Item -ItemType Directory -Force $licenseTarget | Out-Null
        Copy-Item licenses/*.txt $licenseTarget
    }

    dotnet tool run vpk -- --skip-updates pack --packId ZiumDesktop --packTitle Zium --packAuthors sabsalee --packVersion $Version --packDir $PublishDirectory --mainExe Zium.exe --runtime win-x64 --channel win-x64 --icon src/Zium.Windows/zium.ico --shortcuts StartMenuRoot --delta None --noPortable --outputDir $OutputDirectory
    if ($LASTEXITCODE -ne 0) { throw 'Zium installer packaging failed.' }

    $setup = Get-ChildItem $OutputDirectory -Filter 'ZiumDesktop-*-Setup.exe' | Select-Object -First 1
    $feed = Join-Path $OutputDirectory 'releases.win-x64.json'
    $package = Get-ChildItem $OutputDirectory -Filter "ZiumDesktop-$Version-*-full.nupkg" | Select-Object -First 1
    if (-not $setup -or -not (Test-Path $feed) -or -not $package) {
        throw "Expected installer, feed and full update package are missing for $Version."
    }
    $setupPath = Join-Path $OutputDirectory 'Zium-Setup.exe'
    Move-Item $setup.FullName $setupPath -Force
    @($setupPath, $feed, $package.FullName) | ForEach-Object {
        $hash = Get-FileHash $_ -Algorithm SHA256
        '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), [IO.Path]::GetFileName($_)
    } | Set-Content (Join-Path $OutputDirectory 'SHA256SUMS') -Encoding utf8NoBOM
} finally {
    Pop-Location
}
