using System.Collections.Immutable;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz;

public static class KeyedExtensions
{
    public static TValue? GetValueOrDefault<TKey, TValue>(
        this Keyed<TKey, TValue> keyed,
        TKey key,
        TValue? defaultValue = default
    )
        where TValue : struct
    {
        if (keyed.TryGetValue(key, out var value))
            return value;

        return defaultValue;
    }
}

public sealed class Keyed<TKey, TValue>
{
    public Dictionary<TKey, TValue>.KeyCollection Keys => _entries.Keys;
    public Dictionary<TKey, TValue>.ValueCollection Values => _entries.Values;

    private readonly Dictionary<TKey, TValue> _entries;

    private readonly object _lock;

    private int _version;

    public bool TryGetValue(TKey key, out TValue value)
    {
        lock (_lock)
        {
            return _entries.TryGetValue(key, out value);
        }
    }

    public TValue? GetValueOrDefault(TKey key, TValue? defaultValue = default)
        => TryGetValue(key, out var result) ? result : defaultValue;

    private Keyed<TKey, TValue> OnBatch(
        ImmutableArray<(TKey Key, TValue Value)> batch,
        CancellationToken token
    )
    {
        lock (_lock)
        {
            token.ThrowIfCancellationRequested();

            _entries.Clear();

            foreach (var (key, value) in batch)
            {
                _entries[key] = value;

                token.ThrowIfCancellationRequested();
            }

            unchecked
            {
                _version++;
            }
        }

        return this;
    }

    public override int GetHashCode()
        => _version;

    public static IncrementalValueProvider<Keyed<TKey, TValue>> Create(
        IncrementalValueProvider<ImmutableArray<(TKey, TValue)>> provider
    )
    {
        var keyed = new Keyed<TKey, TValue>();
        return provider.Select(keyed.OnBatch);
    }
}

public static partial class ProviderExtensions
{
    public static IncrementalValueProvider<Keyed<TKey, TValue>> ToKeyed<TKey, TValue>(
        this IncrementalValuesProvider<TValue> provider,
        Func<TValue, TKey> keySelector
    ) => Keyed<TKey, TValue>.Create(
        provider.Select((x, _) => (keySelector(x), x)).Collect()
    );

    public static IncrementalValuesProvider<(TSource Source, TValue? Value)> Join<TKey, TValue, TSource, TResult>(
        this IncrementalValueProvider<Keyed<TKey, TValue>> keyed,
        IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector,
        TValue? defaultValue = default
    ) => Join(
        keyed,
        source,
        keySelector,
        (TSource Source, TValue? Value) (key, value, source) => (source, value),
        defaultValue
    );

    public static IncrementalValuesProvider<TResult> Combine<TKey, TValue, TSource, TResult>(
        this IncrementalValuesProvider<TSource> source,
        IncrementalValueProvider<Keyed<TKey, TValue>> keyed,
        Func<TSource, TKey> keySelector,
        Func<TKey, TValue?, TSource, TResult> resultSelector,
        TValue? defaultValue = default
    ) => Join(keyed, source, keySelector, resultSelector, defaultValue);

    public static IncrementalValuesProvider<TResult> Combine<TKey, TValue, TSource, TResult>(
        this IncrementalValueProvider<ImmutableArray<TSource>> source,
        IncrementalValueProvider<Keyed<TKey, TValue>> keyed,
        Func<TSource, TKey> keySelector,
        Func<TKey, TValue?, TSource, TResult> resultSelector,
        TValue? defaultValue = default
    ) => Join(keyed, source, keySelector, resultSelector, defaultValue);

    public static IncrementalValuesProvider<TResult> Join<TKey, TValue, TSource, TResult>(
        this IncrementalValueProvider<Keyed<TKey, TValue>> keyed,
        IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TKey, TValue?, TSource, TResult> resultSelector,
        TValue? defaultValue = default
    )
    {
        return source
            .Select((x, _) => (Key: keySelector(x), Source: x))
            .Combine(keyed)
            .Select((pair, _) => resultSelector(
                pair.Left.Key,
                pair.Right.GetValueOrDefault(pair.Left.Key, defaultValue),
                pair.Left.Source
            ));
    }

    public static IncrementalValuesProvider<TResult> Join<TKey, TValue, TSource, TResult>(
        this IncrementalValueProvider<Keyed<TKey, TValue>> keyed,
        IncrementalValueProvider<ImmutableArray<TSource>> source,
        Func<TSource, TKey> keySelector,
        Func<TKey, TValue?, TSource, TResult> resultSelector,
        TValue? defaultValue = default
    )
    {
        return source
            .SelectMany((sources, _) => sources.Select(source => (Key: keySelector(source), Source: source)))
            .Combine(keyed)
            .Select((pair, _) => resultSelector(
                pair.Left.Key,
                pair.Right.GetValueOrDefault(pair.Left.Key, defaultValue),
                pair.Left.Source
            ));
    }

    public static IncrementalValuesProvider<TValue> Map<TSource, TKey, TValue>(
        this IncrementalValuesProvider<TSource> source,
        IncrementalValueProvider<Keyed<TKey, TValue>> keyed,
        Func<TSource, TKey> keySelector
    )
    {
        return source
            .Select((x, _) => keySelector(x))
            .Combine(keyed)
            .SelectMany((pair, _) =>
                pair.Right.TryGetValue(pair.Left, out var value)
                    ? ImmutableArray.Create(value)
                    : ImmutableArray<TValue>.Empty
            );
    }
}