using System.Collections.Immutable;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz;

public static class GroupingExtensions
{
    public static ImmutableEquatableArray<TElement> GetEntriesOrEmptyEquatable<TKey, TElement>(
        this Grouping<TKey, TElement> grouping,
        TKey key
    )
        where TElement : IEquatable<TElement>
    {
        if (grouping.TryGetEntries(key, out var entries))
            return entries.ToImmutableEquatableArray();

        return ImmutableEquatableArray<TElement>.Empty;
    }
}

public sealed class Grouping<TKey, TElement>
{
    private enum State
    {
        Added,
        Removed,
        Cached
    }

    private readonly record struct Entry(
        TKey Key,
        TElement Element,
        State State
    );

    public Dictionary<TKey, HashSet<TElement>>.KeyCollection Keys => _entries.Keys;

    private readonly Dictionary<TKey, HashSet<TElement>> _entries = [];

    private readonly object _lock = new();

    private int _version;

    private (TKey Key, TElement Element) Process(
        (TKey Key, TElement Element) tuple,
        CancellationToken token)
    {
        lock (_lock)
        {
            if (!_entries.TryGetValue(tuple.Key, out var entries))
                _entries[tuple.Key] = entries = new HashSet<TElement>();

            entries.Add(tuple.Element);
        }

        return tuple;
    }

    public bool Has((TKey Key, TElement Element) tuple)
        => _entries.ContainsKey(tuple.Key) && _entries[tuple.Key].Contains(tuple.Element);

    public bool TryGetEntries(TKey key, out HashSet<TElement> entries)
        => _entries.TryGetValue(key, out entries) && entries.Count > 0;

    public ImmutableArray<TElement> GetEntriesOrEmpty(TKey key)
    {
        if (_entries.TryGetValue(key, out var entries))
            return entries.ToImmutableArray();

        return ImmutableArray<TElement>.Empty;
    }

