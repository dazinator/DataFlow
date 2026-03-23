namespace DataFlow.Blazor.Demo.Server.Flows;

using DataFlow.POC.Core;

// ── Domain types ──────────────────────────────────────────────────────────────

/// <summary>Raw invoice as received from an upstream system.</summary>
public record Invoice(Guid Id, string Type, decimal Amount, string Customer, string Region);

/// <summary>Invoice after validation and enrichment (currency + tax rate assigned).</summary>
public record ValidatedInvoice(Invoice Invoice, string Currency, decimal TaxRate);

/// <summary>Payment record ready for settlement and downstream systems.</summary>
public record Payment(Guid PaymentId, Guid InvoiceId, decimal GrossAmount, decimal TaxAmount,
                      string Currency, string Status, string ProcessorLane);

// ── Source ────────────────────────────────────────────────────────────────────

/// <summary>
/// Generates a realistic stream of invoices at a steady pace.
/// Routing distribution: ~40% standard · ~30% premium · ~30% international.
/// </summary>
public sealed class InvoiceSourceBlock : BlockBase<object, Invoice>
{
    private readonly int   _count;
    private readonly int   _delayMs;

    private static readonly string[] Customers =
    [
        "Acme Corp", "Globex Ltd", "Initech", "Umbrella Inc", "Stark Industries",
        "Wayne Enterprises", "Oscorp", "Soylent Corp", "Buy N Large", "Massive Dynamic",
        "Weyland-Yutani", "Veridian Dynamics", "Bluth Company", "Prestige Worldwide",
        "Sterling Cooper", "Dunder Mifflin", "Vandelay Industries", "Dinoco", "Cyberdyne",
    ];

    private static readonly string[] Regions = ["EMEA", "APAC", "AMER", "LATAM"];

    public InvoiceSourceBlock(string name, int count, int delayMs)
        : base(new BlockContext(name))
    {
        _count   = count;
        _delayMs = delayMs;
    }

    public override async IAsyncEnumerable<Invoice> ExecuteAsync(
        IAsyncEnumerable<object> input,
        IExecutionContext context)
    {
        var rng = new Random(42); // fixed seed — reproducible routing distribution
        for (var i = 1; i <= _count; i++)
        {
            await Task.Delay(_delayMs, context.CancellationToken);

            // 40% standard · 30% premium · 30% international
            var type = (i % 10) switch
            {
                0 or 1 or 2 or 3 => "standard",
                4 or 5 or 6      => "premium",
                _                => "international",
            };

            var amount  = Math.Round((decimal)(rng.NextDouble() * 49_900 + 100), 2);
            var customer = Customers[rng.Next(Customers.Length)];
            var region   = Regions[rng.Next(Regions.Length)];

            yield return new Invoice(Guid.NewGuid(), type, amount, customer, region);
        }
    }
}

// ── Validation ────────────────────────────────────────────────────────────────

/// <summary>
/// Validates each invoice and assigns currency + tax rate based on type/region.
/// Fast pass-through — all invoices pass validation in this demo.
/// </summary>
public sealed class InvoiceValidatorBlock : BlockBase<Invoice, ValidatedInvoice>
{
    public InvoiceValidatorBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<ValidatedInvoice> ExecuteAsync(
        IAsyncEnumerable<Invoice> input,
        IExecutionContext context)
    {
        await foreach (var inv in input.WithCancellation(context.CancellationToken))
        {
            await Task.Delay(8, context.CancellationToken);

            var (currency, taxRate) = inv.Type switch
            {
                "international" => ("USD", 0m),     // zero-rated cross-border
                "premium"       => ("GBP", 0.20m),  // UK VAT
                _               => ("EUR", 0.19m),  // standard EU VAT
            };

            yield return new ValidatedInvoice(inv, currency, taxRate);
        }
    }
}

// ── Classifier (routing fan-out point) ───────────────────────────────────────

/// <summary>
/// Lightweight pass-through block that forms the routing decision point.
/// The outgoing SelectiveRoutingEdgeStrategy dispatches items to the
/// correct specialised processor based on <see cref="Invoice.Type"/>.
/// </summary>
public sealed class InvoiceClassifierBlock : BlockBase<ValidatedInvoice, ValidatedInvoice>
{
    public InvoiceClassifierBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<ValidatedInvoice> ExecuteAsync(
        IAsyncEnumerable<ValidatedInvoice> input,
        IExecutionContext context)
    {
        await foreach (var inv in input.WithCancellation(context.CancellationToken))
        {
            await Task.Delay(4, context.CancellationToken);
            yield return inv;
        }
    }
}

// ── Specialised processors ────────────────────────────────────────────────────

