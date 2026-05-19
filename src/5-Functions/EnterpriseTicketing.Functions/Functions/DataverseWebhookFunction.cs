using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EnterpriseTicketing.Functions.Functions;

/// <summary>
/// HTTP-triggered Azure Function that receives Dataverse webhook notifications.
///
/// Dataverse Webhook Architecture:
///   Dataverse can trigger webhooks on Create/Update/Delete/Associate/Disassociate operations.
///   This Function acts as the webhook endpoint, receiving Dataverse plugin step notifications.
///
/// Authentication:
///   Dataverse sends a shared secret in the request headers.
///   Validate the secret before processing. For production, use certificate-based validation.
///
/// IMPORTANT: Dataverse webhooks are synchronous — they wait for your response.
///   Return 200 OK immediately (within 2 seconds) or Dataverse will time out and retry.
///   For long-running processing, publish to Service Bus and return immediately.
///
/// Dataverse webhook payload (RemoteExecutionContext):
///   MessageName: "Create", "Update", "Delete"
///   EntityLogicalName: "new_ticket"
///   PrimaryEntityId: GUID of the affected record
///   Attributes: changed attribute values (for Update)
///   Stage: 40 (PostOperation, after commit)
/// </summary>
public class DataverseWebhookFunction
{
    private readonly ILogger<DataverseWebhookFunction> _logger;

    public DataverseWebhookFunction(ILogger<DataverseWebhookFunction> logger)
    {
        _logger = logger;
    }

    [Function("DataverseWebhook")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "dataverse/webhook")]
        HttpRequest req,
        FunctionContext executionContext)
    {
        // Validate shared secret header
        if (!req.Headers.TryGetValue("x-webhook-secret", out var secret) ||
            secret.ToString() != Environment.GetEnvironmentVariable("DATAVERSE_WEBHOOK_SECRET"))
        {
            _logger.LogWarning("Unauthorized webhook request from {RemoteIp}",
                req.HttpContext.Connection.RemoteIpAddress);
            return new UnauthorizedResult();
        }

        var requestBody = await new StreamReader(req.Body).ReadToEndAsync(executionContext.CancellationToken);

        _logger.LogInformation("Received Dataverse webhook notification");

        // Parse Dataverse RemoteExecutionContext
        JsonElement payload;
        try
        {
            payload = JsonSerializer.Deserialize<JsonElement>(requestBody);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid Dataverse webhook payload");
            return new BadRequestObjectResult("Invalid payload format");
        }

        var messageName = payload.TryGetProperty("MessageName", out var msgName)
            ? msgName.GetString()
            : "Unknown";

        var entityName = payload.TryGetProperty("PrimaryEntityName", out var entityProp)
            ? entityProp.GetString()
            : "Unknown";

        var entityId = payload.TryGetProperty("PrimaryEntityId", out var idProp)
            ? idProp.GetString()
            : "Unknown";

        _logger.LogInformation(
            "Dataverse {MessageName} on {EntityName} ({EntityId})",
            messageName, entityName, entityId);

        // Process synchronously if fast, otherwise publish to Service Bus and return 200 immediately
        // Dataverse gives ~2 seconds before timeout — do NOT do slow work here
        await ProcessWebhookAsync(messageName, entityName, entityId, executionContext.CancellationToken);

        // Must return 200 OK for Dataverse to consider the webhook successful
        return new OkResult();
    }

    private async Task ProcessWebhookAsync(
        string? messageName, string? entityName, string? entityId, CancellationToken cancellationToken)
    {
        // Production: publish to Service Bus queue for asynchronous processing
        // This prevents the 2-second Dataverse timeout from being exceeded
        _logger.LogDebug(
            "Webhook processed: {MessageName} {EntityName} {EntityId}",
            messageName, entityName, entityId);

        await Task.CompletedTask;
    }
}
