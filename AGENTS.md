# AGENTS.md --- PRN222 Assignment 01

## 1. Project Overview

This repository is for **PRN222 Assignment 01 ---
FUNewsManagementSystem**.

The application is a **News Management System** built with:

-   ASP.NET Core MVC
-   .NET 8
-   Entity Framework Core 8
-   SQL Server
-   Database First
-   LINQ
-   Class Library projects
-   Repository pattern
-   Singleton pattern where appropriate

The solution is currently named:

`NguyenMinhHuy_SE19C_A01`

Main MVC project:

`NguyenMinhHuyMVC`

Database:

`FUNewsManagement`

------------------------------------------------------------------------

## 2. Mandatory Assignment Architecture

Database access must follow this direction:

``` text
View
  ↓
Controller
  ↓
Service
  ↓
Repository
  ↓
DAO
  ↓
FUNewsManagementContext
  ↓
SQL Server
```

**Important rules:**

1.  Controllers must NOT access `FUNewsManagementContext` directly in
    production code.
2.  Controllers must NOT query the database directly.
3.  All database access must go through:
    -   Service
    -   Repository
    -   Data Access Object (DAO)
4.  The database connection string must come from `appsettings.json`.
5.  Do not bypass layers just to make implementation faster.
6.  Keep responsibilities separated between projects.
7.  Use dependency injection where appropriate.
8.  Use async EF Core operations where practical.
9.  Preserve the assignment's database schema unless a change is
    explicitly required.
10. Before making major structural changes, inspect the existing code
    first.

A temporary Controller → DbContext call is acceptable only for a short
connection test and must be removed afterward.

------------------------------------------------------------------------

## 3. Solution Structure

Current intended structure:

``` text
NguyenMinhHuy_SE19C_A01
│
├── NguyenMinhHuyMVC
│   ├── Controllers
│   ├── Models / ViewModels
│   ├── Views
│   ├── wwwroot
│   ├── Program.cs
│   └── appsettings.json
│
├── BusinessObjects
│   └── Models
│       ├── Category.cs
│       ├── NewsArticle.cs
│       ├── SystemAccount.cs
│       ├── Tag.cs
│       └── NewsTag.cs (if generated/required)
│
├── DataAccessObjects
│   ├── Context
│   │   └── FUNewsManagementContext.cs
│   └── DAO classes
│
├── Repositories
│   ├── Interfaces
│   └── Implementations
│
└── Services
    ├── Interfaces
    └── Implementations
```

Some default `Class1.cs` files may still exist and can be removed when
they are no longer needed.

------------------------------------------------------------------------

## 4. Current Namespace Convention

Entity classes belong to:

``` csharp
namespace BusinessObjects.Models;
```

The EF Core context belongs to:

``` csharp
namespace DataAccessObjects.Context;
```

Do not place the scaffolded entities or DbContext under the
`NguyenMinhHuyMVC` namespace.

Expected dependency direction:

``` text
NguyenMinhHuyMVC
      ↓
   Services
      ↓
 Repositories
      ↓
DataAccessObjects
      ↓
BusinessObjects
```

Avoid circular references.

------------------------------------------------------------------------

## 5. Current Project Status

Already completed:

-   Solution with 5 projects created.
-   .NET 8 selected.
-   SQL Server database `FUNewsManagement` created from the supplied SQL
    script.
-   EF Core 8 packages installed.
-   Database First scaffold performed.
-   Entity classes moved/generated under `BusinessObjects/Models`.
-   Entity namespaces corrected to `BusinessObjects.Models`.
-   `FUNewsManagementContext` is under `DataAccessObjects/Context`.
-   Context namespace corrected to `DataAccessObjects.Context`.
-   `DataAccessObjects` references `BusinessObjects`.
-   MVC can reference the context for DI registration.
-   Connection string is configured through `appsettings.json`.
-   `FUNewsManagementContext` is registered with ASP.NET Core DI in
    `Program.cs`.
-   The solution currently builds successfully with **0 build errors**.

Do not redo the project setup unless there is a concrete reason.

------------------------------------------------------------------------

## 6. EF Core Configuration

The MVC application's `Program.cs` registers the DbContext approximately
as follows:

``` csharp
using DataAccessObjects.Context;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<FUNewsManagementContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));
```

The exact SQL Server instance may vary by local machine.

Typical `appsettings.json` structure:

``` json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=FUNewsManagement;Trusted_Connection=True;TrustServerCertificate=True"
  },
  "AdminAccount": {
    "Email": "admin@FUNewsManagementSystem.org",
    "Password": "@@abc123@@"
  }
}
```

If the machine uses SQL Express, the server may instead be
`.\SQLEXPRESS`.

Do not hardcode the database connection string inside
`FUNewsManagementContext`.

------------------------------------------------------------------------

## 7. Database Schema

### Category

Important fields:

``` text
CategoryID          smallint, identity, PK
CategoryName        nvarchar(100), required
CategoryDesciption  nvarchar(250), required
ParentCategoryID    smallint, nullable
IsActive            bit, nullable
```

