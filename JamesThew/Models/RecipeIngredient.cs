namespace JamesThew.Models;

public class RecipeIngredient
{
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;

    public int Position { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? QuantityText { get; set; }
    public string? Unit { get; set; }
}
