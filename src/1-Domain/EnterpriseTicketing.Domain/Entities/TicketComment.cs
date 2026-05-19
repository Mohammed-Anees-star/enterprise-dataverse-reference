namespace EnterpriseTicketing.Domain.Entities;

public sealed class TicketComment
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string AuthorUserId { get; private set; } = string.Empty;
    public bool IsInternal { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private TicketComment() { }

    public static TicketComment Create(Guid ticketId, string content, string authorUserId, bool isInternal = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(authorUserId);

        return new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            Content = content.Trim(),
            AuthorUserId = authorUserId,
            IsInternal = isInternal,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
