namespace RecipeCalculator.Models;

public class Ingredient
{
    public string Name { get; set; } = "";
    public decimal PriceGBP { get; set; }       // price in £ for the whole package
    public decimal PackageSize { get; set; }     // total size of the package
    public string Unit { get; set; } = "g";     // unit of measurement (g, kg, ml, etc.)
    public decimal AmountUsed { get; set; }     // amount used, in the same unit as PackageSize

    public decimal CostPence => PackageSize > 0 ? (PriceGBP * 100m * AmountUsed / PackageSize) : 0;
    public string CostDisplay => $"£{CostPence / 100:0.00}";
}
