using Azure.Data.Tables;
using Ice_Task3_NotesAPI.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using Ice_Task3_NotesAPI.Services;

namespace Ice_Task3_NotesAPI.Functions;
// All refernces are in program.cs file
public class GetNoteById
{
    private readonly ILogger<GetNoteById> _logger;
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtTokenService _jwtTokenService;

    private const string NoteEntity = "Notes";
    private const string AttachEntity = "Attachments";
    private const string NotePartitionKey = "NotePartition";

    public GetNoteById(
        ILogger<GetNoteById> logger,
        TableServiceClient tableServiceClient, JwtTokenService jwtTokenService)
    {
        _logger = logger;
        _jwtTokenService = jwtTokenService;
        _tableServiceClient = tableServiceClient;
    }

    [Function("GetNoteById")]
    public async Task<IActionResult> GetNote([HttpTrigger( AuthorizationLevel.Anonymous,"get", Route = "notes/{noteId}")]
        HttpRequest request, string noteId)
    {

        string? authorizationHeader = request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith( "Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return new UnauthorizedObjectResult( "A valid Bearer token is required.");
        }

        string token = authorizationHeader["Bearer ".Length..].Trim();

        var principal = _jwtTokenService.ValidateToken(token);

        if (principal is null)
        {
            return new UnauthorizedObjectResult( "The token is invalid or has expired.");
        }

        string? userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return new UnauthorizedObjectResult( "The token does not contain a user ID.");
        }


        _logger.LogInformation( $"Getting note {noteId}");

        try
        {
            var notesTable = _tableServiceClient.GetTableClient(NoteEntity);

            var note = await notesTable.GetEntityIfExistsAsync<Note>( NotePartitionKey, noteId);

            if (!note.HasValue)
            {
                return new NotFoundObjectResult(new
                {
                    message = $"Note with id {noteId} does not exist."
                });
            }

            if (note.Value.UserId != userId)
            {
                return new NotFoundObjectResult(new
                {
                    message = "Note not found."
                });
            }

            var attachmentsTable = _tableServiceClient.GetTableClient( AttachEntity);

            await attachmentsTable.CreateIfNotExistsAsync();
            var attachments = new List<object>();

            await foreach ( var attachment in attachmentsTable .QueryAsync<Attachment>( a => a.PartitionKey == noteId))
            {
                attachments.Add(new
                {
                    id = attachment.Id,
                    fileName = attachment.FileName,
                    contentType = attachment.ContentType,
                    url = attachment.Url
                });
            }

            var storedNote = note.Value;

            return new OkObjectResult(new
            {
                id = storedNote.Id,
                title = storedNote.Title,
                content = storedNote.Content,
                createdAt = storedNote.CreatedAt,
                updatedAt = storedNote.UpdatedAt,
                attachmentCount = attachments.Count,
                attachments = attachments
            });
        }
        catch (Exception ex)
        {
            _logger.LogError( $"Could not retrieve note. Error: {ex.Message}");

            return new StatusCodeResult( StatusCodes.Status500InternalServerError);
        }
    }
}