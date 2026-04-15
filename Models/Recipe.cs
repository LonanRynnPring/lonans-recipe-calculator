namespace RecipeCalculator.Models;

public class Recipe
{
    public string? Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Servings { get; set; }
    public List<Ingredient> Ingredients { get; set; } = new();
    public DateTime CreatedAt { get; set; }

    public decimal TotalCostPence => Ingredients.Sum(i => i.CostPence);
    public decimal CostPerServingPence => Servings > 0 ? TotalCostPence / Servings : 0;
    public string TotalCostDisplay => $"£{TotalCostPence / 100:0.00}";
    public string CostPerServingDisplay => $"£{CostPerServingPence / 100:0.00}";
}
