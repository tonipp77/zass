<#
.SYNOPSIS
    Produces the Zass Windows distribution artifacts.

.DESCRIPTION
    Publishes Zass self-contained for win-x64 (no .NET runtime required on the
    target machine) and lays out two deliverables under .\dist:

      dist\app\                         folder build, the Inno Setup install source
      dist\portable\Zass.exe            single-file portable build
      dist\Zass-portable-<ver>-<rid>.exe  versioned copy of the portable build
      dist\Zass-portable-win-x64.exe     stable release asset name

    If the Inno Setup compiler (ISCC.exe) is available it also compiles the
    installer into dist\Zass-Setup-<ver>-win-x64.exe and copies it to
    dist\Zass-Setup-win-x64.exe; otherwise that step is skipped with a warning.

    Unsigned builds may trigger a SmartScreen warning on first run.

.EXAMPLE
    pwsh -File build\publish.ps1
    pwsh -File build\publish.ps1 -Version 2.0.1
#>
[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$Rid = "win-x64",
    [string]$Version
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root "Zass.App\Zass.App.csproj"
$iss = Join-Path $root "installer\Zass.iss"
$dist = Join-Path $root "dist"
$appOut = Join-Path $dist "app"
$portableOut = Join-Path $dist "portable"

[xml]$proj = Get-Content $project
$projectVersion = ($proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1)
if (-not $projectVersion) { throw "Zass.App.csproj must declare a Version." }
if ($Version -and $Version -ne $projectVersion) {
    throw "Requested version $Version differs from Zass.App.csproj version $projectVersion."
}
$Version = $projectVersion

Write-Host "Publishing Zass $Version ($Configuration, $Rid, self-contained)..." -ForegroundColor Cyan

$rootFull = [System.IO.Path]::GetFullPath($root)
$distFull = [System.IO.Path]::GetFullPath($dist)
if ($distFull -ne [System.IO.Path]::Combine($rootFull, "dist")) {
    throw "Refusing to clean an output directory outside the project root: $distFull"
}
if (Test-Path -LiteralPath $distFull) {
    if ((Get-Item -LiteralPath $distFull).LinkType) {
        throw "Refusing to clean a linked output directory: $distFull"
    }
    Remove-Item -LiteralPath $distFull -Recurse -Force
}

# 1) Folder build - the install source (kept as loose files so WPF native libs and
#    the localization satellites stay on disk; no .pdb in the distributed output).
dotnet publish $project -c $Configuration -r $Rid --self-contained true `
    -p:Version=$Version -p:PublishSingleFile=false -p:DebugType=none -p:DebugSymbols=false `
    -o $appOut --nologo
if ($LASTEXITCODE -ne 0) { throw "Folder publish failed." }

# 2) Single-file portable - one self-extracting Zass.exe (native libs embedded).
dotnet publish $project -c $Configuration -r $Rid --self-contained true `
    -p:Version=$Version -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=none -p:DebugSymbols=false `
    -o $portableOut --nologo
if ($LASTEXITCODE -ne 0) { throw "Portable publish failed." }

$portableExe = Join-Path $portableOut "Zass.exe"
$portableDist = Join-Path $dist "Zass-portable-$Version-$Rid.exe"
Copy-Item $portableExe $portableDist -Force
Copy-Item $portableExe (Join-Path $dist "Zass-portable-$Rid.exe") -Force
Write-Host "Portable build: $portableDist" -ForegroundColor Green

# 3) Installer (optional - requires Inno Setup 6.3+).
$iscc = Get-Command ISCC.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    foreach ($candidate in @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe")) {
        if (Test-Path $candidate) { $iscc = $candidate; break }
    }
}

if ($iscc) {
    $isccPath = if ($iscc -is [System.Management.Automation.CommandInfo]) { $iscc.Source } else { $iscc }
    & $isccPath "/DAppVersion=$Version" $iss
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }
    $setupExe = Join-Path $dist "Zass-Setup-$Version-win-x64.exe"
    Copy-Item $setupExe (Join-Path $dist "Zass-Setup-win-x64.exe") -Force
    Write-Host "Installer: $setupExe" -ForegroundColor Green
}
else {
    Write-Warning "Inno Setup (ISCC.exe) not found - skipped the installer. Install Inno Setup 6.3+ and re-run, or distribute the portable build from the dist folder."
}

Write-Host "Done." -ForegroundColor Cyan
