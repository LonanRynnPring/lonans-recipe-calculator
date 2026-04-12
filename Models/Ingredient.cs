namespace RecipeCalculator.Models;

public class Ingredient
{
    public string Name { get; set; } = "";
    public decimal PricePerUnit { get; set; }  // pence per unit/packet
    public decimal AmountUsed { get; set; }    // fraction of units used

    public decimal CostPence => PricePerUnit * AmountUsed;
    public string CostDisplay => $"£{CostPence / 100:0.00}";
}
