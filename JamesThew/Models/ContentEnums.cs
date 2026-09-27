namespace JamesThew.Models;

public enum ContentKind
{
    Recipe = 1,
    Tip = 2
}

public enum ContentOrigin
{
    Editorial = 1,
    Community = 2
}

public enum ContentVisibility
{
    Free = 1,
    MembersOnly = 2
}

public enum PublicationStatus
{
    Draft = 1,
    Pending = 2,
    Published = 3,
    Rejected = 4
}
