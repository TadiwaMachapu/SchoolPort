$body = @{
    email = "admin@demo.schoolportal.com"
    password = "Admin@123"
} | ConvertTo-Json

$response = Invoke-WebRequest -Uri "http://localhost:5128/api/auth/login" `
    -Method POST `
    -ContentType "application/json" `
    -Body $body `
    -UseBasicParsing

Write-Host "Status Code: $($response.StatusCode)"
Write-Host "Response: $($response.Content)"
