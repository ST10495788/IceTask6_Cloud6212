using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Ice_Task3_NotesAPI.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Security.Claims;
using Ice_Task3_NotesAPI.Services;
using Microsoft.Extensions.Logging;

namespace Ice_Task3_NotesAPI.Functions;

// All refernces are in program.cs file

public class AddAttachment
{
    private readonly ILogger<AddAttachment> _logger;
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly JwtTokenService _jwtTokenService;

    private const string NoteEntity = "Notes";
    private const string AttachEntity = "Attachments";
    private const string NotePartitionKey = "NotePartition";
    private const string BlobContainerName = "note-attachments";
    public AddAttachment(
        ILogger<AddAttachment> logger,
        TableServiceClient tableServiceClient,
        BlobServiceClient blobServiceClient,
        JwtTokenService jwtTokenService)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
        _blobServiceClient = blobServiceClient;
        _jwtTokenService = jwtTokenService;
    }


    [Function("AddAttachment")]
    public async Task<IActionResult> AddAttachmentToNote(
        [HttpTrigger( AuthorizationLevel.Anonymous, "post", Route = "notes/{noteId}/attachments")] HttpRequest request, string noteId)
    {

        string? authorizationHeader = request.Headers["Authorization"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith( "Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return new UnauthorizedObjectResult( "A valid Bearer token is required.");
        }

        string token = authorizationHeader["Bearer ".Length..].Trim();

        var principal = _jwtTokenService.ValidateToken(token);

        if (principal is null)
        {
            return new UnauthorizedObjectResult("The token is invalid or has expired.");
        }

        string? userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
        {
            return new UnauthorizedObjectResult("The token does not contain a user ID.");
        }


        _logger.LogInformation( $"Uploading attachment for note {noteId}");

        try
        {
            // Checking whether the note exists
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

            if (!request.HasFormContentType)
            {
                return new BadRequestObjectResult(new
                {
                    message = "The request must contain a file."
                });
            }

            var form = await request.ReadFormAsync();

            var file = form.Files.GetFile("file");

            // Validating the uploaded file
            if (file == null || file.Length == 0)
            {
                return new BadRequestObjectResult(new
                {
                    message = "A valid file is required."
                });
            }

            var containerClient = _blobServiceClient.GetBlobContainerClient( BlobContainerName);

            await containerClient.CreateIfNotExistsAsync();

            var attachmentId = Guid.NewGuid().ToString();
            var safeFileName = Path.GetFileName(file.FileName);
            var blobName = $"{noteId}/{attachmentId}-{safeFileName}";

            var blobClient = containerClient.GetBlobClient(blobName);

            // Upload ing the file
            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync( stream,  new BlobHttpHeaders
                {
                    ContentType = file.ContentType
                });

            var attachmentsTable = _tableServiceClient.GetTableClient(AttachEntity);

            await attachmentsTable.CreateIfNotExistsAsync();

            // Storing attachment metadata
            var attachment = new Attachment
            {
                Id = attachmentId,
                FileName = safeFileName,
                ContentType = file.ContentType,
                Url = blobClient.Uri.ToString(),
                BlobName = blobName,
                PartitionKey = noteId,

                RowKey = attachmentId
            };

            await attachmentsTable.AddEntityAsync(attachment);
            return new ObjectResult(new
            {
                id = attachment.Id,
                fileName = attachment.FileName,
                contentType = attachment.ContentType,
                url = attachment.Url
            })
            {
                StatusCode = StatusCodes.Status201Created
            };
        }
        catch (Exception ex)
        {
            _logger.LogError( $"Could not upload attachment. Error: {ex.Message}");

            return new StatusCodeResult( StatusCodes.Status500InternalServerError);
        }
    }
}