using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UnifiedNotesGateway.Controllers;

[ApiController , Route("api/auth") , AllowAnonymous]
public class AuthProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public AuthProxyController(IHttpClientFactory httpClientFactory)
    {
        _httpClient = httpClientFactory.CreateClient("NotesApi");
    }

    [HttpPost("register")]
    public Task<IActionResult> Register([FromBody] JsonElement body)
    {
        return ForwardAuthRequest("api/auth/register", body);
    }

    [HttpPost("login")]
    public Task<IActionResult> Login([FromBody] JsonElement body)
    {
        return ForwardAuthRequest("api/auth/login", body);
    }

    private async Task<IActionResult> ForwardAuthRequest(string path, JsonElement body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);

        request.Content = new StringContent(body.GetRawText(), System.Text.Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync( request, HttpContext.RequestAborted);

        var content = await response.Content.ReadAsStringAsync( HttpContext.RequestAborted);

        return new ContentResult
        {
            StatusCode = (int)response.StatusCode, Content = content, ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json"
        };
    }
}