    private Grouping<TKey, TElement> ProcessBatch(
        ImmutableArray<(TKey Key, TElement Element)> entries,
        CancellationToken token)
    {
        lock (_lock)
        {
            var keys = new List<TKey>();
            foreach (var grouping in entries.GroupBy(x => x.Key, x => x.Element))
            {
                keys.Add(grouping.Key);
                _entries[grouping.Key].IntersectWith(grouping);
            }

            foreach (var removedKey in _entries.Keys.Except(keys))
            {
                _entries.Remove(removedKey);
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

    public static IncrementalValueProvider<Grouping<TKey, TElement>> CreateAround<TSource>(
        IncrementalValuesProvider<TSource> provider,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector
    ) => Create(
        provider.Select((source, _) => (keySelector(source), elementSelector(source)))
    );

    public static IncrementalValueProvider<Grouping<TKey, TElement>> CreateAround<TSource>(
        IncrementalValueProvider<ImmutableArray<TSource>> provider,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector
    ) => Create(
        provider.SelectMany((sources, _) =>
            sources.Select(source => (keySelector(source), elementSelector(source)))
        )
    );

    public static IncrementalValueProvider<Grouping<TKey, TElement>> CreateAround<TSource>(
        IncrementalValueProvider<ImmutableEquatableArray<TSource>> provider,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector
    )
        where TSource : IEquatable<TSource>
    {
        return Create(
            provider.SelectMany((sources, _) =>
                sources.Select(source => (keySelector(source), elementSelector(source)))
            )
        );
    }

    public static IncrementalValueProvider<Grouping<TKey, TElement>> CreateAroundMany<TSource>(
        IncrementalValuesProvider<TSource> provider,
        Func<TSource, TKey> keySelector,
        Func<TSource, IEnumerable<TElement>> elementSelector
    ) => Create(
        provider.SelectMany((source, _) =>
        {
            var key = keySelector(source);
            return elementSelector(source).Select(element => (key, element));
        })
    );

    public static IncrementalValueProvider<Grouping<TKey, TElement>> CreateAroundMany<TSource>(
        IncrementalValueProvider<ImmutableArray<TSource>> provider,
        Func<TSource, TKey> keySelector,
        Func<TSource, IEnumerable<TElement>> elementSelector
    ) => Create(
        provider.SelectMany((source, _) =>
        {
            return source
                .GroupBy(
                    x => keySelector(x),
                    x => elementSelector(x)
                )
                .SelectMany(group => group
                    .SelectMany(elements => elements
                        .Select(element =>
                            (group.Key, element)
                        )
                    )
                );
        })
    );

    private static IncrementalValueProvider<Grouping<TKey, TElement>> Create(
        IncrementalValuesProvider<(TKey, TElement)> source
    )
    {
        var grouping = new Grouping<TKey, TElement>();

        return source
            .Select(grouping.Process)
            .Collect()
            .Select(grouping.ProcessBatch);
    }
}

public static partial class ProviderExtensions
{
    public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupBy<TKey, TElement>(
        this IncrementalValuesProvider<TElement> source,
        Func<TElement, TKey> keySelector
    ) => Grouping<TKey, TElement>.CreateAround(source, keySelector, x => x);


    public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupBy<TKey, TElement>(
        this IncrementalValueProvider<ImmutableArray<TElement>> source,
        Func<TElement, TKey> keySelector
    ) => Grouping<TKey, TElement>.CreateAround(source, keySelector, x => x);

    public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupBy<TKey, TElement>(
        this IncrementalValueProvider<ImmutableEquatableArray<TElement>> source,
        Func<TElement, TKey> keySelector
    )
        where TElement : IEquatable<TElement>
        => Grouping<TKey, TElement>.CreateAround(source, keySelector, x => x);

    public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(
        this IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector
    ) => Grouping<TKey, TElement>.CreateAround(source, keySelector, elementSelector);

    public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupManyBy<TSource, TKey, TElement>(
        this IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, IEnumerable<TElement>> elementSelector
    ) => Grouping<TKey, TElement>.CreateAroundMany(source, keySelector, elementSelector);

    public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupManyBy<TSource, TKey, TElement>(
        this IncrementalValueProvider<ImmutableArray<TSource>> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, IEnumerable<TElement>> elementSelector
    ) => Grouping<TKey, TElement>.CreateAroundMany(source, keySelector, elementSelector);

    public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(
        this IncrementalValueProvider<ImmutableArray<TSource>> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, TElement> elementSelector
    ) => Grouping<TKey, TElement>.CreateAround(source, keySelector, elementSelector);

    public static IncrementalValuesProvider<TResult> Pair<TKey, TElement, TSource, TResult>(
        this IncrementalValueProvider<Grouping<TKey, TElement>> group,
        IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TKey, TElement, TSource, TResult> mapper
    )
    {
        return source
            .Select((x, _) => (Source: x, Key: keySelector(x)))
            .Combine(group)
            .SelectMany((pair, token) =>
            {
                if (!pair.Right.TryGetEntries(pair.Left.Key, out var entries))
                    return [];

                return entries.Select(x => mapper(pair.Left.Key, x, pair.Left.Source));
            });
    }

    public static IncrementalValueProvider<Grouping<TKey, TResult>> Mixin<TKey, TElement, TSource, TResult>(
        this IncrementalValueProvider<Grouping<TKey, TElement>> group,
        IncrementalValueProvider<TSource> source,
        Func<TKey, ImmutableArray<TElement>, TSource, IEnumerable<TResult>> resultSelector
    )
    {
        return Grouping<TKey, TResult>.CreateAround(
            source
                .Combine(group)
                .SelectMany((pair, _) =>
                    pair.Right.Keys.SelectMany(key =>
                        resultSelector(
                            key,
                            pair.Right.GetEntriesOrEmpty(key),
                            pair.Left
                        ).Select(x =>
                            (Key: key, Result: x)
                        )
                    )
                ),
            x => x.Key,
            x => x.Result
        );
    }

    public static IncrementalValueProvider<Grouping<TKey, TResult>> Mixin<TKey, TElement, TSource, TResult>(
        this IncrementalValueProvider<Grouping<TKey, TElement>> group,
        IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TKey, ImmutableArray<TElement>, TSource, IEnumerable<TResult>> resultSelector)
    {
        return Grouping<TKey, TResult>.CreateAround(
            source
                .Select((x, _) => (Source: x, Key: keySelector(x)))
                .Combine(group)
                .SelectMany((pair, _) =>
                    resultSelector(
                        pair.Left.Key,
                        pair.Right.GetEntriesOrEmpty(pair.Left.Key),
                        pair.Left.Source
                    ).Select(result =>
                        (Key: pair.Left.Key, Result: result)
                    )
                ),
            x => x.Key,
            x => x.Result
        );
    }

    public static IncrementalValuesProvider<(TSource Left, ImmutableEquatableArray<TElement> Right)> Combine<
        TSource,
        TKey,
        TElement
    >(
        this IncrementalValuesProvider<TSource> provider,
        IncrementalValueProvider<Grouping<TKey, TElement>> group,
        Func<TSource, TKey> keySelector
    )
        where TElement : IEquatable<TElement>
        => Combine(provider, group, keySelector, (a, b) => (a, b));

    public static IncrementalValuesProvider<TResult> Combine<
        TSource,
        TKey,
        TElement,
        TResult
    >(
        this IncrementalValuesProvider<TSource> provider,
        IncrementalValueProvider<Grouping<TKey, TElement>> group,
        Func<TSource, TKey> keySelector,
        Func<TSource, ImmutableEquatableArray<TElement>, TResult> mapper
    )
        where TElement : IEquatable<TElement>
    {
        return provider
            .Select((source, _) => (Source: source, Key: keySelector(source)))
            .Combine(group)
            .Select((pair, _) =>
            {
                var entries = pair.Right.TryGetEntries(pair.Left.Key, out var elements)
                    ? new ImmutableEquatableArray<TElement>(elements)
                    : ImmutableEquatableArray<TElement>.Empty;

                return mapper(pair.Left.Source, entries);
            });
    }

    public static IncrementalValuesProvider<TResult> Combine<
        TSource,
        TKey,
        TElement,
        TResult
    >(
        this IncrementalValuesProvider<TSource> provider,
        IncrementalValueProvider<Grouping<TKey, TElement>> group,
        Func<TSource, TKey> keySelector,
        Func<TSource, ImmutableEquatableArray<TElement>, Grouping<TKey, TElement>, TResult> mapper
    )
        where TElement : IEquatable<TElement>
    {
        return provider
            .Select((source, _) => (Source: source, Key: keySelector(source)))
            .Combine(group)
            .Select((pair, _) =>
            {
                var entries = pair.Right.TryGetEntries(pair.Left.Key, out var elements)
                    ? new ImmutableEquatableArray<TElement>(elements)
                    : ImmutableEquatableArray<TElement>.Empty;

                return mapper(pair.Left.Source, entries, pair.Right);
            });
    }
}