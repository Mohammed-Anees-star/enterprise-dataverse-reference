namespace EnterpriseTicketing.Domain.Entities;

/// <summary>
/// File attachment for a ticket. We store only metadata here; the bytes live in blob storage
/// or as a Dataverse file column - the URL is opaque to the domain.
/// </summary>
public sealed class TicketAttachment
{
    private TicketAttachment() { }

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string StorageUri { get; private set; } = string.Empty;
    public string UploadedByUserId { get; private set; } = string.Empty;
    public DateTimeOffset UploadedAt { get; private set; }

    public static TicketAttachment Create(
        Guid ticketId,
        string fileName,
        string contentType,
        long sizeBytes,
        string storageUri,
        string uploadedByUserId,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storageUri);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sizeBytes);

        return new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            StorageUri = storageUri,
            UploadedByUserId = uploadedByUserId,
            UploadedAt = now
        };
    }
}
