namespace JamesThew.Models;

public class RecipeStep
{
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int Position { get; set; }
    public string Instruction { get; set; } = string.Empty;
}
