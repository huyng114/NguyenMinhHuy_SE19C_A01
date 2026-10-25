param([string]$BaseUrl = 'http://localhost:5027')

# Also checks authentication regressions and loads the shared HTTP/assertion helpers.
. (Join-Path $PSScriptRoot 'Verify-Authentication.ps1') -BaseUrl $BaseUrl

$admin = New-Client
$member = New-Client
$testKey = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$testEmail = "qa-$testKey@example.test"
$testName = "QA Account $testKey"
$testPassword = "Qa!$testKey"
$testId = $null
$newsId = "qa-$testKey"
$connection = New-Object System.Data.SqlClient.SqlConnection($settings.ConnectionStrings.DefaultConnection)

try {
    $connection.Open()
    $login = Get-Page $admin '/Account/Login'
    $result = Post-Form $admin '/Account/Login' @{
        Email = $settings.AdminAccount.Email; Password = $settings.AdminAccount.Password
        __RequestVerificationToken = (Get-Token $login.Body)
    }
    Assert-Check ($result.Status -eq 302) 'Administrator can authenticate for account management'
    $list = Get-Page $admin '/AdminAccounts'
    Assert-Check ($list.Status -eq 200 -and $list.Body.Contains('Create account')) 'Administrator can list accounts'
    $create = Get-Page $admin '/AdminAccounts/Create'
    Assert-Check ($create.Status -eq 200 -and $create.Body.Contains('modal-header')) 'Create endpoint returns a modal form'
    $token = Get-Token $create.Body
    $fields = @{ Name = $testName; Email = $testEmail; Role = 1; Password = $testPassword; __RequestVerificationToken = $token }

    $withoutToken = Post-Form $admin '/AdminAccounts/Create' @{ Name = $testName; Email = $testEmail; Role = 1; Password = $testPassword }
    Assert-Check ($withoutToken.Status -eq 400) 'Create requires an antiforgery token'
    $invalid = Post-Form $admin '/AdminAccounts/Create' @{ Name = ''; Email = 'invalid'; Role = 3; Password = ''; __RequestVerificationToken = $token }
    Assert-Check ($invalid.Status -eq 422 -and $invalid.Body.Contains('field-validation-error')) 'Create validates fields and rejects unknown roles'
    $reserved = Post-Form $admin '/AdminAccounts/Create' @{ Name = $testName; Email = $settings.AdminAccount.Email; Role = 1; Password = $testPassword; __RequestVerificationToken = $token }
    Assert-Check ($reserved.Status -eq 422 -and $reserved.Body.Contains('reserved for the administrator')) 'Administrator email cannot be assigned to a database account'

    $created = Post-Form $admin '/AdminAccounts/Create' $fields
    Assert-Check ($created.Status -eq 200 -and ($created.Body | ConvertFrom-Json).success) 'Create saves a new Staff account'
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT AccountID FROM SystemAccount WHERE AccountEmail = @email'
    $null = $command.Parameters.AddWithValue('@email', $testEmail)
    $testId = [int16]$command.ExecuteScalar()
    Assert-Check ($testId -gt 0) 'New account receives an ID without an identity column'

    $duplicateFields = $fields.Clone()
    $duplicateFields.Email = $testEmail.ToUpperInvariant()
    $duplicate = Post-Form $admin '/AdminAccounts/Create' $duplicateFields
    Assert-Check ($duplicate.Status -eq 422 -and $duplicate.Body.Contains('already used')) 'Duplicate email is rejected regardless of case'
    Assert-Check (-not $duplicate.Body.Contains($testPassword)) 'Validation responses never expose submitted passwords'

    $search = Get-Page $admin ('/AdminAccounts?search=' + [Uri]::EscapeDataString($testEmail))
    Assert-Check ($search.Status -eq 200 -and $search.Body.Contains($testName) -and $search.Body.Contains('1 account found')) 'Search finds the account by email'
    $searchName = Get-Page $admin ('/AdminAccounts?search=' + [Uri]::EscapeDataString($testName))
    Assert-Check ($searchName.Body.Contains($testEmail)) 'Search finds the account by name'
    $roleFilter = Get-Page $admin ('/AdminAccounts?role=2&search=' + [Uri]::EscapeDataString($testEmail))
    Assert-Check ($roleFilter.Body.Contains('No accounts match')) 'Role filter excludes accounts of another role'
    Assert-Check (-not $search.Body.Contains($testPassword)) 'Account list does not expose passwords'

    $memberLogin = Get-Page $member '/Account/Login'
    $memberResult = Post-Form $member '/Account/Login' @{ Email = $testEmail; Password = $testPassword; __RequestVerificationToken = (Get-Token $memberLogin.Body) }
    Assert-Check ($memberResult.Status -eq 302 -and $memberResult.Location -eq '/Staff') 'New account can log in as Staff'
    $blocked = Get-Page $member '/AdminAccounts'
    Assert-Check ($blocked.Status -eq 302 -and $blocked.Location.Contains('/Account/AccessDenied')) 'Staff cannot access account management'

    $edit = Get-Page $admin "/AdminAccounts/Edit/$testId"
    Assert-Check ($edit.Status -eq 200 -and $edit.Body.Contains('Edit account') -and -not $edit.Body.Contains($testPassword)) 'Edit modal loads account data without the password'
    $updateFields = @{ Id = $testId; Name = "$testName Updated"; Email = $testEmail; Role = 2; Password = ''; __RequestVerificationToken = (Get-Token $edit.Body) }
    $updated = Post-Form $admin '/AdminAccounts/Edit' $updateFields
    Assert-Check ($updated.Status -eq 200 -and ($updated.Body | ConvertFrom-Json).success) 'Edit changes name and role with a blank password'
    $stale = Get-Page $member '/Staff'
    Assert-Check ($stale.Status -eq 302 -and $stale.Location.Contains('/Account/Login')) 'Changing role invalidates the previous authentication cookie'
    $memberLogin = Get-Page $member '/Account/Login'
    $memberResult = Post-Form $member '/Account/Login' @{ Email = $testEmail; Password = $testPassword; __RequestVerificationToken = (Get-Token $memberLogin.Body) }
    Assert-Check ($memberResult.Status -eq 302 -and $memberResult.Location -eq '/News') 'Blank edit password preserves existing login and new Lecturer role'

    $updateFields.Password = "New!$testPassword"
    $updated = Post-Form $admin '/AdminAccounts/Edit' $updateFields
    Assert-Check ($updated.Status -eq 200) 'Administrator can reset the password'
    $fresh = New-Client
    try {
        $page = Get-Page $fresh '/Account/Login'
        $oldLogin = Post-Form $fresh '/Account/Login' @{ Email = $testEmail; Password = $testPassword; __RequestVerificationToken = (Get-Token $page.Body) }
        Assert-Check ($oldLogin.Status -eq 200 -and $oldLogin.Body.Contains('Invalid email or password.')) 'Old password is rejected after reset'
        $newLogin = Post-Form $fresh '/Account/Login' @{ Email = $testEmail; Password = $updateFields.Password; __RequestVerificationToken = (Get-Token $page.Body) }
        Assert-Check ($newLogin.Status -eq 302) 'New password is accepted after reset'
    } finally { $fresh.Dispose() }

    $command = $connection.CreateCommand()
    $command.CommandText = 'INSERT INTO NewsArticle (NewsArticleID, Headline, CreatedByID, NewsStatus) VALUES (@newsId, @headline, @accountId, 0)'
    $null = $command.Parameters.AddWithValue('@newsId', $newsId)
    $null = $command.Parameters.AddWithValue('@headline', "QA cleanup fixture $testKey")
    $null = $command.Parameters.AddWithValue('@accountId', $testId)
    $null = $command.ExecuteNonQuery()
    $deleteFields = @{ id = $testId; __RequestVerificationToken = $token }
    $linked = Post-Form $admin '/AdminAccounts/Delete' $deleteFields
    Assert-Check ($linked.Status -eq 422 -and $linked.Body.Contains('linked to news articles')) 'Deletion is blocked for an account linked to a news article'
    $command.CommandText = 'SELECT COUNT(*) FROM NewsArticle WHERE NewsArticleID = @newsId'
    Assert-Check ([int]$command.ExecuteScalar() -eq 1) 'Blocked deletion preserves the news article'
    $command.CommandText = 'DELETE FROM NewsArticle WHERE NewsArticleID = @newsId'
    $null = $command.ExecuteNonQuery()

    $deleted = Post-Form $admin '/AdminAccounts/Delete' $deleteFields
    Assert-Check ($deleted.Status -eq 200 -and ($deleted.Body | ConvertFrom-Json).success) 'Unlinked account can be deleted'
    $missing = Get-Page $admin "/AdminAccounts/Edit/$testId"
    Assert-Check ($missing.Status -eq 404) 'Deleted account cannot be opened for editing'
    $deletedSession = Get-Page $member '/Staff'
    Assert-Check ($deletedSession.Status -eq 302 -and $deletedSession.Location.Contains('/Account/Login')) 'Deleting an account invalidates its previous authentication cookie'

    foreach ($role in @(1, 2)) {
        $other = New-Client
        try {
            $page = Get-Page $other '/Account/Login'
            $null = Post-Form $other '/Account/Login' @{ Email = $accounts[$role].Email; Password = $accounts[$role].Password; __RequestVerificationToken = (Get-Token $page.Body) }
            foreach ($path in @('/AdminAccounts', '/AdminAccounts/Create', "/AdminAccounts/Edit/$testId")) {
                $result = Get-Page $other $path
                Assert-Check ($result.Status -eq 302 -and $result.Location.Contains('/Account/AccessDenied')) "Role $role is denied GET $path"
            }
            foreach ($action in @('Create', 'Edit', 'Delete')) {
                $result = Post-Form $other "/AdminAccounts/$action" @{}
                Assert-Check ($result.Status -eq 302 -and $result.Location.Contains('/Account/AccessDenied')) "Role $role is denied POST $action"
            }
        } finally { $other.Dispose() }
    }
} finally {
    # Only remove fixtures generated by this run, even if a check fails.
    if ($connection.State -eq 'Open') {
        $cleanup = $connection.CreateCommand()
        $cleanup.CommandText = 'DELETE FROM NewsArticle WHERE NewsArticleID = @newsId; DELETE FROM SystemAccount WHERE AccountEmail = @email'
        $null = $cleanup.Parameters.AddWithValue('@newsId', $newsId)
        $null = $cleanup.Parameters.AddWithValue('@email', $testEmail)
        $null = $cleanup.ExecuteNonQuery()
    }
    $connection.Dispose()
    $admin.Dispose()
    $member.Dispose()
}
Write-Output "Completed $script:checks checks including authentication and account management. Test records were cleaned up."
