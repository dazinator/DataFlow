using Apache.Arrow;
using Apache.Arrow.Types;

namespace Research.ApacheArrow.Prototype;

// Reference-only spike: nested schema shape for JournalBatch -> Journal -> JournalEntry.
public static class ArrowJournalSchemaSpike
{
    public static Schema CreateNestedJournalSchema()
    {
        var entryStruct = new StructType(new[]
        {
            new Field("entry_id", Int64Type.Default, false),
            new Field("account", StringType.Default, true),
            new Field("amount", Decimal128Type.Default, false)
        });

        var journalStruct = new StructType(new[]
        {
            new Field("journal_id", Int64Type.Default, false),
            new Field("posted_on", TimestampType.Default, true),
            new Field("entries", new ListType(new Field("entry", entryStruct, false)), true)
        });

        return new Schema(new[]
        {
            new Field("batch_id", StringType.Default, false),
            new Field("journals", new ListType(new Field("journal", journalStruct, false)), true)
        });
    }
}
