using System.Text.Json;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Ice_Task3_NotesAPI.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Security.Claims;
using Ice_Task3_NotesAPI.Services;
using Microsoft.Extensions.Logging;

namespace Ice_Task3_NotesAPI.Functions;
// All refernces are in program.cs file
public class CreateNote
{
    private readonly ILogger<CreateNote> _logger;
    private readonly JwtTokenService _jwtTokenService;
    private readonly TableServiceClient _tableServiceClient;

    private const string NoteEntity = "Notes";

    public CreateNote( ILogger<CreateNote> logger,TableServiceClient tableServiceClient, JwtTokenService jwtTokenService)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
        _jwtTokenService = jwtTokenService;
    }

    [Function("CreateNote")]
    public async Task<IActionResult> CreateNotes(
        [HttpTrigger( AuthorizationLevel.Anonymous, "post", Route = "notes")] HttpRequest request)
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
        _logger.LogInformation("API processing to create notes");

        try
        {
            var reqBody = await new StreamReader(request.Body).ReadToEndAsync();

            var inputNote = JsonSerializer.Deserialize<Note>(
                    reqBody,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (inputNote == null || string.IsNullOrWhiteSpace(inputNote.Title) || string.IsNullOrWhiteSpace(inputNote.Content))
            {
                return new BadRequestObjectResult(new
                {
                    message = "Title and content are required."
                });
            }

            var tableClient = _tableServiceClient.GetTableClient(NoteEntity);

            await tableClient.CreateIfNotExistsAsync();

            var currentDate = DateTime.UtcNow;

            var noteId = Guid.NewGuid().ToString();

            var noteEntity = new Note
            {
                Id = noteId,
                UserId = userId,
                Title = inputNote.Title,
                Content = inputNote.Content,
                CreatedAt = currentDate,
                UpdatedAt = currentDate,
                PartitionKey = "NotePartition",
                RowKey = noteId
            };

            await tableClient.AddEntityAsync(noteEntity);

            return new ObjectResult(new
            {
                id = noteEntity.Id,
                title = noteEntity.Title,
                content = noteEntity.Content,
                createdAt = noteEntity.CreatedAt,
                updatedAt = noteEntity.UpdatedAt,
                attachmentCount = 0,
                attachments = new List<Attachment>()
            })
            {
                StatusCode = StatusCodes.Status201Created
            };
        }
        catch (Exception ex)
        {
            _logger.LogError( $"Could not create note. Error: {ex.Message}");

            return new StatusCodeResult( StatusCodes.Status500InternalServerError);
        }
    }
}