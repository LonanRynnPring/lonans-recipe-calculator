using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using RecipeCalculator.Models;

namespace RecipeCalculator.Services;

public class FirestoreService
{
    private readonly HttpClient _http;
    private readonly AuthService _auth;

    private const string ProjectId = "lonans-recipe-calculator";
    private const string BaseUrl = $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";

    public FirestoreService(HttpClient http, AuthService auth)
    {
        _http = http;
        _auth = auth;
    }

    public async Task<List<Recipe>> GetRecipesAsync()
    {
        var request = CreateRequest(HttpMethod.Get, $"{BaseUrl}/users/{_auth.UserId}/recipes");
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return new();

        var json = await response.Content.ReadAsStringAsync();
        var listResponse = JsonSerializer.Deserialize<FirestoreListResponse>(json);
        if (listResponse?.Documents == null) return new();

        return listResponse.Documents
            .Select(DocumentToRecipe)
            .OfType<Recipe>()
            .OrderByDescending(r => r.CreatedAt)
            .ToList();
    }

    public async Task<Recipe?> GetRecipeAsync(string recipeId)
    {
        var request = CreateRequest(HttpMethod.Get, $"{BaseUrl}/users/{_auth.UserId}/recipes/{recipeId}");
        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var doc = await response.Content.ReadFromJsonAsync<FirestoreDocument>();
        return doc != null ? DocumentToRecipe(doc) : null;
    }

    public async Task<string?> SaveRecipeAsync(Recipe recipe)
    {
        var body = new
        {
            fields = new
            {
                name = new { stringValue = recipe.Name },
                servings = new { doubleValue = (double)recipe.Servings },
                ingredientsJson = new { stringValue = JsonSerializer.Serialize(recipe.Ingredients) },
                createdAt = new { timestampValue = DateTime.UtcNow.ToString("O") }
            }
        };

        var request = CreateRequest(HttpMethod.Post, $"{BaseUrl}/users/{_auth.UserId}/recipes");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        var saved = await response.Content.ReadFromJsonAsync<FirestoreDocument>();
        return saved?.Name?.Split('/').Last();
    }

    public async Task<bool> UpdateRecipeAsync(Recipe recipe)
    {
        var body = new
        {
            fields = new
            {
                name = new { stringValue = recipe.Name },
                servings = new { doubleValue = (double)recipe.Servings },
                ingredientsJson = new { stringValue = JsonSerializer.Serialize(recipe.Ingredients) },
                createdAt = new { timestampValue = recipe.CreatedAt.ToString("O") }
            }
        };

        var request = CreateRequest(HttpMethod.Patch, $"{BaseUrl}/users/{_auth.UserId}/recipes/{recipe.Id}");
        request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        var response = await _http.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteRecipeAsync(string recipeId)
    {
        var request = CreateRequest(HttpMethod.Delete, $"{BaseUrl}/users/{_auth.UserId}/recipes/{recipeId}");
        var response = await _http.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _auth.IdToken);
        return request;
    }

    private static Recipe? DocumentToRecipe(FirestoreDocument doc)
    {
        if (doc.Fields == null) return null;

        var recipe = new Recipe
        {
            Id = doc.Name?.Split('/').Last(),
            Name = doc.Fields.TryGetValue("name", out var n) ? n.StringValue ?? "" : "",
            Servings = doc.Fields.TryGetValue("servings", out var s) ? (decimal)(s.DoubleValue ?? 1) : 1,
            CreatedAt = doc.Fields.TryGetValue("createdAt", out var ca) && ca.TimestampValue != null
                ? DateTime.Parse(ca.TimestampValue)
                : DateTime.UtcNow
        };

        if (doc.Fields.TryGetValue("ingredientsJson", out var ij) && ij.StringValue != null)
            recipe.Ingredients = JsonSerializer.Deserialize<List<Ingredient>>(ij.StringValue) ?? new();

        return recipe;
    }

    // Firestore REST API shapes
    private class FirestoreListResponse
    {
        [JsonPropertyName("documents")]
        public List<FirestoreDocument>? Documents { get; set; }
    }

    private class FirestoreDocument
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("fields")]
        public Dictionary<string, FirestoreValue>? Fields { get; set; }
    }

    private class FirestoreValue
    {
        [JsonPropertyName("stringValue")]
        public string? StringValue { get; set; }

        [JsonPropertyName("doubleValue")]
        public double? DoubleValue { get; set; }

        [JsonPropertyName("timestampValue")]
        public string? TimestampValue { get; set; }
    }
}
