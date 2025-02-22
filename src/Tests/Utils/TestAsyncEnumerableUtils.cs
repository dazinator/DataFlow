namespace Tests.Utils;

public class TestAsyncEnumerableUtils
{
    public static async IAsyncEnumerable<string> GetItemsStreamAsync(int howMany)
    {
        for (var i = 0; i < howMany; i++)
        {
            yield return $"Item: {i}";
            await Task.Delay(10);
        }
    }

}

