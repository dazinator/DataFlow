namespace DataFlow.Blazor.ItemTypes;

/// <summary>
/// Derives a human-readable label from a CLR type for use in flow visualizations.
/// Used as the fallback when no explicit label has been registered in
/// <see cref="IDataFlowItemTypeStore"/>.
/// </summary>
public static class DataFlowTypeNameFormatter
{
    /// <summary>
    /// Returns a readable label for <paramref name="type"/>, or an empty string for
    /// framework noise types (<c>object</c>, <c>void</c>) that carry no domain meaning.
    /// </summary>
    public static string Format(Type type)
    {
        if (type == typeof(object) || type == typeof(void))
            return string.Empty;

        // T[] → "{element}[]"
        if (type.IsArray)
        {
            var elem = Format(type.GetElementType()!);
            return elem.Length == 0 ? string.Empty : $"{elem}[]";
        }

        if (type.IsGenericType)
        {
            var def  = type.GetGenericTypeDefinition();
            var args = type.GetGenericArguments();

            if (args.Length == 1)
            {
                var argLabel = Format(args[0]);
                if (argLabel.Length == 0) return string.Empty;

                if (def == typeof(List<>)              ||
                    def == typeof(IList<>)             ||
                    def == typeof(IReadOnlyList<>)     ||
                    def == typeof(ICollection<>)       ||
                    def == typeof(IReadOnlyCollection<>))
                    return $"{argLabel} list";

                if (def == typeof(IEnumerable<>))
                    return $"{argLabel} sequence";

                return argLabel;
            }
        }

        // C# aliases for common primitives
        return type.Name switch
        {
            "Int32"   => "int",
            "Int64"   => "long",
            "Double"  => "double",
            "Single"  => "float",
            "Boolean" => "bool",
            "String"  => "string",
            "Decimal" => "decimal",
            var n     => n
        };
    }
}
