param(
    [string]$BaseUrl = "https://localhost:7034",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$projectRoot = Split-Path -Parent $PSScriptRoot
$failures = New-Object System.Collections.Generic.List[string]

function Test-Condition {
    param([bool]$Condition, [string]$Name, [string]$Detail)
    if ($Condition) {
        Write-Host "PASS  $Name" -ForegroundColor Green
    }
    else {
        Write-Host "FAIL  $Name - $Detail" -ForegroundColor Red
        $failures.Add($Name)
    }
}

Push-Location $projectRoot
try {
    if (-not $SkipBuild) {
        & dotnet build THAN_NONG_SHOP.csproj --no-restore -o .tmp/smoke-build
        Test-Condition ($LASTEXITCODE -eq 0) "Project build" "dotnet build failed"
    }

    $sharedConfigs = @("appsettings.json", "appsettings.Production.json", "appsettings.Development.example.json")
    $secretPattern = 'sk-[A-Za-z0-9_-]{20,}|Password\s*=\s*[^;\"]+|"(ApiKey|ChecksumKey|ClientId)"\s*:\s*"(?!"|YOUR_)'
    $secretHits = Select-String -Path $sharedConfigs -Pattern $secretPattern
    Test-Condition ($null -eq $secretHits) "Shared configuration has no credentials" "A possible credential was found"

    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.AllowAutoRedirect = $false
    $handler.ServerCertificateCustomValidationCallback = { param($message, $certificate, $chain, $errors) $true }
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.Timeout = [TimeSpan]::FromSeconds(10)

    $cases = @(
        @{ Path = "/"; Expected = 200; Name = "Home page" },
        @{ Path = "/Products"; Expected = 200; Name = "Product listing" },
        @{ Path = "/Account/Login"; Expected = 200; Name = "Login page" },
        @{ Path = "/Admin"; Expected = 302; Name = "Admin requires authentication" },
        @{ Path = "/Cart/Checkout"; Expected = 302; Name = "Checkout requires authentication" },
        @{ Path = "/Account/Logout"; Expected = 405; Name = "Logout rejects GET" }
    )

    foreach ($case in $cases) {
        try {
            $response = $client.GetAsync("$BaseUrl$($case.Path)").GetAwaiter().GetResult()
            Test-Condition ([int]$response.StatusCode -eq $case.Expected) $case.Name "expected $($case.Expected), received $([int]$response.StatusCode)"
        }
        catch {
            Test-Condition $false $case.Name $_.Exception.Message
        }
    }

    try {
        $emptyContent = New-Object System.Net.Http.StringContent("")
        $logoutResponse = $client.PostAsync("$BaseUrl/Account/Logout", $emptyContent).GetAwaiter().GetResult()
        Test-Condition ([int]$logoutResponse.StatusCode -eq 400) "Logout requires anti-forgery token" "expected 400, received $([int]$logoutResponse.StatusCode)"
    }
    catch {
        Test-Condition $false "Logout requires anti-forgery token" $_.Exception.Message
    }

    $client.Dispose()
    $handler.Dispose()
}
finally {
    Pop-Location
}

if ($failures.Count -gt 0) {
    Write-Host "`nSmoke test failed: $($failures.Count) check(s)." -ForegroundColor Red
    exit 1
}

Write-Host "`nAll smoke tests passed." -ForegroundColor Green
exit 0
