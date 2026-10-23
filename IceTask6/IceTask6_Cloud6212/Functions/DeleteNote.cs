using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Ice_Task3_NotesAPI.Model;
using System.Security.Claims;
using Ice_Task3_NotesAPI.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Ice_Task3_NotesAPI.Functions;

// All refernces are in program.cs file
public class DeleteNote
{
    private readonly ILogger<DeleteNote> _logger;
    private readonly TableServiceClient _tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly JwtTokenService _jwtTokenService;

    private const string NoteEntity = "Notes";
    private const string AttachEntity = "Attachments";
    private const string NotePartitionKey = "NotePartition";
    private const string BlobContainerName = "note-attachments";
    public DeleteNote(
        ILogger<DeleteNote> logger,
        TableServiceClient tableServiceClient,
        BlobServiceClient blobServiceClient,
        JwtTokenService jwtTokenService)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
        _blobServiceClient = blobServiceClient;
        _jwtTokenService = jwtTokenService;
    }


    [Function("DeleteNote")]
    public async Task<IActionResult> DeleteNotes(
        [HttpTrigger( AuthorizationLevel.Anonymous, "delete", Route = "notes/{noteId}")] HttpRequest request, string noteId)
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


        _logger.LogInformation($"Deleting note {noteId}");

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

            var attachmentsTable = _tableServiceClient.GetTableClient(AttachEntity);

            await attachmentsTable.CreateIfNotExistsAsync();

            var containerClient = _blobServiceClient.GetBlobContainerClient(BlobContainerName);

            await foreach (var attachment in attachmentsTable .QueryAsync<Attachment>(
                        a => a.PartitionKey == noteId))
            {
                // Delete the actual blob file
                if (!string.IsNullOrWhiteSpace(
                    attachment.BlobName))
                {
                    var blobClient = containerClient.GetBlobClient(attachment.BlobName);

                    await blobClient.DeleteIfExistsAsync();
                }

                // Delete attachment 
                await attachmentsTable.DeleteEntityAsync( attachment.PartitionKey, attachment.RowKey);
            }

            // Finally deleting the note
            await notesTable.DeleteEntityAsync( NotePartitionKey, noteId);

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError( $"Could not delete note. Error: {ex.Message}");

            return new StatusCodeResult( StatusCodes.Status500InternalServerError);
        }
    }
}