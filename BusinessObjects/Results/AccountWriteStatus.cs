namespace BusinessObjects.Results;

public enum AccountWriteStatus
{
    Success,
    NotFound,
    DuplicateEmail,
    WrongPassword,
    InUse,
    IdLimitReached
}
