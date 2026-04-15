using Apache.Arrow;
using Apache.Arrow.Types;

namespace Research.ApacheArrow.Prototype;

// Reference-only spike: target denormalized table shape for analytics stages.
public static class ArrowJournalNormalizationSpike
{
    public static Schema CreateEntryFactSchema() => new(new[]
    {
        new Field("batch_id", StringType.Default, false),
        new Field("journal_id", Int64Type.Default, false),
        new Field("entry_id", Int64Type.Default, false),
        new Field("account", StringType.Default, true),
        new Field("amount", Decimal128Type.Default, false)
    });

    // Shape contract for a normalized batch emitted into DataFlow.
    public static RecordBatch CreateEmptyFactBatch(Schema schema) =>
        new(schema, new IArrowArray[]
        {
            new StringArray.Builder().Build(),
            new Int64Array.Builder().Build(),
            new Int64Array.Builder().Build(),
            new StringArray.Builder().Build(),
            new Decimal128Array.Builder().Build(),
        }, 0);
}
