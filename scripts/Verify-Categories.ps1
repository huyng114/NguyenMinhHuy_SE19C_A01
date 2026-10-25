param([string]$BaseUrl = 'http://localhost:5027')

. (Join-Path $PSScriptRoot 'Verify-Authentication.ps1') -BaseUrl $BaseUrl -HelpersOnly
$connection = New-Object System.Data.SqlClient.SqlConnection($settings.ConnectionStrings.DefaultConnection)
$staff = New-Client
$testKey = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$prefix = "QA-Category-$testKey"
$description = "Description-$testKey"
$newsId = "qc-$testKey"
$createdIds = New-Object 'System.Collections.Generic.List[int]'

function Find-TestCategory([string]$Name) {
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT CategoryID FROM Category WHERE CategoryName = @name'
    $null = $command.Parameters.AddWithValue('@name', $Name)
    return [int]$command.ExecuteScalar()
}

function Login-As($Client, [string]$Email, [string]$Password) {
    $page = Get-Page $Client '/Account/Login'
    $response = Post-Form $Client '/Account/Login' @{ Email = $Email; Password = $Password; __RequestVerificationToken = (Get-Token $page.Body) }
    if ($response.Status -ne 302) { throw 'Test account login failed.' }
}

try {
    $connection.Open()
    $accounts = @{}
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT AccountRole, AccountEmail, AccountPassword FROM SystemAccount WHERE AccountRole IN (1, 2)'
    $reader = $command.ExecuteReader()
    while ($reader.Read()) {
        $role = [int]$reader.GetValue(0)
        if (-not $accounts.ContainsKey($role)) { $accounts[$role] = @{ Email = [string]$reader.GetValue(1); Password = [string]$reader.GetValue(2) } }
    }
    $reader.Close()
    Login-As $staff $accounts[1].Email $accounts[1].Password
    $list = Get-Page $staff '/Category'
    Assert-Check ($list.Status -eq 200 -and $list.Body.Contains('Create category')) 'Staff can view category management'
    Assert-Check ($list.Body.Contains('id="deleteCategoryModal"')) 'Category list contains a delete confirmation dialog'
    $create = Get-Page $staff '/Category/Create'
    $token = Get-Token $create.Body
    Assert-Check ($create.Status -eq 200 -and $create.Body.Contains('modal-header') -and $create.Body.Contains('ParentId')) 'Create returns a modal with parent and status fields'

    foreach ($action in @('Create', 'Edit', 'Delete')) {
        $response = Post-Form $staff "/Category/$action" @{}
        Assert-Check ($response.Status -eq 400) "$action requires an antiforgery token"
    }
    $response = Post-Form $staff '/Category/Create' @{ Name = ' '; Description = ''; IsActive = ''; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('field-validation-error')) 'Server rejects missing name, description and status'
    $response = Post-Form $staff '/Category/Create' @{ Name = ('N' * 101); Description = ('D' * 251); IsActive = 'true'; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 422) 'Server rejects fields longer than the database limits'

    $command.CommandText = 'SELECT TOP (1) candidate FROM (VALUES (CONVERT(smallint, -32768)), (CONVERT(smallint, -32767)), (CONVERT(smallint, 32767))) AS ids(candidate) WHERE NOT EXISTS (SELECT 1 FROM Category WHERE CategoryID = candidate)'
    $missingId = [int]$command.ExecuteScalar()
    $response = Post-Form $staff '/Category/Create' @{ Name = "$prefix-Invalid"; Description = $description; IsActive = 'true'; ParentId = $missingId; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('parent category no longer exists')) 'Create rejects a nonexistent parent'

    $response = Post-Form $staff '/Category/Create' @{ Name = "$prefix-Root"; Description = $description; IsActive = 'true'; ParentId = ''; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 200 -and ($response.Body | ConvertFrom-Json).success) 'Staff can create a top-level active category'
    $rootId = Find-TestCategory "$prefix-Root"
    $createdIds.Add($rootId)
    Assert-Check ($rootId -gt 0) 'SQL Server generates the category identity ID'
    $response = Post-Form $staff '/Category/Create' @{ Name = "$prefix-Child"; Description = $description; IsActive = 'false'; ParentId = $rootId; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 200) 'Staff can create an inactive child category'
    $childId = Find-TestCategory "$prefix-Child"
    $createdIds.Add($childId)
    $response = Post-Form $staff '/Category/Create' @{ Name = "$prefix-Grandchild"; Description = $description; IsActive = 'true'; ParentId = $childId; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 200) 'Staff can create a category under an inactive parent'
    $grandchildId = Find-TestCategory "$prefix-Grandchild"
    $createdIds.Add($grandchildId)

    $search = Get-Page $staff ('/Category?search=' + [Uri]::EscapeDataString("$prefix-Child"))
    Assert-Check ($search.Status -eq 200 -and $search.Body.Contains("$prefix-Child") -and $search.Body.Contains("$prefix-Root")) 'Search by name shows the category and parent'
    $search = Get-Page $staff ('/Category?search=' + [Uri]::EscapeDataString($description))
    Assert-Check ($search.Body.Contains('3 categories found')) 'Search by description finds all matching categories'
    $search = Get-Page $staff ('/Category?isActive=false&search=' + [Uri]::EscapeDataString($prefix))
    Assert-Check ($search.Body.Contains('1 category found') -and $search.Body.Contains("$prefix-Child")) 'Inactive status filter returns the inactive child'
    $search = Get-Page $staff ('/Category?isActive=true&search=' + [Uri]::EscapeDataString($prefix))
    Assert-Check ($search.Body.Contains('2 categories found')) 'Active status filter returns only active categories'
    $empty = Get-Page $staff ('/Category?search=' + [Uri]::EscapeDataString("NoMatch-$testKey"))
    Assert-Check ($empty.Body.Contains('No categories match your filters.')) 'Search displays a useful empty state'

    $edit = Get-Page $staff "/Category/Edit/$rootId"
    Assert-Check ($edit.Status -eq 200 -and $edit.Body.Contains('Edit category')) 'Edit endpoint returns a populated modal'
    Assert-Check (-not $edit.Body.Contains("<option value=`"$rootId`"") -and -not $edit.Body.Contains("<option value=`"$childId`"") -and -not $edit.Body.Contains("<option value=`"$grandchildId`"")) 'Parent options exclude the category and all descendants'
    $fields = @{ Id = $rootId; Name = "$prefix-Root"; Description = $description; IsActive = 'true'; ParentId = $rootId; __RequestVerificationToken = $token }
    $response = Post-Form $staff '/Category/Edit' $fields
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('cannot be its own parent')) 'Server rejects a self-parent relationship'
    $fields.ParentId = $grandchildId
    $response = Post-Form $staff '/Category/Edit' $fields
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('circular category hierarchy')) 'Server rejects a cycle across several generations'
    $fields.ParentId = $missingId
    $response = Post-Form $staff '/Category/Edit' $fields
    Assert-Check ($response.Status -eq 422) 'Edit rejects a nonexistent parent'
    $fields.ParentId = ''
    $fields.Name = "$prefix-Root Updated"
    $fields.Description = "$description Updated"
    $fields.IsActive = 'false'
    $response = Post-Form $staff '/Category/Edit' $fields
    Assert-Check ($response.Status -eq 200 -and ($response.Body | ConvertFrom-Json).success) 'Staff can update category name, description and status'
    $edited = Get-Page $staff "/Category/Edit/$rootId"
    Assert-Check ($edited.Body.Contains("$description Updated") -and $edited.Body.Contains("$prefix-Root Updated")) 'Saved changes appear when reopening the edit modal'

    $response = Post-Form $staff '/Category/Delete' @{ id = $rootId; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('child categories')) 'Cannot delete a category that has children'
    $command = $connection.CreateCommand()
    $command.CommandText = 'INSERT INTO NewsArticle (NewsArticleID, Headline, CategoryID, NewsStatus) VALUES (@newsId, @headline, @categoryId, 0)'
    $null = $command.Parameters.AddWithValue('@newsId', $newsId)
    $null = $command.Parameters.AddWithValue('@headline', "Category test $testKey")
    $null = $command.Parameters.AddWithValue('@categoryId', $grandchildId)
    $null = $command.ExecuteNonQuery()
    foreach ($active in @(0, 1)) {
        $command.CommandText = "UPDATE NewsArticle SET NewsStatus = $active WHERE NewsArticleID = @newsId"
        $null = $command.ExecuteNonQuery()
        $response = Post-Form $staff '/Category/Delete' @{ id = $grandchildId; __RequestVerificationToken = $token }
        Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('used by news articles')) "Cannot delete a category used by news with status $active"
    }
    $command.CommandText = 'SELECT COUNT(*) FROM NewsArticle WHERE NewsArticleID = @newsId'
    Assert-Check ([int]$command.ExecuteScalar() -eq 1) 'Blocked deletion preserves the related news article'
    $command.CommandText = 'DELETE FROM NewsArticle WHERE NewsArticleID = @newsId'
    $null = $command.ExecuteNonQuery()

    $moved = Post-Form $staff '/Category/Edit' @{ Id = $grandchildId; Name = "$prefix-Grandchild"; Description = $description; IsActive = 'true'; ParentId = ''; __RequestVerificationToken = $token }
    Assert-Check ($moved.Status -eq 200) 'Staff can move a child category to the top level'
    foreach ($categoryId in @($grandchildId, $childId, $rootId)) {
        $response = Post-Form $staff '/Category/Delete' @{ id = $categoryId; __RequestVerificationToken = $token }
        Assert-Check ($response.Status -eq 200 -and ($response.Body | ConvertFrom-Json).success) "Staff can delete unused category $categoryId"
    }
    $missing = Get-Page $staff "/Category/Edit/$rootId"
    Assert-Check ($missing.Status -eq 404) 'Opening a deleted category returns 404'
    $response = Post-Form $staff '/Category/Edit' $fields
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('no longer exists')) 'Editing a deleted category reports a useful error'
    $response = Post-Form $staff '/Category/Delete' @{ id = $rootId; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('no longer exists')) 'Deleting an already deleted category reports a useful error'

    foreach ($identity in @('Guest', 'Lecturer', 'Admin')) {
        $client = New-Client
        try {
            if ($identity -eq 'Lecturer') { Login-As $client $accounts[2].Email $accounts[2].Password }
            if ($identity -eq 'Admin') { Login-As $client $settings.AdminAccount.Email $settings.AdminAccount.Password }
            $destination = if ($identity -eq 'Guest') { '/Account/Login' } else { '/Account/AccessDenied' }
            foreach ($path in @('/Category', '/Category/Create', "/Category/Edit/$rootId")) {
                $response = Get-Page $client $path
                Assert-Check ($response.Status -eq 302 -and $response.Location.Contains($destination)) "$identity is blocked from GET $path"
            }
            foreach ($action in @('Create', 'Edit', 'Delete')) {
                $response = Post-Form $client "/Category/$action" @{}
                Assert-Check ($response.Status -eq 302 -and $response.Location.Contains($destination)) "$identity is blocked from POST $action"
            }
        } finally { $client.Dispose() }
    }
} finally {
    if ($connection.State -eq 'Open') {
        # Restrict cleanup to the unique fixtures created by this run, including failed assertions.
        $cleanup = $connection.CreateCommand()
        $cleanup.CommandText = 'DELETE FROM NewsArticle WHERE NewsArticleID = @newsId; UPDATE Category SET ParentCategoryID = NULL WHERE CategoryName LIKE @prefix; DELETE FROM Category WHERE CategoryName LIKE @prefix'
        $null = $cleanup.Parameters.AddWithValue('@newsId', $newsId)
        $null = $cleanup.Parameters.AddWithValue('@prefix', "$prefix%")
        $null = $cleanup.ExecuteNonQuery()
    }
    $connection.Dispose()
    $staff.Dispose()
}
Write-Output "Completed $script:checks category checks. Temporary records were cleaned up."