/// <summary>Standard invoice processor — fastest lane.</summary>
public sealed class StandardInvoiceProcessorBlock : BlockBase<ValidatedInvoice, Payment>
{
    public StandardInvoiceProcessorBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<Payment> ExecuteAsync(
        IAsyncEnumerable<ValidatedInvoice> input,
        IExecutionContext context)
    {
        await foreach (var vi in input.WithCancellation(context.CancellationToken))
        {
            await Task.Delay(50, context.CancellationToken);
            var tax = Math.Round(vi.Invoice.Amount * vi.TaxRate, 2);
            yield return new Payment(Guid.NewGuid(), vi.Invoice.Id,
                vi.Invoice.Amount + tax, tax, vi.Currency, "settled", "standard");
        }
    }
}

/// <summary>Premium invoice processor — additional compliance checks, ~80% slower than standard.</summary>
public sealed class PremiumInvoiceProcessorBlock : BlockBase<ValidatedInvoice, Payment>
{
    public PremiumInvoiceProcessorBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<Payment> ExecuteAsync(
        IAsyncEnumerable<ValidatedInvoice> input,
        IExecutionContext context)
    {
        await foreach (var vi in input.WithCancellation(context.CancellationToken))
        {
            await Task.Delay(90, context.CancellationToken); // fraud + credit checks
            var tax = Math.Round(vi.Invoice.Amount * vi.TaxRate, 2);
            yield return new Payment(Guid.NewGuid(), vi.Invoice.Id,
                vi.Invoice.Amount + tax, tax, vi.Currency, "settled", "premium");
        }
    }
}

/// <summary>
/// International invoice processor — FX conversion + cross-border compliance.
/// Slowest lane; drives backpressure into the fan-in buffer when routing volume is high.
/// </summary>
public sealed class InternationalInvoiceProcessorBlock : BlockBase<ValidatedInvoice, Payment>
{
    public InternationalInvoiceProcessorBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<Payment> ExecuteAsync(
        IAsyncEnumerable<ValidatedInvoice> input,
        IExecutionContext context)
    {
        await foreach (var vi in input.WithCancellation(context.CancellationToken))
        {
            await Task.Delay(150, context.CancellationToken); // FX + SWIFT check
            yield return new Payment(Guid.NewGuid(), vi.Invoice.Id,
                vi.Invoice.Amount, 0m, vi.Currency, "settled", "international");
        }
    }
}

// ── Fan-in normaliser ─────────────────────────────────────────────────────────

/// <summary>
/// Fan-in convergence point that merges payments from all three processor lanes.
/// Normalises status strings and passes payments downstream for broadcast.
/// The three bounded incoming edges create visible backpressure when the
/// broadcast sinks cannot keep up.
/// </summary>
public sealed class PaymentNormalizerBlock : BlockBase<Payment, Payment>
{
    public PaymentNormalizerBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<Payment> ExecuteAsync(
        IAsyncEnumerable<Payment> input,
        IExecutionContext context)
    {
        await foreach (var p in input.WithCancellation(context.CancellationToken))
        {
            await Task.Delay(5, context.CancellationToken);
            yield return p with { Status = p.Status.ToUpperInvariant() };
        }
    }
}

// ── Broadcast sinks ───────────────────────────────────────────────────────────

/// <summary>
/// Records every settled payment to the immutable audit ledger.
/// Deliberately slow (130 ms) — this is the broadcast bottleneck that fills
/// the upstream buffer and cascades backpressure through the pipeline.
/// </summary>
public sealed class AuditLogBlock : BlockBase<Payment, object>
{
    public AuditLogBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<Payment> input,
        IExecutionContext context)
    {
        await foreach (var p in input.WithCancellation(context.CancellationToken))
            await Task.Delay(130, context.CancellationToken); // durable write to ledger
        yield break;
    }
}

/// <summary>
/// Dispatches settlement notifications to customers and internal teams.
/// </summary>
public sealed class NotificationBlock : BlockBase<Payment, object>
{
    public NotificationBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<Payment> input,
        IExecutionContext context)
    {
        await foreach (var p in input.WithCancellation(context.CancellationToken))
            await Task.Delay(45, context.CancellationToken); // email + webhook dispatch
        yield break;
    }
}

/// <summary>
/// Streams payment metrics into the analytics warehouse.
/// Fast sink — never a bottleneck.
/// </summary>
public sealed class AnalyticsBlock : BlockBase<Payment, object>
{
    public AnalyticsBlock(string name) : base(new BlockContext(name)) { }

    public override async IAsyncEnumerable<object> ExecuteAsync(
        IAsyncEnumerable<Payment> input,
        IExecutionContext context)
    {
        await foreach (var p in input.WithCancellation(context.CancellationToken))
            await Task.Delay(12, context.CancellationToken); // streaming event push
        yield break;
    }
}
