param([string]$BaseUrl = 'http://localhost:5027')

. (Join-Path $PSScriptRoot 'Verify-Authentication.ps1') -BaseUrl $BaseUrl -HelpersOnly

$admin = New-Client
$staff = New-Client
$key = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$oldEmail = "qa-profile-$key@example.test"
$newEmail = "qa-updated-$key@example.test"
$oldPassword = "Qa!$key"
$newPassword = "NewQa!$key"
$olderId = "qp-$key-a"
$morningId = "qp-$key-b"
$afternoonId = "qp-$key-c"
$olderTitle = "QA older $key"
$morningTitle = "QA morning $key"
$afternoonTitle = "QA afternoon $key"
$today = [datetime]::Today
$connection = New-Object System.Data.SqlClient.SqlConnection($settings.ConnectionStrings.DefaultConnection)

try {
    $connection.Open()
    $login = Get-Page $admin '/Account/Login'
    $response = Post-Form $admin '/Account/Login' @{
        Email = $settings.AdminAccount.Email; Password = $settings.AdminAccount.Password
        __RequestVerificationToken = (Get-Token $login.Body)
    }
    Assert-Check ($response.Status -eq 302) 'Admin can sign in for report verification'

    $form = Get-Page $admin '/AdminAccounts/Create'
    $response = Post-Form $admin '/AdminAccounts/Create' @{
        Name = "QA Staff $key"; Email = $oldEmail; Role = 1; Password = $oldPassword
        __RequestVerificationToken = (Get-Token $form.Body)
    }
    Assert-Check ($response.Status -eq 200 -and ($response.Body | ConvertFrom-Json).success) 'Temporary Staff account created'

    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT AccountID FROM SystemAccount WHERE AccountEmail = @email'
    $null = $command.Parameters.AddWithValue('@email', $oldEmail)
    $staffId = [int16]$command.ExecuteScalar()
    Assert-Check ($staffId -gt 0) 'Temporary Staff account exists in SQL Server'

    foreach ($article in @(
        @{ Id = $olderId; Title = $olderTitle; Created = $today.AddDays(-1).AddHours(12); Status = $true },
        @{ Id = $morningId; Title = $morningTitle; Created = $today.AddHours(9); Status = $false },
        @{ Id = $afternoonId; Title = $afternoonTitle; Created = $today.AddHours(15); Status = $true }
    )) {
        $insert = $connection.CreateCommand()
        $insert.CommandText = 'INSERT INTO NewsArticle (NewsArticleID, NewsTitle, Headline, CreatedDate, CreatedByID, NewsStatus) VALUES (@id, @title, @title, @created, @author, @status)'
        $null = $insert.Parameters.AddWithValue('@id', $article.Id)
        $null = $insert.Parameters.AddWithValue('@title', $article.Title)
        $null = $insert.Parameters.AddWithValue('@created', $article.Created)
        $null = $insert.Parameters.AddWithValue('@author', $staffId)
        $null = $insert.Parameters.AddWithValue('@status', $article.Status)
        $null = $insert.ExecuteNonQuery()
    }

    $login = Get-Page $staff '/Account/Login'
    $response = Post-Form $staff '/Account/Login' @{
        Email = $oldEmail; Password = $oldPassword
        __RequestVerificationToken = (Get-Token $login.Body)
    }
    Assert-Check ($response.Status -eq 302 -and $response.Location -eq '/Staff') 'Temporary Staff account can sign in'

    $profile = Get-Page $staff '/StaffProfile'
    Assert-Check ($profile.Status -eq 200 -and $profile.Body.Contains($oldEmail)) 'Staff can view their profile'
    $token = Get-Token $profile.Body
    $wrong = Post-Form $staff '/StaffProfile' @{
        Name = "QA Staff $key"; Email = $newEmail; CurrentPassword = 'wrong-password'
        __RequestVerificationToken = $token
    }
    Assert-Check ($wrong.Status -eq 200 -and $wrong.Body.Contains('Current password is incorrect.')) 'Profile rejects an incorrect current password'
    Assert-Check (-not $wrong.Body.Contains('wrong-password')) 'Profile validation does not echo the submitted password'

    $updated = Post-Form $staff '/StaffProfile' @{
        Name = "QA Updated $key"; Email = $newEmail; CurrentPassword = $oldPassword
        NewPassword = $newPassword; ConfirmNewPassword = $newPassword
        __RequestVerificationToken = $token
    }
    if ($updated.Status -ne 302 -or $updated.Location -notin @('/', '/Account/Login')) {
        $errors = [regex]::Matches($updated.Body, '<span[^>]*field-validation-error[^>]*>(.*?)</span>|<div[^>]*validation-summary-errors[^>]*>(.*?)</div>') |
            ForEach-Object { [System.Net.WebUtility]::HtmlDecode(($_.Groups[1].Value + $_.Groups[2].Value)) }
        throw "Profile update returned HTTP $($updated.Status), location '$($updated.Location)', errors: $($errors -join '; ')"
    }
    Assert-Check ($updated.Status -eq 302 -and $updated.Location -in @('/', '/Account/Login')) 'Staff can update profile and is signed out'
    $signedOut = Get-Page $staff '/StaffProfile'
    Assert-Check ($signedOut.Status -eq 302 -and $signedOut.Location.Contains('/Account/Login')) 'Updated profile session no longer accesses Staff pages'

    $login = Get-Page $staff '/Account/Login'
    $oldLogin = Post-Form $staff '/Account/Login' @{
        Email = $oldEmail; Password = $oldPassword
        __RequestVerificationToken = (Get-Token $login.Body)
    }
    Assert-Check ($oldLogin.Status -eq 200 -and $oldLogin.Body.Contains('Invalid email or password.')) 'Old profile credentials are rejected'
    Assert-Check (-not $oldLogin.Body.Contains($oldPassword)) 'Failed login does not echo the submitted password'
    $newLogin = Post-Form $staff '/Account/Login' @{
        Email = $newEmail; Password = $newPassword
        __RequestVerificationToken = (Get-Token $login.Body)
    }
    Assert-Check ($newLogin.Status -eq 302 -and $newLogin.Location -eq '/Staff') 'Updated profile credentials can sign in'

    $history = Get-Page $staff '/StaffNews/History'
    Assert-Check ($history.Status -eq 200 -and $history.Body.Contains($olderTitle) -and
        $history.Body.Contains($morningTitle) -and $history.Body.Contains($afternoonTitle)) 'Staff history contains articles created by that Staff account'

    $date = $today.ToString('yyyy-MM-dd')
    $report = Get-Page $admin "/Admin/Report?startDate=$date&endDate=$date"
    $afternoonPosition = $report.Body.IndexOf($afternoonTitle)
    $morningPosition = $report.Body.IndexOf($morningTitle)
    Assert-Check ($report.Status -eq 200 -and $afternoonPosition -ge 0 -and
        $morningPosition -gt $afternoonPosition -and -not $report.Body.Contains($olderTitle)) 'Report includes the full selected day and sorts by CreatedDate descending'

    $tomorrow = $today.AddDays(1).ToString('yyyy-MM-dd')
    $invalidReport = Get-Page $admin "/Admin/Report?startDate=$tomorrow&endDate=$date"
    Assert-Check ($invalidReport.Status -eq 200 -and $invalidReport.Body.Contains('End date must be on or after start date.')) 'Report rejects an inverted date range'
} finally {
    if ($connection.State -eq 'Open') {
        $cleanup = $connection.CreateCommand()
        $cleanup.CommandText = 'DELETE FROM NewsArticle WHERE NewsArticleID IN (@older, @morning, @afternoon); DELETE FROM SystemAccount WHERE AccountEmail IN (@oldEmail, @newEmail)'
        $null = $cleanup.Parameters.AddWithValue('@older', $olderId)
        $null = $cleanup.Parameters.AddWithValue('@morning', $morningId)
        $null = $cleanup.Parameters.AddWithValue('@afternoon', $afternoonId)
        $null = $cleanup.Parameters.AddWithValue('@oldEmail', $oldEmail)
        $null = $cleanup.Parameters.AddWithValue('@newEmail', $newEmail)
        $null = $cleanup.ExecuteNonQuery()
    }
    $connection.Dispose()
    $admin.Dispose()
    $staff.Dispose()
}

Write-Output "Completed $script:checks profile, history and report checks. Temporary records were cleaned up."
