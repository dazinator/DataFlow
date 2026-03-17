// ============================================================
// ErpHandlerPrototype.cs
// Prototype: Generic ERP Handler Pattern (Approach C)
//
// This is REFERENCE CODE for the implementation team.
// It demonstrates the migration pattern from AddPersistentRouter
// to the POC DataFlow model. Copy and adapt to the target application.
// ============================================================

using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JournalProcessing.Prototype;

// ============================================================
// Domain types (from the existing application)
// ============================================================

/// <summary>
/// Items emitted by JournalRoutingTransformer — one per distinct ERP system per batch.
/// </summary>
public sealed record SystemJournalProcessingItems(
    ErpSystem? System,
    Journal[] Journals);

/// <summary>
/// Resolved ERP system entity from the routing cache.
/// </summary>
public sealed record ErpSystem(string Name, string ConnectionString);

/// <summary>
/// A journal to be posted to an ERP system.
/// </summary>
public sealed record Journal(int Id, string CompanyCode, decimal Amount);

/// <summary>
/// Result of posting a journal to an ERP system.
/// Null PostingResult indicates unrouted (no ERP system found).
/// </summary>
public sealed record JournalPostingResult(Journal Journal, ErpPostingResult? PostingResult);

/// <summary>
/// ERP posting outcome.
/// </summary>
public sealed record ErpPostingResult(bool Success, string? ErrorMessage = null);

// ============================================================
// Handler abstraction — replaces per-route block creation
// ============================================================

/// <summary>
/// Handles posting for a specific ERP system type.
/// Register one implementation per supported ERP type (SAP, Oracle, etc.).
/// </summary>
public interface INamedErpSystemHandler
{
    /// <summary>
    /// The ERP system type name this handler handles.
    /// Must match System.Name values returned by the routing transformer.
    /// </summary>
    string SystemName { get; }

    /// <summary>
    /// Posts the journals in <paramref name="item"/> to the ERP system.
    /// The item carries the resolved System entity, so handlers can read
    /// system-specific configuration from item.System rather than needing
    /// it injected at construction time.
    /// </summary>
    IAsyncEnumerable<JournalPostingResult> PostAsync(
        SystemJournalProcessingItems item,
        CancellationToken cancellationToken);
}

/// <summary>
/// Fallback handler for items where no ERP system was resolved.
/// Emits JournalPostingResult with null PostingResult (not a failure — flows through TMS).
/// </summary>
public sealed class UnroutedJournalHandler
{
    public async IAsyncEnumerable<JournalPostingResult> HandleAsync(
        SystemJournalProcessingItems item,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // No ERP system for these journals — emit stub results
        foreach (var journal in item.Journals)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new JournalPostingResult(journal, PostingResult: null);
            await Task.Yield(); // Allow cooperative scheduling
        }
    }
}

// ============================================================
// SAP handler implementation — replaces SapBtpConnectorErpJournalSender
// ============================================================

/// <summary>
/// SAP BTP Connector ERP handler.
/// Handles all SAP system instances — per-tenant config comes from item.System,
/// not from a constructor argument (unlike the legacy AddPersistentRouter approach).
/// </summary>
public sealed class SapBtpErpHandler : INamedErpSystemHandler
{
    // SAP ERP type names as seen in the routing cache.
    // Both "SAP" and "SapS4Hana" map to this handler via the factory.
    public string SystemName => "SAP";

    private readonly ISapConnectorClient _client;
    private readonly ISapPostRequestMapper _mapper;
    private readonly ILogger<SapBtpErpHandler> _logger;

    public SapBtpErpHandler(
        ISapConnectorClient client,
        ISapPostRequestMapper mapper,
        ILogger<SapBtpErpHandler> logger)
    {
        _client = client;
        _mapper = mapper;
        _logger = logger;
    }

    public async IAsyncEnumerable<JournalPostingResult> PostAsync(
        SystemJournalProcessingItems item,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // item.System is guaranteed non-null when we reach this handler
        var system = item.System!;

        foreach (var journal in item.Journals)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ErpPostingResult result;
            try
            {
                var request = _mapper.Map(journal, system);
                var response = await _client.ODataPostAsync(request, cancellationToken);
                result = new ErpPostingResult(Success: true);
                _logger.LogDebug("Posted journal {JournalId} to {System}", journal.Id, system.Name);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to post journal {JournalId} to {System}",
                    journal.Id, system.Name);
                result = new ErpPostingResult(Success: false, ErrorMessage: ex.Message);
            }

            yield return new JournalPostingResult(journal, result);
        }
    }
}

// ============================================================
// Placeholder interfaces (actual implementations in application)
// ============================================================

public interface ISapConnectorClient
{
    Task<SapODataResponse> ODataPostAsync(SapJournalPostRequest request,
        CancellationToken cancellationToken);
}

public sealed record SapJournalPostRequest(string Payload);
public sealed record SapODataResponse(bool Success);

public interface ISapPostRequestMapper
{
    SapJournalPostRequest Map(Journal journal, ErpSystem system);
}
