# Build MSIX Package for Monitor Switcher
# Requires: Windows SDK (for makeappx.exe and signtool.exe)

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Monitor Switcher - MSIX Builder      " -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$AppName = "MonitorSwitcher"
$Version = "0.0.1.0"
$Publisher = "CN=Aditi Kraft"

# Step 1: Build the app
Write-Host "[1/5] Building application..." -ForegroundColor Yellow
dotnet publish -c Release -r win-x64 --self-contained true -o "msix-content" --nologo -v q

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Step 2: Copy assets to content folder
Write-Host "[2/5] Copying assets..." -ForegroundColor Yellow
Copy-Item "Assets\*.png" "msix-content\Assets\" -Force
Copy-Item "Package.appxmanifest" "msix-content\AppxManifest.xml" -Force

# Step 3: Find Windows SDK tools
Write-Host "[3/5] Locating Windows SDK..." -ForegroundColor Yellow
$sdkPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22000.0\x64",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64"
)

$sdkPath = $null
foreach ($path in $sdkPaths) {
    if (Test-Path "$path\makeappx.exe") {
        $sdkPath = $path
        break
    }
}

if (-not $sdkPath) {
    Write-Host ""
    Write-Host "Windows SDK not found!" -ForegroundColor Red
    Write-Host ""
    Write-Host "To create MSIX packages, you need to install Windows SDK:" -ForegroundColor Yellow
    Write-Host "  1. Open Visual Studio Installer" -ForegroundColor White
    Write-Host "  2. Modify your installation" -ForegroundColor White
    Write-Host "  3. Check 'Windows 10 SDK' or 'Windows 11 SDK'" -ForegroundColor White
    Write-Host ""
    Write-Host "Or download directly from:" -ForegroundColor Yellow
    Write-Host "  https://developer.microsoft.com/en-us/windows/downloads/windows-sdk/" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Alternative: Use the portable ZIP or Inno Setup installer instead." -ForegroundColor Gray
    exit 1
}

Write-Host "  Found SDK at: $sdkPath" -ForegroundColor Gray

# Step 4: Create unsigned MSIX
Write-Host "[4/5] Creating MSIX package..." -ForegroundColor Yellow
$msixPath = "installer\$AppName-$Version.msix"
New-Item -ItemType Directory -Force -Path "installer" | Out-Null

& "$sdkPath\makeappx.exe" pack /d "msix-content" /p $msixPath /o

if ($LASTEXITCODE -ne 0) {
    Write-Host "MSIX creation failed!" -ForegroundColor Red
    exit 1
}

# Step 5: Done
Write-Host "[5/5] Package created!" -ForegroundColor Green
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  MSIX Build Complete!                 " -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output: $msixPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "NOTE: The package is UNSIGNED." -ForegroundColor Yellow
Write-Host "To install, you need to either:" -ForegroundColor Yellow
Write-Host "  1. Enable Developer Mode in Windows Settings" -ForegroundColor White
Write-Host "  2. Sign with a certificate (for distribution)" -ForegroundColor White
Write-Host ""
Write-Host "To enable Developer Mode:" -ForegroundColor Cyan
Write-Host "  Settings > Privacy & security > For developers > Developer Mode: ON" -ForegroundColor White
Write-Host ""
