#requires -Version 7.0
param(
    [string]$BaseUrl = "https://localhost:7034",
    [Parameter(Mandatory=$true)][string]$Username,
    [Parameter(Mandatory=$true)][SecureString]$Password
)
$ErrorActionPreference="Stop"
$session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
function Token([string]$html){$match=[regex]::Match($html,'name="__RequestVerificationToken"[^>]*value="([^"]+)"');if(-not $match.Success){throw "Không tìm thấy anti-forgery token"};$match.Groups[1].Value}
function Check([bool]$ok,[string]$name){if(-not $ok){throw "FAIL $name"};Write-Host "PASS $name" -ForegroundColor Green}
$login=Invoke-WebRequest "$BaseUrl/Account/Login?returnUrl=%2FHome%2FPromotions" -WebSession $session -SkipCertificateCheck
$plain=[System.Net.NetworkCredential]::new('', $Password).Password
$loginResponse=Invoke-WebRequest "$BaseUrl/Account/Login" -Method Post -WebSession $session -SkipCertificateCheck -MaximumRedirection 0 -SkipHttpErrorCheck -Body @{username=$Username;password=$plain;returnUrl='/Home/Promotions';__RequestVerificationToken=(Token $login.Content)}
Check ($loginResponse.StatusCode -eq 302) "Đăng nhập tài khoản kiểm thử"
$promo=Invoke-WebRequest "$BaseUrl/Home/Promotions" -WebSession $session -SkipCertificateCheck
$spinToken=Token $promo.Content
$first=Invoke-RestMethod "$BaseUrl/Home/SpinPromotion" -Method Post -WebSession $session -SkipCertificateCheck -Headers @{RequestVerificationToken=$spinToken}
$second=Invoke-RestMethod "$BaseUrl/Home/SpinPromotion" -Method Post -WebSession $session -SkipCertificateCheck -Headers @{RequestVerificationToken=$spinToken}
Check ($first.code -eq $second.code -and $second.replay -eq $true) "Một lượt quay mỗi tài khoản mỗi ngày"
Check ($first.code -match '^TN[A-HJ-NP-Z2-9]{12}$') "Mã voucher ngẫu nhiên đúng định dạng"
$profile=Invoke-WebRequest "$BaseUrl/Account/Profile" -WebSession $session -SkipCertificateCheck
Check ($profile.Content -match [regex]::Escape($first.code)) "Voucher xuất hiện trong hồ sơ đúng chủ"
Write-Host "Hoàn tất kiểm thử tích hợp promotion trên SQL Server." -ForegroundColor Green
