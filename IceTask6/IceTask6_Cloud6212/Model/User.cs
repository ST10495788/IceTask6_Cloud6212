using Azure;
using Azure.Data.Tables;

namespace Ice_Task3_NotesAPI.Model;

public class User : ITableEntity
{
    public string UserId { get; set; } = Guid.NewGuid().ToString();

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string PartitionKey { get; set; } = "Users";

    public string RowKey { get; set; } = string.Empty;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}
