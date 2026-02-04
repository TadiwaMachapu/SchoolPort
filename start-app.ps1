# School Portal - Start Script
# This script starts both the backend API and frontend Blazor app

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  School Portal - Starting Application" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if .NET is available
try {
    $dotnetVersion = dotnet --version
    Write-Host "✓ .NET SDK Version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "✗ .NET SDK not found. Please install .NET 8 SDK." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Starting Backend API..." -ForegroundColor Yellow

# Start Backend API in a new window
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\SchoolPortal.Server'; Write-Host 'Starting Backend API...' -ForegroundColor Green; dotnet run"

Write-Host "✓ Backend API starting at https://localhost:7071" -ForegroundColor Green
Write-Host ""

# Wait a bit for backend to start
Write-Host "Waiting 5 seconds for backend to initialize..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

Write-Host ""
Write-Host "Starting Frontend Blazor App..." -ForegroundColor Yellow

# Start Frontend in a new window
Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PSScriptRoot\SchoolPortal.Client'; Write-Host 'Starting Frontend...' -ForegroundColor Green; dotnet run"

Write-Host "✓ Frontend starting at http://localhost:5000" -ForegroundColor Green
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Application Started Successfully!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Backend API: https://localhost:7071/swagger" -ForegroundColor White
Write-Host "Frontend:    http://localhost:5000" -ForegroundColor White
Write-Host ""
Write-Host "Demo Credentials:" -ForegroundColor Yellow
Write-Host "  Admin:   admin@demo.schoolportal.com / Admin@123" -ForegroundColor White
Write-Host "  Teacher: teacher@demo.schoolportal.com / Admin@123" -ForegroundColor White
Write-Host "  Student: student@demo.schoolportal.com / Admin@123" -ForegroundColor White
Write-Host ""
Write-Host "Press any key to exit this window..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
