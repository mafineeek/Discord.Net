using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using static Discord.Net.Hanz.OptionalExtensions;

namespace Discord.Net.Hanz;

public readonly record struct Keyed<TKey, TValue>(TKey Key, TValue Value)
{
    public static implicit operator Keyed<TKey, TValue>((TKey, TValue) tuple) => new(tuple.Item1, tuple.Item2);
}

public sealed class IncrementalKeyValueProvider<TKey, TValue>
{
    public IEnumerable<TKey> Keys => _entries.Keys;
    public IEnumerable<TValue> Values => _entries.Values;

    public IEnumerable<KeyValuePair<TKey, TValue>> Entries => _entries;

    public IncrementalValuesProvider<Keyed<TKey, TValue>> EntriesProvider { get; }

    public IncrementalValuesProvider<TKey> KeysProvider { get; }
    public IncrementalValuesProvider<TValue> ValuesProvider { get; }

    private readonly Dictionary<TKey, TValue> _entries;

    private readonly object _lock = new();

    public IncrementalKeyValueProvider(IncrementalValuesProvider<Introspected<Keyed<TKey, TValue>>> provider)
    {
        _entries = [];

        EntriesProvider = provider
            .MaybeSelect(introspected =>
            {
                var (key, value) = introspected.Value;

                switch (introspected.State)
                {
                    case State.Added:
                        _entries[key] = value;
                        goto case State.Cached;
                    case State.Removed:
                        _entries.Remove(key);
                        return default;
                    case State.Cached:
                        return new Keyed<TKey, TValue>(key, value).Some();
                }

                return default;
            });

        KeysProvider = EntriesProvider.Select((x, _) => x.Key);
        ValuesProvider = EntriesProvider.Select((x, _) => x.Value);
    }

    public bool ContainsKey(TKey key)
        => _entries.ContainsKey(key);

    public bool TryGetValue(TKey key, out TValue value)
    {
        if (key is null)
        {
            value = default;
            return false;
        }

        lock (_lock)
            return _entries.TryGetValue(key, out value);
    }
    
    public TValue GetValue(TKey key) => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();

    public TValue? GetValueOrDefault(TKey key)
        => GetValueOrDefault(key, default);

    public TValue? GetValueOrDefault(TKey key, TValue? defaultValue)
        => TryGetValue(key, out var value) ? value : defaultValue;

    public Optional<TValue> OptionallyGet(TKey key)
        => TryGetValue(key, out var value) ? value.Some() : default;

    public IncrementalKeyValueProvider<TKey, TResult> MergeByKey<TOther, TResult>(
        IncrementalKeyValueProvider<TKey, TOther> other,
        Func<TKey, Optional<TValue>, Optional<TOther>, TResult> resultSelector
    ) => new(
        KeysProvider
            .Collect()
            .Combine(other.KeysProvider.Collect())
            .SelectMany((pair, _) => pair.Left.Concat(pair.Right).Distinct())
            .Select(Keyed<TKey, TResult> (key, _) =>
                (key, resultSelector(key, OptionallyGet(key), other.OptionallyGet(key)))
            )
            .AsIntrospected()
    );

    public IncrementalKeyValueProvider<TKey, (TValue Value, TOther Other)> JoinByKey<TOther>(
        IncrementalKeyValueProvider<TKey, TOther> other,
        bool allowDefault = false,
        TOther defaultValue = default
    ) => JoinByKey(other, (key, value, other) => (value, other), allowDefault, defaultValue);

    public IncrementalKeyValueProvider<TKey, TResult> JoinByKey<TOther, TResult>(
        IncrementalKeyValueProvider<TKey, TOther> other,
        Func<TKey, TValue, TOther, TResult> resultSelector,
        bool allowDefault = false,
        TOther defaultValue = default
    ) => new(
        EntriesProvider
            .Combine(other.EntriesProvider.Collect())
            .SelectMany(ImmutableArray<Keyed<TKey, TResult>> (pair, _) =>
            {
                if (!other._entries.TryGetValue(pair.Left.Key, out var otherValue))
                {
                    if (!allowDefault)
                        return ImmutableArray<Keyed<TKey, TResult>>.Empty;

                    otherValue = defaultValue;
                }

                return ImmutableArray.Create<Keyed<TKey, TResult>>(
                    (
                        pair.Left.Key,
                        resultSelector(pair.Left.Key, pair.Left.Value, otherValue)
                    )
                );
            })
            .AsIntrospected()
    );

