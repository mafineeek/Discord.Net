using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz;

public enum State
{
    Added,
    Removed,
    Cached
}

public readonly record struct Introspected<T>(
    T Value,
    State State
);

public static class IntrospectExtension
{
    public static IncrementalValuesProvider<Introspected<T>> AsIntrospected<T>(
        this IncrementalValuesProvider<T> source
    )
    {
        var lastBatch = new HashSet<T>();
        var bucket = new HashSet<T>();
        
        return source.Collect().SelectMany((items, token) => Introspect(items, lastBatch, bucket, token));
    }

    private static IEnumerable<Introspected<T>> Introspect<T>(
        ImmutableArray<T> items,
        HashSet<T> lastBatch,
        HashSet<T> bucket,
        CancellationToken token)
    {
        bucket.Clear();
        bucket.UnionWith(lastBatch);

        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            bucket.Remove(item);

            if (lastBatch.Contains(item))
                yield return new(item, State.Cached);
            else
                yield return new(item, State.Added);
            
            token.ThrowIfCancellationRequested();
        }

        foreach (var item in bucket)
        {
            yield return new(item, State.Removed);
            token.ThrowIfCancellationRequested();
        }
        
        lastBatch.Clear();
        lastBatch.UnionWith(items);
        bucket.Clear();
    }
}
