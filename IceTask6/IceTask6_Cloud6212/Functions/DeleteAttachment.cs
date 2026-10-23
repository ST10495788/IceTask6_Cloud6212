using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Ice_Task3_NotesAPI.Model;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Ice_Task3_NotesAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Ice_Task3_NotesAPI.Functions;
// All refernces are in program.cs file
public class DeleteAttachment
{
    private readonly ILogger<DeleteAttachment> _logger;
    private readonly TableServiceClient _tableServiceClient;
    private readonly JwtTokenService _jwtTokenService;
    private readonly BlobServiceClient _blobServiceClient;

    private const string NoteEntity = "Notes";
    private const string AttachEntity = "Attachments";
    private const string NotePartitionKey = "NotePartition";
    private const string BlobContainerName = "note-attachments";

    public DeleteAttachment(ILogger<DeleteAttachment> logger, TableServiceClient tableServiceClient,
        BlobServiceClient blobServiceClient, JwtTokenService jwtTokenService)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
        _blobServiceClient = blobServiceClient;
        _jwtTokenService = jwtTokenService;
    }


    [Function("DeleteAttachment")]
    public async Task<IActionResult> DeleteAttachmentFromNote( [HttpTrigger( AuthorizationLevel.Anonymous, "delete", Route = "notes/{noteId}/attachments/{attachmentId}")]
        HttpRequest request, string noteId, string attachmentId)
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


        _logger.LogInformation($"Deleting attachment {attachmentId} from note {noteId}");

        try
        {
            var notesTable = _tableServiceClient.GetTableClient(NoteEntity);

            var note =await notesTable.GetEntityIfExistsAsync<Note>( NotePartitionKey, noteId);

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


            var attachmentsTable =_tableServiceClient.GetTableClient(AttachEntity);

            var attachment = await attachmentsTable
                    .GetEntityIfExistsAsync<Attachment>(
                        noteId,
                        attachmentId);

            if (!attachment.HasValue)
            {
                return new NotFoundObjectResult(new
                {
                    message =$"Attachment with id {attachmentId} does not exist."
                });
            }

            var storedAttachment = attachment.Value;

            if (!string.IsNullOrWhiteSpace(storedAttachment.BlobName))
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(BlobContainerName);

                var blobClient = containerClient.GetBlobClient(storedAttachment.BlobName);

                await blobClient.DeleteIfExistsAsync();
            }

            await attachmentsTable.DeleteEntityAsync(noteId, attachmentId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError($"Could not delete attachment. Error: {ex.Message}");

            return new StatusCodeResult( StatusCodes.Status500InternalServerError);
        }
    }
}