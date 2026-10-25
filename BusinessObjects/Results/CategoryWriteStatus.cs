namespace BusinessObjects.Results;

public enum CategoryWriteStatus
{
    Success,
    NotFound,
    ParentNotFound,
    InvalidHierarchy,
    InUse,
    HasChildren
}
