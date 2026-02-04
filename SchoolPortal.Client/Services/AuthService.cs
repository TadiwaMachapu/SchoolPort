using System.Net.Http.Json;
using Blazored.LocalStorage;
using SchoolPortal.Shared.DTOs.Auth;

namespace SchoolPortal.Client.Services;

public class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private const string TokenKey = "authToken";
    private const string UserKey = "currentUser";

    public AuthService(HttpClient httpClient, ILocalStorageService localStorage)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
    }

    public async Task<LoginResponse?> LoginAsync(LoginRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/login", request);
        
        if (response.IsSuccessStatusCode)
        {
            var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
            
            if (loginResponse != null)
            {
                await _localStorage.SetItemAsync(TokenKey, loginResponse.AccessToken);
                await _localStorage.SetItemAsync(UserKey, loginResponse.User);
                
                Console.WriteLine($"[Auth] Saved authToken length: {loginResponse.AccessToken.Length}");
                
                // Set default authorization header
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginResponse.AccessToken);
            }
            
            return loginResponse;
        }
        
        return null;
    }

    public async Task LogoutAsync()
    {
        await _localStorage.RemoveItemAsync(TokenKey);
        await _localStorage.RemoveItemAsync(UserKey);
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<string?> GetTokenAsync()
    {
        try
        {
            return await _localStorage.GetItemAsync<string>(TokenKey);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Auth Error] GetTokenAsync failed: {ex}");
            return null;
        }
    }

    public async Task<UserInfo?> GetCurrentUserAsync()
    {
        try
        {
            return await _localStorage.GetItemAsync<UserInfo>(UserKey);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Auth Error] GetCurrentUserAsync failed: {ex}");
            return null;
        }
    }
}