Note: the supplied database intentionally spells the field as:

`CategoryDesciption`

Do not silently rename the database column without considering the
existing schema.

### NewsArticle

``` text
NewsArticleID   nvarchar(20), PK
NewsTitle       nvarchar(400), nullable
Headline        nvarchar(150), required
CreatedDate     datetime, nullable
NewsContent     nvarchar(4000), nullable
NewsSource      nvarchar(400), nullable
CategoryID      smallint, nullable
NewsStatus      bit, nullable
CreatedByID     smallint, nullable
UpdatedByID     smallint, nullable
ModifiedDate    datetime, nullable
```

### SystemAccount

``` text
AccountID        smallint, PK
AccountName      nvarchar(100), nullable
AccountEmail     nvarchar(70), nullable
AccountRole      int, nullable
AccountPassword  nvarchar(70), nullable
```

`AccountID` is not an identity column in the supplied script.

### Tag

``` text
TagID     int, PK
TagName   nvarchar(50), nullable
Note      nvarchar(400), nullable
```

### NewsTag

Join table:

``` text
NewsArticleID  nvarchar(20), PK/FK
TagID          int, PK/FK
```

The composite primary key is:

``` text
(NewsArticleID, TagID)
```

------------------------------------------------------------------------

## 8. Roles

Role values from the assignment:

``` text
Staff     = 1
Lecturer  = 2
Admin     = configured in appsettings.json
```

The default administrator is not one of the normal database accounts.

Default admin credentials required by the assignment:

``` text
Email:    admin@FUNewsManagementSystem.org
Password: @@abc123@@
```

These credentials must be read from `appsettings.json`, not inserted
into `SystemAccount`.

Seed data includes lecturer and staff accounts. Their supplied
development passwords are plain text in the provided SQL script; do not
introduce a password-hashing migration unless explicitly requested,
because it could diverge from the assignment data and expected login
behavior.

------------------------------------------------------------------------

## 9. Functional Requirements

### Guest

No authentication is required to view news articles whose status is
active.

``` text
NewsStatus = 1
```

Guests must not receive staff/admin management privileges.

### Lecturer

Lecturer authenticates using email and password.

Lecturer may view active news articles.

### Admin

Admin must be able to:

-   Log in using the admin credentials from `appsettings.json`.
-   Manage account information.
-   Read accounts.
-   Create accounts.
-   Update accounts.
-   Delete accounts.
-   Search accounts.
-   Generate a statistical report between `StartDate` and `EndDate`.
-   Filter the report using the news article's `CreatedDate`.
-   Sort report data in descending order.

### Staff

Staff must be able to:

-   Log in using database email/password.
-   Manage categories.
-   Manage news articles.
-   Manage tags associated with news articles.
-   Manage his/her profile.
-   View news history created by him/her.

Category deletion rule:

A category can only be deleted if it is not being used by any news
article.

### Management UI Rules

For these management modules:

-   Account Management
-   Category Management
-   News Article Management

Required operations:

``` text
Read
Create
Update
Delete
Search
```

Additional UI requirements:

-   Create must use a popup dialog/modal.
-   Update must use a popup dialog/modal.
-   Delete must display confirmation.

------------------------------------------------------------------------

## 10. Status Fields

News:

``` text
1 = Active
0 = Inactive
```

Category:

``` text
1 = Active
0 = Inactive
```

Public/Lecturer news pages must only display active news.

------------------------------------------------------------------------

## 11. Login Behavior

The assignment requires the default UI to be the Login page/window.

Recommended login flow:

``` text
User enters Email + Password
            ↓
      LoginController
            ↓
       AccountService
            ↓
     AccountRepository
            ↓
        AccountDAO
            ↓
FUNewsManagementContext
```

However, check the configured admin credentials first or through an
authentication service because the default admin is stored in
`appsettings.json`, not the database.

Expected role routing after login:

``` text
Admin     → Admin area/dashboard
Staff     → Staff area/dashboard
Lecturer  → Active news page
```

Guests should still have a route/link to view active news without
logging in.

Do not implement authentication by allowing the MVC controller to query
`SystemAccounts` directly.

------------------------------------------------------------------------

## 12. Immediate Next Step

Continue from the current successful build.

### Step A --- Verify EF Core connection

Temporarily test that `FUNewsManagementContext` can read
`SystemAccounts`.

Once database connectivity is confirmed, remove any temporary controller
code that accesses DbContext directly.

### Step B --- Implement login layers

Implement in this order:

``` text
AccountDAO
    ↓
IAccountRepository
    ↓
AccountRepository
    ↓
IAccountService
    ↓
AccountService
    ↓
LoginController / AccountController
    ↓
Login View
```

Start with only the methods necessary for authentication.

Example responsibility split:

**AccountDAO** - EF Core/LINQ query for an account by email/password. -
Database-specific access only.

**AccountRepository** - Exposes account persistence/query operations to
the service layer. - Delegates database work to DAO.

