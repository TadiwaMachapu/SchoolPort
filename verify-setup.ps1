# School Portal API - Setup Verification Script
# Run this script to verify your setup is complete

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "School Portal API - Setup Verification" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$ErrorCount = 0

# Check .NET SDK
Write-Host "[1/6] Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "  ✓ .NET SDK found: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "  ✗ .NET SDK not found. Please install .NET 8 SDK" -ForegroundColor Red
    $ErrorCount++
}

# Check project files
Write-Host "[2/6] Checking project files..." -ForegroundColor Yellow
$projectFiles = @(
    "SchoolPortal.Server\SchoolPortal.Server.csproj",
    "SchoolPortal.Data\SchoolPortal.Data.csproj",
    "SchoolPortal.Shared\SchoolPortal.Shared.csproj",
    "SchoolPortal.Tests\SchoolPortal.Tests.csproj"
)

foreach ($file in $projectFiles) {
    if (Test-Path $file) {
        Write-Host "  ✓ Found: $file" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Missing: $file" -ForegroundColor Red
        $ErrorCount++
    }
}

# Check documentation
Write-Host "[3/6] Checking documentation..." -ForegroundColor Yellow
$docFiles = @(
    "README.md",
    "QUICKSTART.md",
    "API_ENDPOINTS.md",
    "DELIVERY_SUMMARY.md",
    "DatabaseSetup.sql",
    "SchoolPortal.postman_collection.json"
)

foreach ($file in $docFiles) {
    if (Test-Path $file) {
        Write-Host "  ✓ Found: $file" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Missing: $file" -ForegroundColor Red
        $ErrorCount++
    }
}

# Check key source files
Write-Host "[4/6] Checking key source files..." -ForegroundColor Yellow
$sourceFiles = @(
    "SchoolPortal.Server\Program.cs",
    "SchoolPortal.Server\Controllers\AuthController.cs",
    "SchoolPortal.Server\Services\AuthService.cs",
    "SchoolPortal.Data\SchoolPortalDbContext.cs",
    "SchoolPortal.Data\Entities\User.cs"
)

foreach ($file in $sourceFiles) {
    if (Test-Path $file) {
        Write-Host "  ✓ Found: $file" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Missing: $file" -ForegroundColor Red
        $ErrorCount++
    }
}

# Try to restore packages
Write-Host "[5/6] Restoring NuGet packages..." -ForegroundColor Yellow
try {
    $restoreOutput = dotnet restore 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Packages restored successfully" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Package restore failed" -ForegroundColor Red
        Write-Host "    $restoreOutput" -ForegroundColor DarkGray
        $ErrorCount++
    }
} catch {
    Write-Host "  ✗ Failed to restore packages: $_" -ForegroundColor Red
    $ErrorCount++
}

# Try to build the solution
Write-Host "[6/6] Building solution..." -ForegroundColor Yellow
try {
    $buildOutput = dotnet build --no-restore 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Solution built successfully" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Build failed. Check the output above." -ForegroundColor Red
        $ErrorCount++
    }
} catch {
    Write-Host "  ✗ Build failed: $_" -ForegroundColor Red
    $ErrorCount++
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan

if ($ErrorCount -eq 0) {
    Write-Host "✓ All checks passed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next Steps:" -ForegroundColor Cyan
    Write-Host "1. Run DatabaseSetup.sql in SQL Server Management Studio" -ForegroundColor White
    Write-Host "2. Update connection string in appsettings.json" -ForegroundColor White
    Write-Host "3. Run: cd SchoolPortal.Server && dotnet run" -ForegroundColor White
    Write-Host "4. Open: https://localhost:7071/swagger" -ForegroundColor White
    Write-Host ""
    Write-Host "Default Login:" -ForegroundColor Cyan
    Write-Host "  Email: admin@demo.schoolportal.com" -ForegroundColor White
    Write-Host "  Password: Admin@123" -ForegroundColor White
} else {
    Write-Host "✗ $ErrorCount error(s) found. Please fix them before proceeding." -ForegroundColor Red
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
