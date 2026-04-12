using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.JSInterop;

namespace RecipeCalculator.Services;

public class AuthService
{
    private readonly HttpClient _http;
    private readonly IJSRuntime _js;
    private bool _initialized;

    private const string ApiKey = "AIzaSyD6N7pglO6tq877AxBIOojl5L04qhOxg3g";
    private const string AuthBase = "https://identitytoolkit.googleapis.com/v1/accounts";
    private const string TokenBase = "https://securetoken.googleapis.com/v1/token";

    public string? IdToken { get; private set; }
    public string? UserId { get; private set; }
    private string? RefreshToken { get; set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(IdToken);

    public event Action? AuthStateChanged;

    public AuthService(HttpClient http, IJSRuntime js)
    {
        _http = http;
        _js = js;
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        _initialized = true;

        IdToken = await _js.InvokeAsync<string?>("localStorage.getItem", "rc_idToken");
        UserId = await _js.InvokeAsync<string?>("localStorage.getItem", "rc_userId");
        RefreshToken = await _js.InvokeAsync<string?>("localStorage.getItem", "rc_refreshToken");

        if (!string.IsNullOrEmpty(RefreshToken))
            await RefreshTokenAsync();
    }

    public async Task<(bool Success, string? Error)> SignUpAsync(string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"{AuthBase}:signUp?key={ApiKey}",
                new { email, password, returnSecureToken = true });

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadFromJsonAsync<FirebaseErrorResponse>();
                return (false, FormatFirebaseError(err?.Error?.Message));
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            await SetAuthStateAsync(result);
            return (true, null);
        }
        catch
        {
            return (false, "Network error. Please try again.");
        }
    }

    public async Task<(bool Success, string? Error)> SignInAsync(string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                $"{AuthBase}:signInWithPassword?key={ApiKey}",
                new { email, password, returnSecureToken = true });

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadFromJsonAsync<FirebaseErrorResponse>();
                return (false, FormatFirebaseError(err?.Error?.Message));
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            await SetAuthStateAsync(result);
            return (true, null);
        }
        catch
        {
            return (false, "Network error. Please try again.");
        }
    }

    public async Task SignOutAsync()
    {
        IdToken = null;
        UserId = null;
        RefreshToken = null;
        await _js.InvokeVoidAsync("localStorage.removeItem", "rc_idToken");
        await _js.InvokeVoidAsync("localStorage.removeItem", "rc_userId");
        await _js.InvokeVoidAsync("localStorage.removeItem", "rc_refreshToken");
        AuthStateChanged?.Invoke();
    }

    private async Task RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken)) return;
        try
        {
            var content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("grant_type", "refresh_token"),
                new KeyValuePair<string, string>("refresh_token", RefreshToken)
            });

            var response = await _http.PostAsync($"{TokenBase}?key={ApiKey}", content);
            if (!response.IsSuccessStatusCode) return;

            var result = await response.Content.ReadFromJsonAsync<RefreshResponse>();
            if (result?.IdToken == null) return;

            IdToken = result.IdToken;
            RefreshToken = result.RefreshToken;
            await _js.InvokeVoidAsync("localStorage.setItem", "rc_idToken", IdToken);
            if (result.RefreshToken != null)
                await _js.InvokeVoidAsync("localStorage.setItem", "rc_refreshToken", result.RefreshToken);
        }
        catch { /* silent — user will be asked to re-login */ }
    }

    private async Task SetAuthStateAsync(AuthResponse? result)
    {
        if (result == null) return;
        IdToken = result.IdToken;
        UserId = result.LocalId;
        RefreshToken = result.RefreshToken;
        await _js.InvokeVoidAsync("localStorage.setItem", "rc_idToken", IdToken ?? "");
        await _js.InvokeVoidAsync("localStorage.setItem", "rc_userId", UserId ?? "");
        await _js.InvokeVoidAsync("localStorage.setItem", "rc_refreshToken", RefreshToken ?? "");
        AuthStateChanged?.Invoke();
    }

    private static string? FormatFirebaseError(string? code) => code switch
    {
        "EMAIL_NOT_FOUND" => "No account found with this email.",
        "INVALID_PASSWORD" => "Incorrect password.",
        "INVALID_LOGIN_CREDENTIALS" => "Invalid email or password.",
        "EMAIL_EXISTS" => "An account with this email already exists.",
        "USER_DISABLED" => "This account has been disabled.",
        "WEAK_PASSWORD : Password should be at least 6 characters" => "Password must be at least 6 characters.",
        _ => code
    };

    private record AuthResponse(
        [property: JsonPropertyName("idToken")] string? IdToken,
        [property: JsonPropertyName("localId")] string? LocalId,
        [property: JsonPropertyName("refreshToken")] string? RefreshToken);

    private record RefreshResponse(
        [property: JsonPropertyName("id_token")] string? IdToken,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken);

    private record FirebaseErrorResponse(
        [property: JsonPropertyName("error")] FirebaseError? Error);

    private record FirebaseError(
        [property: JsonPropertyName("message")] string? Message);
}
