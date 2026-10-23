using System.Net;
using System.Text.Json;
using Ice_Task3_NotesAPI.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Ice_Task3_NotesAPI.Functions;

public class LoginUser
{
    private readonly AuthService _authService;
    public LoginUser(AuthService authService)
    {
        _authService = authService;
    }

    [Function("LoginUser")]
    public async Task<HttpResponseData> Run([HttpTrigger( AuthorizationLevel.Anonymous, "post", Route = "auth/login")] HttpRequestData req)
    {
        var response = req.CreateResponse();

        var body = await JsonSerializer.DeserializeAsync<LoginRequest>( req.Body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
        {
            response.StatusCode = HttpStatusCode.BadRequest;
            await response.WriteStringAsync( "Email and Password are required.");
            return response;
        }

        var token = await _authService.LoginAsync( body.Email, body.Password);

        if (token is null)
        {
            response.StatusCode = HttpStatusCode.Unauthorized;
            await response.WriteStringAsync("Invalid email or password.");
            return response;
        }

        response.StatusCode = HttpStatusCode.OK;
        await response.WriteAsJsonAsync(new
        {
            AccessToken = token,
            TokenType = "Bearer",
            ExpiresIn = 3600
        });

        return response;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