**AccountService** - Business logic for authentication. - Coordinates DB
member login and the special admin account if appropriate. - Should not
contain Razor/UI logic.

**Controller** - Receives HTTP requests. - Validates request/view
model. - Calls service. - Creates session/auth state and redirects. -
Does not access EF Core directly.

Do not implement every CRUD module at once.

------------------------------------------------------------------------

## 13. Suggested Implementation Order

After login works:

``` text
1. Authentication + authorization/session
2. Public/Lecturer active news
3. Admin account CRUD + search
4. Staff category CRUD + search
5. Staff news CRUD + search
6. News ↔ Tag management
7. Staff profile
8. Staff news history
9. Admin StartDate/EndDate report
10. Validation and UI cleanup
11. Final testing against assignment requirements
```

Implement and test one vertical slice at a time.

------------------------------------------------------------------------

## 14. Repository and DAO Rules

Do not collapse Repository and DAO into one class because the assignment
explicitly requires database access through Service, Repository, and
Data Access Objects.

Preferred style:

``` csharp
public interface IAccountRepository
{
    Task<SystemAccount?> GetByEmailAndPasswordAsync(
        string email,
        string password);
}
```

Repository implementation delegates to an Account DAO.

DAO owns EF Core queries.

Service owns business decisions.

Controllers depend on service interfaces.

------------------------------------------------------------------------

## 15. Singleton Requirement

The assignment explicitly requires applying the Singleton pattern
somewhere in the project.

Do **not** make `FUNewsManagementContext` a Singleton.

`DbContext` should remain scoped to the request through ASP.NET Core
dependency injection.

Before implementing Singleton solely for grading compliance, inspect the
assignment/course conventions and existing code. Apply Singleton to a
safe stateless component or to the pattern expected by the course
without causing a scoped-DbContext lifetime problem.

Do not change DI lifetimes blindly.

------------------------------------------------------------------------

## 16. Validation

The assignment requires validation for fields.

Use ASP.NET Core validation and Data Annotations/ViewModels where
suitable.

Examples include:

``` csharp
[Required]
[EmailAddress]
[StringLength(...)]
```

Do not put UI-specific validation concerns into DAO classes.

Server-side validation remains required even if client-side validation
is enabled.

------------------------------------------------------------------------

## 17. Important Database Notes

The supplied SQL script creates:

``` text
FUNewsManagement
```

There is a suspicious final statement in the supplied script referring
to:

``` text
FUNewsManagementFall2024
```

This appears inconsistent with the database created earlier in the same
script.

Do not rename the working database merely because of that final leftover
statement. The project is currently targeting:

``` text
FUNewsManagement
```

------------------------------------------------------------------------

## 18. Coding Guidelines for Codex

When working on this repository:

1.  Inspect existing files before editing.
2.  Explain the next small step before making broad changes.
3.  Keep the project buildable after each step.
4.  Prefer small, reviewable changes.
5.  Do not generate the whole assignment in one pass unless explicitly
    requested.
6.  Do not bypass Service/Repository/DAO.
7.  Do not let Controllers use DbContext directly.
8.  Do not hardcode connection strings.
9.  Do not hardcode the admin credentials in controllers/services; read
    them from configuration.
10. Preserve the existing database-first entities unless modification is
    necessary.
11. Avoid unnecessary database migrations because the project uses the
    supplied database schema.
12. Keep EF Core package major version aligned with .NET 8 / EF Core 8.
13. Use LINQ for querying/sorting as required by the assignment.
14. Respect role permissions.
15. Check category usage before deletion.
16. Ensure public news queries filter `NewsStatus == true`.
17. Ensure report queries use `CreatedDate` and descending sorting.
18. Implement Create/Update management UI using modal/popup dialogs.
19. Add delete confirmation.
20. Ask before making architectural changes that conflict with this
    document.

------------------------------------------------------------------------

## 19. Current Working Goal

The immediate goal is **not** to finish the entire application
automatically.

The current goal is:

``` text
Verify DB connection
        ↓
Build AccountDAO
        ↓
Build AccountRepository
        ↓
Build AccountService
        ↓
Implement Login
        ↓
Test Admin / Staff / Lecturer behavior
```

Continue step-by-step from there.

------------------------------------------------------------------------

## 20. Definition of Done

The assignment is complete when the application satisfies all required
role behavior and management operations while maintaining:

``` text
Controller
→ Service
→ Repository
→ DAO
→ DbContext
→ SQL Server
```

and successfully supports:

-   Login/authentication.
-   Guest active-news viewing.
-   Lecturer active-news viewing.
-   Admin account CRUD/search.
-   Admin date-range report sorted descending.
-   Staff category CRUD/search with safe deletion.
-   Staff news CRUD/search with tags.
-   Staff profile management.
-   Staff-created news history.
-   Popup Create/Update.
-   Delete confirmation.
-   Field validation.
-   Connection string from `appsettings.json`.
-   Repository pattern.
-   Singleton pattern without breaking DbContext lifetime.
