using System.ComponentModel.DataAnnotations;

namespace EnterpriseTicketing.Infrastructure.Messaging.Configuration;

public sealed class ServiceBusConfiguration
{
    public const string SectionName = "ServiceBus";

    [Required]
    public string ConnectionString { get; set; } = string.Empty;

    public string TicketEventsQueueName { get; set; } = "ticket-events";
    public string NotificationsQueueName { get; set; } = "ticket-notifications";
    public int MaxConcurrentCalls { get; set; } = 16;
    public int MaxRetryCount { get; set; } = 3;
}
