
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace UnifiedNotesGateway.Controllers;

[ApiController]
[Route("api/notes") , Authorize]
public class NotesProxyController : ControllerBase
{
    private readonly HttpClient _httpClient;

    public NotesProxyController(IHttpClientFactory factory)
    {
        _httpClient = factory.CreateClient("NotesApi");
    }

    [HttpGet]
    public Task<IActionResult> GetAllNotes() => ForwardAsync(HttpMethod.Get, "api/notes");


    [HttpPut("{noteId}")  ,  Consumes("application/json")]
    public Task<IActionResult> UpdateNote( string noteId, [FromBody] UpdateNoteRequest note) =>
    ForwardAsync(HttpMethod.Put, $"api/notes/{Uri.EscapeDataString(noteId)}");

    [HttpGet("{noteId}")]
    public Task<IActionResult> GetNoteById(string noteId) => ForwardAsync(HttpMethod.Get, $"api/notes/{Uri.EscapeDataString(noteId)}");

    [HttpPut("{noteId}") , Consumes("application/json")]
    public Task<IActionResult> UpdateNote(string noteId) => ForwardAsync(HttpMethod.Put, $"api/notes/{Uri.EscapeDataString(noteId)}");

    [HttpDelete("{noteId}")]
    public Task<IActionResult> DeleteNote(string noteId) => ForwardAsync(HttpMethod.Delete, $"api/notes/{Uri.EscapeDataString(noteId)}");

    [HttpPost("{noteId}/attachments")]
    [Consumes("multipart/form-data")]
    public Task<IActionResult> AddAttachment( string noteId,
        [FromForm] AttachmentUploadRequest upload) =>
        ForwardAsync( HttpMethod.Post, $"api/notes/{Uri.EscapeDataString(noteId)}/attachments");

    [HttpDelete("{noteId}/attachments/{attachmentId}")]
    public Task<IActionResult> DeleteAttachment(string noteId, string attachmentId) =>
        ForwardAsync( HttpMethod.Delete, $"api/notes/{Uri.EscapeDataString(noteId)}/attachments/" + $"{Uri.EscapeDataString(attachmentId)}");

    private async Task<IActionResult> ForwardAsync(HttpMethod method, string path)
    {
        var targetUri = new Uri(_httpClient.BaseAddress!, path + Request.QueryString);

        using var outgoing = new HttpRequestMessage(method, targetUri);

        if (!string.IsNullOrWhiteSpace(Request.Headers.Authorization))
        {
            outgoing.Headers.TryAddWithoutValidation("Authorization", Request.Headers.Authorization.ToString());
        }

        if (Request.ContentLength > 0 || Request.Headers.ContainsKey("Transfer-Encoding"))
        {
            outgoing.Content = new StreamContent(Request.Body);

            if (!string.IsNullOrWhiteSpace(Request.ContentType))
            {
                outgoing.Content.Headers.TryAddWithoutValidation( "Content-Type", Request.ContentType);
            }
        }

        using var response = await _httpClient.SendAsync(outgoing, HttpCompletionOption.ResponseHeadersRead, HttpContext.RequestAborted);

        var body = await response.Content.ReadAsByteArrayAsync(HttpContext.RequestAborted);

        if (response.Content.Headers.ContentType is not null)
        {
            Response.ContentType = response.Content.Headers.ContentType.ToString();
        }

        Response.StatusCode = (int)response.StatusCode;

        return new FileContentResult(body, Response.ContentType ?? "application/octet-stream");
    
    }

    public class CreateNoteRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    public class AttachmentUploadRequest
    {
        public IFormFile? File { get; set; }
    }

    public class UpdateNoteRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }
}
