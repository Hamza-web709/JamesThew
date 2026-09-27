namespace JamesThew.Models;

public class FaqItem
{
    public int Id { get; set; }

    public string QuestionKey { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;

    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}
