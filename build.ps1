# build.ps1 — Build script for OpenLMStudio
# Usage:
#   .\build.ps1                          # Build for Windows x64 (default)
#   .\build.ps1 -RID win-arm64          # Build for Windows ARM64
#   .\build.ps1 -RID osx-arm64          # Build for macOS Apple Silicon
#   .\build.ps1 -RID linux-x64          # Build for Linux x64
#   .\build.ps1 -RID android.35-arm64-v8a  # Build for Android ARM64

param(
    [string]$RID = "",
    [string]$Configuration = "Publish",
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
$Project = "src/Desktop/OpenLMStudio.Desktop.csproj"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  OpenLMStudio Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Clean if requested
if ($Clean) {
    Write-Host "[Clean] Removing previous build artifacts..." -ForegroundColor Yellow
    Remove-Item -Path "publish" -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item -Path "OpenLMStudio.exe" -Force -ErrorAction SilentlyContinue
    Write-Host "[Clean] Done." -ForegroundColor Yellow
    Write-Host ""
}

# Determine the output directory
$rid = $RID
if (-not $rid) {
    $rid = "win-x64"
}
$outDir = "publish\$rid"

Write-Host "[Publish] Configuration: $Configuration" -ForegroundColor Green
Write-Host "[Publish] Runtime:        $rid" -ForegroundColor Green
Write-Host "[Publish] Output:         $outDir" -ForegroundColor Green
Write-Host ""

# Restore dependencies
Write-Host "[Restore] Restoring packages..." -ForegroundColor Cyan
dotnet restore $Project
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] dotnet restore failed." -ForegroundColor Red
    exit 1
}
Write-Host ""

# Publish
Write-Host "[Publish] Building..." -ForegroundColor Cyan
dotnet publish $Project -c $Configuration -r $rid -o $outDir /nowarn:AVLN3001
if ($LASTEXITCODE -ne 0) {
    Write-Host "[Error] dotnet publish failed." -ForegroundColor Red
    exit 1
}

# Copy the main EXE to root for default RID
if ($rid -eq "win-x64") {
    if (Test-Path "$outDir\OpenLMStudio.exe") {
        Copy-Item "$outDir\OpenLMStudio.exe" "OpenLMStudio.exe" -Force
        Write-Host ""
        Write-Host "[Done] OpenLMStudio.exe copied to project root." -ForegroundColor Green
    }
}

# Show output
Write-Host ""
Write-Host "[Done] Build complete!" -ForegroundColor Green
Write-Host "Output: $outDir" -ForegroundColor Cyan