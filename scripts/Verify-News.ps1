param([string]$BaseUrl = 'http://localhost:5027')

. (Join-Path $PSScriptRoot 'Verify-Authentication.ps1') -BaseUrl $BaseUrl -HelpersOnly
$connection = New-Object System.Data.SqlClient.SqlConnection($settings.ConnectionStrings.DefaultConnection)
$staff = New-Client
$editor = New-Client
$guest = New-Client
$testKey = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$prefix = "QA-News-$testKey"
$articleId = $null
$categoryId = $null

function Read-Scalar([string]$Sql, [hashtable]$Parameters = @{}) {
    $command = $connection.CreateCommand()
    $command.CommandText = $Sql
    foreach ($key in $Parameters.Keys) { $null = $command.Parameters.AddWithValue($key, $Parameters[$key]) }
    return $command.ExecuteScalar()
}
function Login-As($Client, $Account) {
    $page = Get-Page $Client '/Account/Login'
    $response = Post-Form $Client '/Account/Login' @{ Email = $Account.Email; Password = $Account.Password; __RequestVerificationToken = (Get-Token $page.Body) }
    if ($response.Status -ne 302) { throw 'Test login failed.' }
}

try {
    $connection.Open()
    $staffAccounts = @()
    $lecturer = $null
    $command = $connection.CreateCommand()
    $command.CommandText = 'SELECT AccountID, AccountRole, AccountEmail, AccountPassword FROM SystemAccount WHERE AccountRole IN (1, 2) ORDER BY AccountID'
    $reader = $command.ExecuteReader()
    while ($reader.Read()) {
        $account = @{ Id = [int]$reader.GetValue(0); Email = [string]$reader.GetValue(2); Password = [string]$reader.GetValue(3) }
        if ([int]$reader.GetValue(1) -eq 1) { $staffAccounts += $account } else { $lecturer = $account }
    }
    $reader.Close()
    if ($staffAccounts.Count -lt 2 -or $null -eq $lecturer) { throw 'Requires two Staff accounts and one Lecturer account in seed data.' }
    $command.CommandText = 'SELECT TOP (2) TagID, TagName FROM Tag ORDER BY TagID'
    $reader = $command.ExecuteReader()
    $tags = @()
    while ($reader.Read()) { $tags += @{ Id = [int]$reader.GetValue(0); Name = [string]$reader.GetValue(1) } }
    $reader.Close()
    if ($tags.Count -lt 2) { throw 'Requires two existing tags in seed data.' }
    $tagIds = @($tags[0].Id, $tags[1].Id)
    Login-As $staff $staffAccounts[0]
    Login-As $editor $staffAccounts[1]
    $list = Get-Page $staff '/StaffNews'
    Assert-Check ($list.Status -eq 200 -and $list.Body.Contains('Create article')) 'Staff can access news management'
    Assert-Check ($list.Body.Contains('id="deleteNewsModal"')) 'News management contains a delete confirmation dialog'
    $create = Get-Page $staff '/StaffNews/Create'
    $token = Get-Token $create.Body
    Assert-Check ($create.Status -eq 200 -and $create.Body.Contains('modal-header') -and $create.Body.Contains('name="TagIds"')) 'Create returns a modal with available tags'
    foreach ($action in @('Create', 'Edit', 'Delete')) {
        $response = Post-Form $staff "/StaffNews/$action" @{}
        Assert-Check ($response.Status -eq 400) "$action requires an antiforgery token"
    }
    $response = Post-Form $staff '/StaffNews/Create' @{ Headline = ''; Status = ''; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('field-validation-error')) 'Server rejects missing headline and status'
    $response = Post-Form $staff '/StaffNews/Create' @{ Headline = ('H' * 151); Title = ('T' * 401); Content = ('C' * 4001); Source = ('S' * 401); Status = 'true'; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 422) 'Server enforces database field lengths'
    $missingCategory = Read-Scalar 'SELECT TOP (1) id FROM (VALUES (CONVERT(smallint,-32768)), (CONVERT(smallint,32767))) ids(id) WHERE NOT EXISTS (SELECT 1 FROM Category WHERE CategoryID = id)'
    $missingTag = Read-Scalar 'SELECT TOP (1) id FROM (VALUES (-2147483647), (2147483647)) ids(id) WHERE NOT EXISTS (SELECT 1 FROM Tag WHERE TagID = id)'
    $fields = @{ Title = "$prefix Title"; Headline = "$prefix Headline"; Content = "$prefix Content"; Source = 'Assignment QA'; Status = 'false'; TagIds = $tagIds; __RequestVerificationToken = $token }
    $invalid = $fields.Clone()
    $invalid.CategoryId = $missingCategory
    $response = Post-Form $staff '/StaffNews/Create' $invalid
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('category no longer exists')) 'Create rejects a nonexistent category'
    $invalid = $fields.Clone()
    $invalid.TagIds = @($tags[0].Id, $missingTag)
    $response = Post-Form $staff '/StaffNews/Create' $invalid
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('tags no longer exist')) 'Create rejects nonexistent tags'

    $categoryPage = Get-Page $staff '/Category/Create'
    $response = Post-Form $staff '/Category/Create' @{ Name = "$prefix Category"; Description = 'Temporary news fixture'; IsActive = 'true'; __RequestVerificationToken = (Get-Token $categoryPage.Body) }
    if ($response.Status -ne 200) { throw 'Unable to create temporary category.' }
    $categoryId = [int](Read-Scalar 'SELECT CategoryID FROM Category WHERE CategoryName = @name' @{ '@name' = "$prefix Category" })
    $fields.CategoryId = $categoryId
    $fields.CreatedById = $staffAccounts[1].Id
    $fields.CreatedDate = '2000-01-01'
    $fields.UpdatedById = $staffAccounts[1].Id
    $fields.ModifiedDate = '2000-01-01'
    $fields.Id = 'client-supplied-id'
    $fields.TagIds = @($tags[0].Id, $tags[0].Id, $tags[1].Id)
    $response = Post-Form $staff '/StaffNews/Create' $fields
    Assert-Check ($response.Status -eq 200 -and ($response.Body | ConvertFrom-Json).success) 'Staff can create an inactive article with multiple tags'
    $articleId = [string](Read-Scalar 'SELECT NewsArticleID FROM NewsArticle WHERE Headline = @headline' @{ '@headline' = $fields.Headline })
    $articleParameters = @{ '@id' = $articleId }
    Assert-Check ($articleId.Length -eq 20 -and $articleId -ne 'client-supplied-id') 'Article ID is generated by the server within the schema length'
    $author = Read-Scalar 'SELECT CreatedByID FROM NewsArticle WHERE NewsArticleID = @id' $articleParameters
    $createdAt = Read-Scalar 'SELECT CreatedDate FROM NewsArticle WHERE NewsArticleID = @id' $articleParameters
    Assert-Check ([int]$author -eq $staffAccounts[0].Id -and [datetime]$createdAt -gt [datetime]'2020-01-01') 'Create ignores forged author and date fields'
    Assert-Check ([int](Read-Scalar 'SELECT COUNT(*) FROM NewsArticle WHERE NewsArticleID = @id AND UpdatedByID IS NULL AND ModifiedDate IS NULL' $articleParameters) -eq 1) 'A new article has no forged modification metadata'
    Assert-Check ([int](Read-Scalar 'SELECT COUNT(*) FROM NewsTag WHERE NewsArticleID = @id' $articleParameters) -eq 2) 'Duplicate tag selections produce only distinct links'
    $public = Get-Page $guest '/News'
    Assert-Check ($public.Status -eq 200 -and -not $public.Body.Contains($prefix)) 'Inactive article is hidden from guests'
    $detail = Get-Page $staff "/StaffNews/Details/$articleId"
    Assert-Check ($detail.Status -eq 200 -and $detail.Body.Contains($fields.Content)) 'Staff can read inactive article details'
    $edit = Get-Page $staff "/StaffNews/Edit/$articleId"
    Assert-Check ($edit.Status -eq 200 -and [regex]::Matches($edit.Body, 'checked="checked"').Count -eq 2) 'Edit modal preselects the assigned tags'
    $search = Get-Page $staff ('/StaffNews?search=' + [Uri]::EscapeDataString($prefix))
    Assert-Check ($search.Body.Contains('1 article found')) 'Search finds the new article'
    $search = Get-Page $staff ('/StaffNews?search=' + [Uri]::EscapeDataString($tags[0].Name))
    Assert-Check ($search.Body.Contains($fields.Title)) 'Search can match an article tag'
    $filter = Get-Page $staff "/StaffNews?categoryId=$categoryId&status=false"
    Assert-Check ($filter.Body.Contains($fields.Title)) 'Category and inactive status filters include the article'
    $filter = Get-Page $staff "/StaffNews?categoryId=$categoryId&status=true"
    Assert-Check ($filter.Body.Contains('No articles match')) 'Active filter excludes the inactive article'

    $editorPage = Get-Page $editor "/StaffNews/Edit/$articleId"
    $updates = $fields.Clone()
    $updates.Id = $articleId
    $updates.Title = "$prefix Updated"
    $updates.Status = 'true'
    $updates.TagIds = @($tags[1].Id)
    $updates.Content = "<script>alert('$prefix')</script>"
    $updates.__RequestVerificationToken = Get-Token $editorPage.Body
    $updates.CreatedById = $staffAccounts[1].Id
    $updates.UpdatedById = $staffAccounts[0].Id
    $response = Post-Form $editor '/StaffNews/Edit' $updates
    Assert-Check ($response.Status -eq 200 -and ($response.Body | ConvertFrom-Json).success) 'Another Staff member can update an article and remove a tag'
    $newAuthor = Read-Scalar 'SELECT CreatedByID FROM NewsArticle WHERE NewsArticleID = @id' $articleParameters
    $newCreatedAt = Read-Scalar 'SELECT CreatedDate FROM NewsArticle WHERE NewsArticleID = @id' $articleParameters
    Assert-Check ([int]$newAuthor -eq [int]$author -and [datetime]$newCreatedAt -eq [datetime]$createdAt) 'Edit preserves original author and creation date'
    $updatedBy = Read-Scalar 'SELECT UpdatedByID FROM NewsArticle WHERE NewsArticleID = @id' $articleParameters
    $modifiedAt = Read-Scalar 'SELECT ModifiedDate FROM NewsArticle WHERE NewsArticleID = @id' $articleParameters
    Assert-Check ([int]$updatedBy -eq $staffAccounts[1].Id -and [datetime]$modifiedAt -ge [datetime]$createdAt) 'Edit records the actual editor and server time'
    Assert-Check ([int](Read-Scalar 'SELECT COUNT(*) FROM NewsTag WHERE NewsArticleID = @id' $articleParameters) -eq 1) 'Edit removes the unchecked tag association'
    $public = Get-Page $guest '/News'
    Assert-Check ($public.Body.Contains($updates.Title) -and $public.Body.Contains('&lt;script&gt;') -and -not $public.Body.Contains($updates.Content)) 'Active article becomes public and content is HTML-escaped'

    $invalid = $updates.Clone()
    $invalid.Title = "$prefix ShouldNotSave"
    $invalid.TagIds = @($missingTag)
    $response = Post-Form $editor '/StaffNews/Edit' $invalid
    Assert-Check ($response.Status -eq 422) 'Edit rejects deleted or nonexistent tags'
    $savedTitle = Read-Scalar 'SELECT NewsTitle FROM NewsArticle WHERE NewsArticleID = @id' $articleParameters
    Assert-Check ($savedTitle -eq $updates.Title -and [int](Read-Scalar 'SELECT COUNT(*) FROM NewsTag WHERE NewsArticleID = @id' $articleParameters) -eq 1) 'Invalid tag edit leaves article and tag links unchanged'
    $updates.Remove('TagIds')
    $response = Post-Form $editor '/StaffNews/Edit' $updates
    Assert-Check ($response.Status -eq 200 -and [int](Read-Scalar 'SELECT COUNT(*) FROM NewsTag WHERE NewsArticleID = @id' $articleParameters) -eq 0) 'Unchecking all tags removes all article links'
    $updates.TagIds = $tagIds
    $updates.Status = 'false'
    $response = Post-Form $editor '/StaffNews/Edit' $updates
    Assert-Check ($response.Status -eq 200 -and [int](Read-Scalar 'SELECT COUNT(*) FROM NewsTag WHERE NewsArticleID = @id' $articleParameters) -eq 2) 'Tags can be added back while making the article inactive'
    $public = Get-Page $guest '/News'
    Assert-Check (-not $public.Body.Contains($prefix)) 'Deactivating an article removes it from public news'

    foreach ($identity in @('Guest', 'Lecturer', 'Admin')) {
        $client = New-Client
        try {
            if ($identity -eq 'Lecturer') { Login-As $client $lecturer }
            if ($identity -eq 'Admin') { Login-As $client @{ Email = $settings.AdminAccount.Email; Password = $settings.AdminAccount.Password } }
            $destination = if ($identity -eq 'Guest') { '/Account/Login' } else { '/Account/AccessDenied' }
            foreach ($path in @('/StaffNews', '/StaffNews/Create', "/StaffNews/Edit/$articleId", "/StaffNews/Details/$articleId")) {
                $blocked = Get-Page $client $path
                Assert-Check ($blocked.Status -eq 302 -and $blocked.Location.Contains($destination)) "$identity is blocked from GET $path"
            }
            foreach ($action in @('Create', 'Edit', 'Delete')) {
                $blocked = Post-Form $client "/StaffNews/$action" @{}
                Assert-Check ($blocked.Status -eq 302 -and $blocked.Location.Contains($destination)) "$identity is blocked from POST $action"
            }
            if ($identity -eq 'Lecturer') {
                $public = Get-Page $client '/News'
                Assert-Check ($public.Status -eq 200 -and -not $public.Body.Contains($prefix)) 'Lecturer also sees only active public news'
            }
        } finally { $client.Dispose() }
    }
    $response = Post-Form $staff '/StaffNews/Delete' @{ id = $articleId; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 200 -and ($response.Body | ConvertFrom-Json).success) 'Staff can delete an article with tag associations'
    Assert-Check ([int](Read-Scalar 'SELECT COUNT(*) FROM NewsTag WHERE NewsArticleID = @id' $articleParameters) -eq 0) 'Delete removes all NewsTag links'
    Assert-Check ([int](Read-Scalar 'SELECT COUNT(*) FROM Tag WHERE TagID IN (@first, @second)' @{ '@first' = $tags[0].Id; '@second' = $tags[1].Id }) -eq 2) 'Delete preserves the shared tags'
    $missing = Get-Page $staff "/StaffNews/Edit/$articleId"
    Assert-Check ($missing.Status -eq 404) 'Deleted article cannot be opened for editing'
    $response = Post-Form $editor '/StaffNews/Edit' $updates
    Assert-Check ($response.Status -eq 422 -and $response.Body.Contains('no longer exists')) 'Editing a deleted article reports a useful error'
    $response = Post-Form $staff '/StaffNews/Delete' @{ id = $articleId; __RequestVerificationToken = $token }
    Assert-Check ($response.Status -eq 422) 'Repeated deletion reports a useful error'
} finally {
    if ($connection.State -eq 'Open') {
        $cleanup = $connection.CreateCommand()
        $cleanup.CommandText = 'DELETE nt FROM NewsTag nt INNER JOIN NewsArticle n ON n.NewsArticleID = nt.NewsArticleID WHERE n.Headline LIKE @prefix; DELETE FROM NewsArticle WHERE Headline LIKE @prefix; DELETE FROM Category WHERE CategoryName = @categoryName'
        $null = $cleanup.Parameters.AddWithValue('@prefix', "$prefix%")
        $null = $cleanup.Parameters.AddWithValue('@categoryName', "$prefix Category")
        $null = $cleanup.ExecuteNonQuery()
    }
    $connection.Dispose()
    $staff.Dispose()
    $editor.Dispose()
    $guest.Dispose()
}
Write-Output "Completed $script:checks news checks. Temporary articles and category were cleaned up; existing tags were preserved."
