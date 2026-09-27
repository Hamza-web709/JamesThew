namespace JamesThew.Models;

public class Tip
{
    public int Id { get; set; }

    public int ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
}
