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
public class GetAllNotes
{
    private readonly ILogger<GetAllNotes> _logger;
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtTokenService _jwtTokenService;

    private const string NoteEntity = "Notes";
    private const string AttachEntity = "Attachments";
    private const string NotePartitionKey = "NotePartition";

    public GetAllNotes( ILogger<GetAllNotes> logger, TableServiceClient tableServiceClient, JwtTokenService jwtTokenService)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
        _jwtTokenService = jwtTokenService;
    }

    [Function("GetAllNotes")]
    public async Task<IActionResult> GetNotes([HttpTrigger( AuthorizationLevel.Anonymous, "get", Route = "notes")] HttpRequest request)
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
            return new UnauthorizedObjectResult("The token does not contain a user ID.");
        }

        _logger.LogInformation( "Getting all notes");

        try
        {
            var notesTable = _tableServiceClient.GetTableClient(NoteEntity);

            var attachmentsTable = _tableServiceClient.GetTableClient( AttachEntity);

            await notesTable.CreateIfNotExistsAsync();
            await attachmentsTable.CreateIfNotExistsAsync();

            var result = new List<object>();

            await foreach (var note in notesTable.QueryAsync<Note>( n => n.PartitionKey == NotePartitionKey && n.UserId == userId))
            {
                var attachments = new List<object>();

                await foreach ( var attachment in attachmentsTable .QueryAsync<Attachment>( a => a.PartitionKey == note.RowKey))
                {
                    attachments.Add(new
                    {
                        id = attachment.Id, fileName = attachment.FileName,
                        contentType = attachment.ContentType,
                        url = attachment.Url
                    });
                }

                result.Add(new
                {
                    id = note.Id,
                    title = note.Title,
                    content = note.Content,
                    createdAt = note.CreatedAt,
                    updatedAt = note.UpdatedAt,
                    attachmentCount = attachments.Count,
                    attachments = attachments
                });
            }

            return new OkObjectResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError( $"Could not get notes. Error: {ex.Message}");

            return new StatusCodeResult( StatusCodes.Status500InternalServerError);
        }
    }
}