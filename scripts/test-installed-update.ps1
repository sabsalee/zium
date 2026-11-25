$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$temp = Join-Path ([IO.Path]::GetTempPath()) ('zium-install-check-' + [Guid]::NewGuid().ToString('N'))
$oldFlag = $env:ZIUM_INSTALLER_CHECKS
Push-Location $root
try {
    $env:ZIUM_INSTALLER_CHECKS = '1'
    $baseline = '0.0.1'
    [xml]$project = Get-Content src/Zium.Windows/Zium.Windows.csproj
    $current = [string]$project.Project.PropertyGroup.Version
    $feed = (Resolve-Path artifacts/installer).Path
    $baselineFiles = Join-Path $temp 'baseline'
    New-Item -ItemType Directory -Force $baselineFiles | Out-Null
    Copy-Item 'artifacts/publish/*' $baselineFiles -Recurse
    Set-Content (Join-Path $baselineFiles 'baseline-only.marker') 'baseline'
    $setupDirectory = Join-Path $temp 'setup'
    & ./scripts/package-windows.ps1 -ReusePublish -PublishDirectory $baselineFiles -OutputDirectory $setupDirectory -Version $baseline

    $install = Join-Path $temp 'installed'
    $setup = Start-Process (Join-Path $setupDirectory 'Zium-Setup.exe') -ArgumentList @('--silent', '--installto', "`"$install`"") -PassThru
    if (-not $setup.WaitForExit(120000)) {
        $setup.Kill()
        throw 'The test installation timed out.'
    }
    if ($setup.ExitCode -ne 0) { throw "Test install returned $($setup.ExitCode)." }

    $report = Join-Path $temp 'result.json'
    $exe = Join-Path $install 'current/Zium.exe'
    Start-Process $exe -ArgumentList @('--check-installed-update', "`"$feed`"", "`"$report`"", $baseline, $current)
    $deadline = [DateTime]::UtcNow.AddSeconds(150)
    do {
        if (Test-Path $report) {
            $result = Get-Content $report -Raw | ConvertFrom-Json
            if ($result.status -ne 'passed' -or $result.version -ne $current) { throw "Installed update failed: $($result.error)" }
            Write-Host "Installed $baseline -> $current, restarted, and read local data and the DPAPI-protected credential."
            return
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw 'No result from the installed update and restart check.'
} finally {
    $env:ZIUM_INSTALLER_CHECKS = $oldFlag
    if (Test-Path $temp) { Remove-Item $temp -Recurse -Force -ErrorAction SilentlyContinue }
    Pop-Location
}
