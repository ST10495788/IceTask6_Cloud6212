using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Azure;
using Azure.Data.Tables;

namespace Ice_Task3_NotesAPI.Model
{
    // All refernces are in program.cs file
    public class Note : ITableEntity
    {
        public string Id { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string? Title { get; set; }

        public string? Content { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string PartitionKey { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        [IgnoreDataMember]
        public List<Attachment> Attachments { get; set; } = new List<Attachment>();
    }
}