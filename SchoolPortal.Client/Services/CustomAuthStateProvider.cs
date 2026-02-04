using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;

namespace SchoolPortal.Client.Services;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly IAuthService _authService;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public CustomAuthStateProvider(IAuthService authService, HttpClient httpClient, IConfiguration configuration)
    {
        _authService = authService;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            var useMockApi = _configuration.GetValue<bool>("UseMockApi");

            if (useMockApi)
            {
                var mockClaims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, "00000000-0000-0000-0000-000000000001"),
                    new Claim(ClaimTypes.Email, "mock@demo.com"),
                    new Claim(ClaimTypes.Role, "Admin"),
                    new Claim("schoolId", "00000000-0000-0000-0000-000000000099")
                };
                var mockIdentity = new ClaimsIdentity(mockClaims, "mock");
                var mockUser = new ClaimsPrincipal(mockIdentity);
                return new AuthenticationState(mockUser);
            }

            var token = await _authService.GetTokenAsync();

            if (string.IsNullOrWhiteSpace(token))
            {
                return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
            }

            // Set authorization header
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            // Parse JWT token to get claims
            var claims = ParseClaimsFromJwt(token);
            var identity = new ClaimsIdentity(claims, "jwt");
            var user = new ClaimsPrincipal(identity);

            return new AuthenticationState(user);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Auth Error] GetAuthenticationStateAsync failed: {ex}");
            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }
    }

    public void NotifyUserAuthentication(string token)
    {
        var claims = ParseClaimsFromJwt(token);
        var identity = new ClaimsIdentity(claims, "jwt");
        var user = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    public void NotifyUserLogout()
    {
        var identity = new ClaimsIdentity();
        var user = new ClaimsPrincipal(identity);

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(user)));
    }

    private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(jwt))
            {
                return Enumerable.Empty<Claim>();
            }

            jwt = jwt.Trim();

            if (jwt.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                jwt = jwt.Substring(7).Trim();
            }

            var parts = jwt.Split('.');
            if (parts.Length != 3)
            {
                Console.Error.WriteLine($"[Auth Error] Invalid JWT format: expected 3 parts, got {parts.Length}");
                return Enumerable.Empty<Claim>();
            }

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwt);
            return token.Claims;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[Auth Error] ParseClaimsFromJwt failed: {ex}");
            return Enumerable.Empty<Claim>();
        }
    }
}
