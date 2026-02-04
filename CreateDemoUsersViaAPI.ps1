# Create Demo Users via API
# Make sure the backend is running at https://localhost:7071 before running this script

$apiBaseUrl = "https://localhost:7071/api"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  Creating Demo Users via API" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Function to create user
function Create-User {
    param (
        [string]$Email,
        [string]$Password,
        [string]$FirstName,
        [string]$LastName,
        [string]$Role,
        [string]$Token
    )
    
    $body = @{
        email = $Email
        password = $Password
        firstName = $FirstName
        lastName = $LastName
        role = $Role
    } | ConvertTo-Json
    
    try {
        $headers = @{
            "Content-Type" = "application/json"
            "Authorization" = "Bearer $Token"
        }
        
        $response = Invoke-RestMethod -Uri "$apiBaseUrl/users" -Method Post -Body $body -Headers $headers -SkipCertificateCheck
        Write-Host "✓ Created $Role user: $Email" -ForegroundColor Green
        return $true
    }
    catch {
        Write-Host "✗ Failed to create $Role user: $($_.Exception.Message)" -ForegroundColor Red
        return $false
    }
}

Write-Host "Step 1: First, you need to create an Admin user manually in the database" -ForegroundColor Yellow
Write-Host "        OR if you already have an admin user, login to get a token." -ForegroundColor Yellow
Write-Host ""
Write-Host "Do you have an existing admin user? (Y/N): " -NoNewline
$hasAdmin = Read-Host

if ($hasAdmin -eq "Y" -or $hasAdmin -eq "y") {
    Write-Host ""
    Write-Host "Please enter admin credentials:" -ForegroundColor Cyan
    $adminEmail = Read-Host "Admin Email"
    $adminPassword = Read-Host "Admin Password" -AsSecureString
    $adminPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($adminPassword))
    
    # Login to get token
    $loginBody = @{
        email = $adminEmail
        password = $adminPasswordPlain
    } | ConvertTo-Json
    
    try {
        Write-Host "Logging in..." -ForegroundColor Yellow
        $loginResponse = Invoke-RestMethod -Uri "$apiBaseUrl/auth/login" -Method Post -Body $loginBody -ContentType "application/json" -SkipCertificateCheck
        $token = $loginResponse.accessToken
        Write-Host "✓ Login successful!" -ForegroundColor Green
        Write-Host ""
        
        # Create demo users
        Write-Host "Creating demo users..." -ForegroundColor Cyan
        Create-User -Email "teacher@demo.schoolportal.com" -Password "Admin@123" -FirstName "Teacher" -LastName "Demo" -Role "Teacher" -Token $token
        Create-User -Email "student@demo.schoolportal.com" -Password "Admin@123" -FirstName "Student" -LastName "Demo" -Role "Student" -Token $token
        
        Write-Host ""
        Write-Host "========================================" -ForegroundColor Cyan
        Write-Host "  Demo Users Created Successfully!" -ForegroundColor Green
        Write-Host "========================================" -ForegroundColor Cyan
    }
    catch {
        Write-Host "✗ Login failed: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Please check your credentials and try again." -ForegroundColor Yellow
    }
}
else {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host "  Manual Setup Required" -ForegroundColor Yellow
    Write-Host "========================================" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "You need to create an admin user first. Here's how:" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "1. Open SQL Server Management Studio or Azure Data Studio" -ForegroundColor White
    Write-Host "2. Connect to your database" -ForegroundColor White
    Write-Host "3. Run this SQL to check if you have a school:" -ForegroundColor White
    Write-Host ""
    Write-Host "   SELECT * FROM Schools;" -ForegroundColor Gray
    Write-Host ""
    Write-Host "4. If no school exists, create one:" -ForegroundColor White
    Write-Host ""
    Write-Host "   INSERT INTO Schools (Name, IsActive, CreatedAt)" -ForegroundColor Gray
    Write-Host "   VALUES ('Demo School', 1, GETUTCDATE());" -ForegroundColor Gray
    Write-Host ""
    Write-Host "5. Then use Swagger UI to create the first admin user:" -ForegroundColor White
    Write-Host "   - Open: https://localhost:7071/swagger" -ForegroundColor Cyan
    Write-Host "   - Find: POST /api/users" -ForegroundColor Cyan
    Write-Host "   - Create an admin user with email/password" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "6. After creating the admin, run this script again." -ForegroundColor White
}

Write-Host ""
Write-Host "Press any key to exit..." -ForegroundColor Gray
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
