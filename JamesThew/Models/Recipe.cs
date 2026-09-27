namespace JamesThew.Models;

public class Recipe
{
    public int Id { get; set; }

    public int ContentItemId { get; set; }
    public ContentItem ContentItem { get; set; } = null!;

    public int? Servings { get; set; }
    public int? PrepMinutes { get; set; }
    public int? CookMinutes { get; set; }

    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
    public ICollection<RecipeStep> Steps { get; set; } = new List<RecipeStep>();
}
