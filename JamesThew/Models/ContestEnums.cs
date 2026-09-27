namespace JamesThew.Models;

public enum ContestType
{
    Recipe = 1,
    Tip = 2
}

public enum ContestStatus
{
    Draft = 1,
    Published = 2,
    Closed = 3,
    Archived = 4
}

public enum ContestTimelinePhase
{
    Upcoming = 1,
    Open = 2,
    Ended = 3
}
