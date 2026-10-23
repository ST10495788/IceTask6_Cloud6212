using System.Text.Json;
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
public class UpdateNote
{
    private readonly ILogger<UpdateNote> _logger;
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtTokenService _jwtTokenService;

    private const string NoteEntity = "Notes";
    private const string NotePartitionKey = "NotePartition";

    public UpdateNote(
        ILogger<UpdateNote> logger,
        TableServiceClient tableServiceClient, JwtTokenService jwtTokenService)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
        _jwtTokenService = jwtTokenService;
    }

    [Function("UpdateNote")]
    public async Task<IActionResult> UpdateNotes( [HttpTrigger( AuthorizationLevel.Anonymous,"put", Route = "notes/{noteId}")] HttpRequest request, string noteId)
    {

        string? authorizationHeader = request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
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


        _logger.LogInformation( $"API processing update for note {noteId}");

        try
        {
            var notesTable = _tableServiceClient.GetTableClient(NoteEntity);

            var noteResponse = await notesTable.GetEntityIfExistsAsync<Note>( NotePartitionKey, noteId);

            if (!noteResponse.HasValue)
            {
                return new NotFoundObjectResult(new
                {
                    message = $"Note with id {noteId} does not exist."
                });
            }

            if (noteResponse.Value.UserId != userId)
            {
                return new NotFoundObjectResult(new
                {
                    message = "Note not found."
                });
            }

            var body = await new StreamReader(request.Body).ReadToEndAsync();

            var inputNote = JsonSerializer.Deserialize<Note>(
                    body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (inputNote == null || string.IsNullOrWhiteSpace(inputNote.Title) ||  string.IsNullOrWhiteSpace(inputNote.Content))
            {
                return new BadRequestObjectResult(new
                {
                    message ="Title and content are required."
                });
            }

            var existingNote = noteResponse.Value;

            existingNote.Title = inputNote.Title;
            existingNote.Content = inputNote.Content;
            existingNote.UpdatedAt = DateTime.UtcNow;

            await notesTable.UpdateEntityAsync( existingNote, existingNote.ETag, TableUpdateMode.Replace);

            return new OkObjectResult(new
            {
                id = existingNote.Id,
                title = existingNote.Title,
                content = existingNote.Content,
                createdAt = existingNote.CreatedAt,
                updatedAt = existingNote.UpdatedAt
            });
        }
        catch (JsonException)
        {
            return new BadRequestObjectResult(new
            {
                message = "Invalid JSON data."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Could not update note. Error: {ex.Message}");

            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}