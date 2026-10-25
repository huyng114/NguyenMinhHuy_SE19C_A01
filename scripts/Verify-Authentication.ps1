param([string]$BaseUrl = 'http://localhost:5027', [switch]$HelpersOnly)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$projectRoot = Split-Path -Parent $PSScriptRoot
$settings = Get-Content -Raw (Join-Path $projectRoot 'NguyenMinhHuyMVC/appsettings.json') | ConvertFrom-Json
$script:checks = 0

function Assert-Check($Condition, [string]$Description) {
    if (-not $Condition) { throw "FAILED: $Description" }
    $script:checks++
    Write-Output "PASS: $Description"
}

function New-Client {
    $handler = New-Object System.Net.Http.HttpClientHandler
    $handler.AllowAutoRedirect = $false
    $handler.UseProxy = $false
    $client = New-Object System.Net.Http.HttpClient($handler)
    $client.BaseAddress = [Uri]$BaseUrl
    return $client
}

function Get-Page($Client, [string]$Path) {
    $response = $Client.GetAsync($Path).GetAwaiter().GetResult()
    try {
        return [pscustomobject]@{
            Status = [int]$response.StatusCode
            Location = [string]$response.Headers.Location
            Body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        }
    } finally { $response.Dispose() }
}

function Get-Token([string]$Html) {
    $match = [regex]::Match($Html, 'name="__RequestVerificationToken"[^>]*value="([^"]+)"')
    if (-not $match.Success) { throw 'Antiforgery token is missing.' }
    return [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
}

function Post-Form($Client, [string]$Path, [hashtable]$Fields) {
    $values = New-Object 'System.Collections.Generic.List[System.Collections.Generic.KeyValuePair[string,string]]'
    foreach ($key in $Fields.Keys) {
        foreach ($value in @($Fields[$key])) {
            $values.Add([System.Collections.Generic.KeyValuePair[string,string]]::new([string]$key, [string]$value))
        }
    }
    $content = [System.Net.Http.FormUrlEncodedContent]::new($values)
    try {
        $response = $Client.PostAsync($Path, $content).GetAwaiter().GetResult()
        try {
            return [pscustomobject]@{
                Status = [int]$response.StatusCode
                Location = [string]$response.Headers.Location
                Body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            }
        } finally { $response.Dispose() }
    } finally { $content.Dispose() }
}

if ($HelpersOnly) { return }

# Read existing data only. Credentials remain in memory and are never logged.
$connection = New-Object System.Data.SqlClient.SqlConnection($settings.ConnectionStrings.DefaultConnection)
$accounts = @{}
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT AccountRole, AccountEmail, AccountPassword FROM SystemAccount WHERE AccountRole IN (1, 2)'
    $reader = $command.ExecuteReader()
    while ($reader.Read()) {
        $role = [int]$reader.GetValue(0)
        if (-not $accounts.ContainsKey($role)) {
            $accounts[$role] = @{ Email = [string]$reader.GetValue(1); Password = [string]$reader.GetValue(2) }
        }
    }
    $reader.Close()
    $command.CommandText = 'SELECT COUNT(*) FROM NewsArticle WHERE NewsStatus = 1'
    $activeNewsCount = [int]$command.ExecuteScalar()
} finally { $connection.Dispose() }

$guest = New-Client
try {
    $login = Get-Page $guest '/'
    Assert-Check ($login.Status -eq 200 -and $login.Body.Contains('Sign in to access your account.')) 'Default page is Login'
    foreach ($path in @('/Admin', '/Staff')) {
        $page = Get-Page $guest $path
        Assert-Check ($page.Status -eq 302 -and $page.Location.Contains('/Account/Login')) "Guest is blocked from $path"
    }
    $news = Get-Page $guest '/News'
    $renderedCount = [regex]::Matches($news.Body, '<article class="card mb-3">').Count
    Assert-Check ($news.Status -eq 200 -and $renderedCount -eq $activeNewsCount) 'Guest news page matches active article count from SQL Server'
    $invalid = Post-Form $guest '/Account/Login' @{ Email = $settings.AdminAccount.Email; Password = 'incorrect-password'; __RequestVerificationToken = (Get-Token $login.Body) }
    Assert-Check ($invalid.Status -eq 200 -and $invalid.Body.Contains('Invalid email or password.')) 'Incorrect password is rejected'
    $missingToken = Post-Form $guest '/Account/Login' @{ Email = $settings.AdminAccount.Email; Password = $settings.AdminAccount.Password }
    Assert-Check ($missingToken.Status -eq 400) 'Login requires an antiforgery token'
    $invalidFields = Post-Form $guest '/Account/Login' @{ Email = 'invalid-email'; Password = ''; __RequestVerificationToken = (Get-Token $login.Body) }
    Assert-Check ($invalidFields.Status -eq 200 -and $invalidFields.Body.Contains('field-validation-error')) 'Server validates login fields'
} finally { $guest.Dispose() }

$scenarios = @(
    @{ Role = 'Admin'; Email = $settings.AdminAccount.Email; Password = $settings.AdminAccount.Password; Target = '/Admin'; Forbidden = @('/Staff') },
    @{ Role = 'Staff'; Email = $accounts[1].Email; Password = $accounts[1].Password; Target = '/Staff'; Forbidden = @('/Admin') },
    @{ Role = 'Lecturer'; Email = $accounts[2].Email; Password = $accounts[2].Password; Target = '/News'; Forbidden = @('/Admin', '/Staff') }
)

foreach ($scenario in $scenarios) {
    $client = New-Client
    try {
        $login = Get-Page $client '/Account/Login'
        $result = Post-Form $client '/Account/Login' @{
            Email = $scenario.Email; Password = $scenario.Password
            ReturnUrl = 'https://example.invalid/untrusted'
            __RequestVerificationToken = (Get-Token $login.Body)
        }
        Assert-Check ($result.Status -eq 302 -and $result.Location -eq $scenario.Target) "$($scenario.Role) login redirects to the correct page and rejects external ReturnUrl"
        $dashboard = Get-Page $client $scenario.Target
        Assert-Check ($dashboard.Status -eq 200 -and $dashboard.Body.Contains('Logout')) "$($scenario.Role) authentication cookie is accepted"
        foreach ($path in $scenario.Forbidden) {
            $blocked = Get-Page $client $path
            Assert-Check ($blocked.Status -eq 302 -and $blocked.Location.Contains('/Account/AccessDenied')) "$($scenario.Role) is blocked from $path"
        }
        $denied = Get-Page $client '/Account/AccessDenied'
        Assert-Check ($denied.Status -eq 403) 'Access denied page returns HTTP 403'
        $unsafeLogout = Post-Form $client '/Account/Logout' @{}
        Assert-Check ($unsafeLogout.Status -eq 400) 'Logout requires an antiforgery token'
        $logout = Post-Form $client '/Account/Logout' @{ __RequestVerificationToken = (Get-Token $dashboard.Body) }
        Assert-Check ($logout.Status -eq 302 -and $logout.Location -in @('/', '/Account/Login')) "$($scenario.Role) can log out"
        $afterLogout = Get-Page $client '/Admin'
        Assert-Check ($afterLogout.Status -eq 302 -and $afterLogout.Location.Contains('/Account/Login')) 'Logged out session cannot access a protected page'
    } finally { $client.Dispose() }
}

Write-Output "Completed $script:checks authentication checks. No database records were changed."
