$body = @{
    email = "admin@demo.schoolportal.com"
    password = "Admin@123"
} | ConvertTo-Json

Write-Host "Testing login API..."
Write-Host "Body: $body"

try {
    $response = Invoke-WebRequest -Uri "http://localhost:5128/api/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $body `
        -UseBasicParsing
    
    Write-Host "Success! Status: $($response.StatusCode)"
    Write-Host "Response: $($response.Content)"
}
catch {
    Write-Host "Error Status: $($_.Exception.Response.StatusCode.value__)"
    Write-Host "Error: $($_.Exception.Message)"
    
    $result = $_.Exception.Response.GetResponseStream()
    $reader = New-Object System.IO.StreamReader($result)
    $responseBody = $reader.ReadToEnd()
    Write-Host "Response Body: $responseBody"
}
