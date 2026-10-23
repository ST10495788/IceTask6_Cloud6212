using System.Net;
using System.Text.Json;
using Ice_Task3_NotesAPI.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace Ice_Task3_NotesAPI.Functions;

public class RegisterUser
{
    private readonly AuthService _authService;

    public RegisterUser(AuthService authService)
    {
        _authService = authService;
    }

    [Function("RegisterUser")]
    public async Task<HttpResponseData> Run([HttpTrigger( AuthorizationLevel.Anonymous,"post", Route = "auth/register")] HttpRequestData req)
    {
        var response = req.CreateResponse();

        try
        {
            var body = await JsonSerializer.DeserializeAsync<RegisterRequest>( req.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (body is null ||
                string.IsNullOrWhiteSpace(body.DisplayName) ||
                string.IsNullOrWhiteSpace(body.Email) ||
                string.IsNullOrWhiteSpace(body.Password))
            {
                response.StatusCode = HttpStatusCode.BadRequest;
                await response.WriteStringAsync("DisplayName, Email and Password are required.");
                return response;
            }

            var user = await _authService.RegisterAsync( body.DisplayName, body.Email, body.Password);

            response.StatusCode = HttpStatusCode.Created;
            await response.WriteAsJsonAsync(new
            {
                user.UserId,
                user.DisplayName,
                user.Email,
                Message = "Account created successfully."
            });

            return response;
        }
        catch (InvalidOperationException ex)
        {
            response.StatusCode = HttpStatusCode.Conflict;
            await response.WriteStringAsync(ex.Message);
            return response;
        }
    }

    public class RegisterRequest
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
