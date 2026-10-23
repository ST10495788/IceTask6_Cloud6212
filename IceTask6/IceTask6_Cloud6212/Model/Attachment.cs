using System;
using Azure;
using Azure.Data.Tables;

namespace Ice_Task3_NotesAPI.Model
{
    // All refernces are in program.cs file
    public class Attachment : ITableEntity
    {
        public string Id { get; set; } = string.Empty;

        public string? FileName { get; set; }

        public string? ContentType { get; set; }

        public string? Url { get; set; }

        public string? BlobName { get; set; }

        public string PartitionKey { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }
    }
}