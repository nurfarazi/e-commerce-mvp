using ECommerceMvp.Shared.Application;
using MongoDB.Driver;

namespace ECommerceMvp.Checkout.QueryApi;

/// <summary>
/// Query: Get checkout saga status.
/// </summary>
public class GetCheckoutStatusQuery : IQuery<GetCheckoutStatusResponse>
{
    public string CheckoutId { get; set; } = string.Empty;
}

public class GetCheckoutStatusResponse
{
    public string CheckoutId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public DateTime InitiatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Handler for GetCheckoutStatusQuery.
/// Queries MongoDB snapshots collection for latest saga state.
/// </summary>
public class GetCheckoutStatusQueryHandler : IQueryHandler<GetCheckoutStatusQuery, GetCheckoutStatusResponse>
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<GetCheckoutStatusQueryHandler> _logger;

    public GetCheckoutStatusQueryHandler(IMongoDatabase database, ILogger<GetCheckoutStatusQueryHandler> logger)
    {
        _database = database;
        _logger = logger;
    }

    public async Task<GetCheckoutStatusResponse> HandleAsync(GetCheckoutStatusQuery query)
    {
        try
        {
            var collection = _database.GetCollection<dynamic>("checkout_snapshots");
            var filter = Builders<dynamic>.Filter.Eq("aggregate_id", query.CheckoutId);

            var snapshot = await collection
                .Find(filter)
                .SortByDescending(d => d["version"])
                .FirstOrDefaultAsync();

            if (snapshot == null)
            {
                return new GetCheckoutStatusResponse
                {
                    CheckoutId = query.CheckoutId,
                    Status = "NotFound",
                    Success = false,
                    Error = "Checkout saga not found"
                };
            }

            return new GetCheckoutStatusResponse
            {
                CheckoutId = query.CheckoutId,
                Status = snapshot["status"],
                FailureReason = snapshot.Contains("failure_reason") ? snapshot["failure_reason"] : null,
                InitiatedAt = snapshot["initiated_at"],
                CompletedAt = snapshot.Contains("completed_at") ? snapshot["completed_at"] : null,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying checkout status for {CheckoutId}", query.CheckoutId);
            return new GetCheckoutStatusResponse
            {
                CheckoutId = query.CheckoutId,
                Success = false,
                Error = ex.Message
            };
        }
    }
}