    public IncrementalKeyValueProvider<TKey, TResult> JoinByKey<TOther, TResult>(
        IncrementalGroupingProvider<TKey, TOther> other,
        Func<TKey, TValue, ImmutableArray<TOther>, TResult> resultSelector,
        bool includeEmpty = false
    ) => new(
        EntriesProvider
            .Combine(other.EntriesProvider.Collect())
            .SelectMany(ImmutableArray<Keyed<TKey, TResult>> (pair, _) =>
            {
                if (!other.TryGetValues(pair.Left.Key, out var otherValues) && !includeEmpty)
                    return ImmutableArray<Keyed<TKey, TResult>>.Empty;

                return ImmutableArray.Create<Keyed<TKey, TResult>>(
                    (
                        pair.Left.Key,
                        resultSelector(pair.Left.Key, pair.Left.Value, otherValues)
                    )
                );
            })
            .AsIntrospected()
    );

    public IncrementalKeyValueProvider<TOther, TValue> PairKeys<TOther>(
        IncrementalKeyValueProvider<TKey, TOther> other
    ) => other.Pair(this);

    public IncrementalKeyValueProvider<TValue, TOther> Pair<TOther>(
        IncrementalKeyValueProvider<TKey, TOther> other
    )
    {
        return new(
            EntriesProvider
                .Combine(other.EntriesProvider.Collect())
                .SelectMany(ImmutableArray<Keyed<TValue, TOther>> (pair, _) =>
                {
                    if (!other._entries.TryGetValue(pair.Left.Key, out var otherValue))
                        return ImmutableArray<Keyed<TValue, TOther>>.Empty;

                    return ImmutableArray.Create<Keyed<TValue, TOther>>((pair.Left.Value, otherValue));
                })
                .AsIntrospected()
        );
    }

    public IncrementalKeyValueProvider<TKey, TResult> Map<TResult>(
        Func<TKey, TValue, TResult> selector
    ) => new(
        EntriesProvider
            .Select(Keyed<TKey, TResult> (x, _) => (x.Key, selector(x.Key, x.Value)))
            .AsIntrospected()
    );

    public IncrementalKeyValueProvider<TOther, TValue> TransformKeyVia<TOther>(
        IncrementalKeyValueProvider<TKey, TOther> other
    ) => new(
        EntriesProvider
            .Combine(other.EntriesProvider.Collect())
            .MaybeSelect(pair =>
            {
                if (!other.TryGetValue(pair.Left.Key, out var value))
                    return default;

                return new Keyed<TOther, TValue>(value, pair.Left.Value).Some();
            })
            .AsIntrospected()
    );
}

public static class KeyedExtensions
{
    public static IncrementalKeyValueProvider<TKey, TSource> KeyedBy<TSource, TKey>(
        this IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> selector
    ) => source.KeyedBy(x => (selector(x), x));

    public static IncrementalKeyValueProvider<TKey, TValue> KeyedBy<TSource, TKey, TValue>(
        this IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TValue> valueSelector
    ) => source.KeyedBy(x => (keySelector(x), valueSelector(x)));

    public static IncrementalKeyValueProvider<TKey, TValue> KeyedBy<TSource, TKey, TValue>(
        this IncrementalValuesProvider<TSource> source,
        Func<TSource, (TKey, TValue)> selector
    ) => new(
        source
            .Select(Keyed<TKey, TValue> (x, _) => selector(x))
            .AsIntrospected()
    );

    public static IncrementalValuesProvider<TResult> Select<TKey, TValue, TResult>(
        this IncrementalKeyValueProvider<TKey, TValue> keyed,
        Func<TKey, TValue, TResult> selector
    ) => keyed.EntriesProvider.Select((kvp, _) => selector(kvp.Key, kvp.Value));

    public static IncrementalValuesProvider<TResult> Select<TKey, TValue, TResult>(
        this IncrementalKeyValueProvider<TKey, TValue> keyed,
        Func<TValue, TResult> selector
    ) => keyed.EntriesProvider.Select((kvp, _) => selector(kvp.Value));

    public static IncrementalKeyValueProvider<TKey, TValue> Where<TKey, TValue>(
        this IncrementalKeyValueProvider<TKey, TValue> keyed,
        Func<TValue, bool> selector
    ) => keyed.Where((_, v) => selector(v));

    public static IncrementalKeyValueProvider<TKey, TValue> Where<TKey, TValue>(
        this IncrementalKeyValueProvider<TKey, TValue> keyed,
        Func<TKey, TValue, bool> selector
    ) => new(
        keyed.EntriesProvider
            .Where(kvp => selector(kvp.Key, kvp.Value))
            .AsIntrospected()
    );
}