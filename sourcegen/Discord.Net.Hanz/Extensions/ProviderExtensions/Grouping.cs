using System.Collections.Immutable;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz;

public sealed class IncrementalGroupingProvider<TKey, TValue>
{
    public IEnumerable<(TKey Key, TValue Value)> Entries
        => _groups.SelectMany(kvp => kvp.Value.Select(value => (kvp.Key, value)));

    public IEnumerable<TKey> Keys => _groups.Keys;
    public IEnumerable<TValue> Values => _groups.Values.SelectMany(x => x);

    public IncrementalValuesProvider<(TKey Key, TValue Value)> EntriesProvider { get; }
    public IncrementalValuesProvider<TKey> KeysProvider { get; }
    public IncrementalValuesProvider<TValue> ValuesProvider { get; }

    private readonly Dictionary<TKey, HashSet<TValue>> _groups;
    private readonly object _lock = new();

    public IncrementalGroupingProvider(
        IncrementalValuesProvider<Introspected<(TKey, TValue)>> provider)
    {
        _groups = [];

        EntriesProvider = provider
            .MaybeSelect(introspected =>
            {
                var (key, value) = introspected.Value;

                switch (introspected.State)
                {
                    case State.Added:
                        lock (_lock)
                        {
                            if (!_groups.TryGetValue(key, out var values))
                                _groups[key] = values = [];

                            values.Add(value);
                        }

                        goto case State.Cached;
                    case State.Removed:
                        lock (_lock)
                        {
                            if (_groups.TryGetValue(key, out var values))
                            {
                                values.Remove(value);

                                if (values.Count == 0)
                                    _groups.Remove(key);
                            }
                        }

                        return default;
                    case State.Cached:
                        return (key, value).Some();
                }

                return default;
            });

        KeysProvider = EntriesProvider.Select((x, _) => x.Key);
        ValuesProvider = EntriesProvider.Select((x, _) => x.Value);
    }

    public bool HasEntries(TKey key)
        => TryGetValues(key, out var values) && values.Length > 0;

    public bool TryGetValues(TKey key, out ImmutableArray<TValue> values)
    {
        HashSet<TValue>? set;

        lock (_lock)
        {
            if (!_groups.TryGetValue(key, out set))
                return false;
        }

        values = set.ToImmutableArray();
        return true;
    }

    public ImmutableArray<TValue> GetValuesOrEmpty(TKey key)
        => TryGetValues(key, out var values) ? values : ImmutableArray<TValue>.Empty;

    public IncrementalValuesProvider<TResult> JoinByKey<TSource, TResult>(
        IncrementalKeyValueProvider<TKey, TSource> source,
        Func<TKey, ImmutableArray<TValue>, TSource, TResult> resultSelector
    )
    {
        return source
            .EntriesProvider
            .Combine(EntriesProvider.Collect())
            .SelectMany((pair, _) =>
            {
                var (key, value) = pair.Left;

                HashSet<TValue> values;

                lock (_lock)
                {
                    if (!_groups.TryGetValue(key, out values))
                        return ImmutableArray<TResult>.Empty;
                }

                return ImmutableArray.Create(resultSelector(key, values.ToImmutableArray(), value));
            });
    }

    public IncrementalValuesProvider<TResult> JoinByKey<TSource, TResult>(
        IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TKey, ImmutableArray<TValue>, TSource, TResult> resultSelector
    )
    {
        return source
            .Combine(EntriesProvider.Collect())
            .SelectMany(ImmutableArray<TResult> (source, _) =>
            {
                var key = keySelector(source.Left);

                HashSet<TValue> values;

                lock (_lock)
                {
                    if (!_groups.TryGetValue(key, out values))
                        return ImmutableArray<TResult>.Empty;
                }

                return ImmutableArray.Create(resultSelector(key, values.ToImmutableArray(), source.Left));
            });
    }

    public IncrementalGroupingProvider<TKey, TResult> MapValues<TResult>(
        Func<TKey, TValue, TResult> selector
    ) => Map((key, value) => (key, selector(key, value)));

    public IncrementalGroupingProvider<TResult, TValue> MapKeys<TResult>(
        Func<TKey, TValue, TResult> selector
    ) => Map((key, value) => (selector(key, value), value));

    public IncrementalGroupingProvider<TNewKey, TNewValue> Map<TNewKey, TNewValue>(
        Func<TKey, TValue, (TNewKey, TNewValue)> selector
    ) => new(
        EntriesProvider
            .Select((kvp, _) => selector(kvp.Key, kvp.Value))
            .AsIntrospected()
    );

    public IncrementalGroupingProvider<TKey, TResult> MaybeMapValues<TResult>(
        Func<TKey, TValue, Optional<TResult>> selector,
        bool allowDefault = false,
        TResult defaultValue = default
    ) => MaybeMap((key, value) =>
    {
        var result = selector(key, value);

        return result.HasValue
            ? new Optional<(TKey, TResult)>((key, result.Value))
            : default;
    }, allowDefault, defaultValueFactory: (key, value) => (key, defaultValue));

    public IncrementalGroupingProvider<TResult, TValue> MaybeMapKeys<TResult>(
        Func<TKey, TValue, Optional<TResult>> selector,
        bool allowDefault = false,
        TResult defaultValue = default
    ) => MaybeMap((key, value) =>
    {
        var result = selector(key, value);

        return result.HasValue
            ? new Optional<(TResult, TValue)>((result.Value, value))
            : default;
    }, allowDefault, defaultValueFactory: (key, value) => (defaultValue, value));

    public IncrementalGroupingProvider<TNewKey, TNewValue> MaybeMap<TNewKey, TNewValue>(
        Func<TKey, TValue, Optional<(TNewKey, TNewValue)>> selector,
        bool allowDefault = false,
        Func<TKey, TValue, (TNewKey, TNewValue)>? defaultValueFactory = null
    ) => new(
        EntriesProvider
            .SelectMany((kvp, _) =>
            {
                var result = selector(kvp.Key, kvp.Value);

                if (result.HasValue)
                    return ImmutableArray.Create(result.Value);

                if (allowDefault)
                    return ImmutableArray.Create(
                        defaultValueFactory is not null
                            ? defaultValueFactory(kvp.Key, kvp.Value)
                            : (default, default)
                    );

                return ImmutableArray<(TNewKey, TNewValue)>.Empty;
            })
            .AsIntrospected()
    );

    public IncrementalGroupingProvider<TKey, TResult> MapValuesVia<TSource, TResult>(
        IncrementalKeyValueProvider<TKey, TSource> source,
        Func<TKey, TValue, TSource, TResult> selector,
        TSource defaultValue = default,
        bool includeDefault = false
    )
    {
        return new IncrementalGroupingProvider<TKey, TResult>(
            EntriesProvider
                .Combine(source.EntriesProvider.Collect())
                .MaybeSelect(pair =>
                {
                    if (!source.TryGetValue(pair.Left.Key, out var value))
                    {
                        if (!includeDefault)
                            return default;

                        value = defaultValue;
                    }

                    return (pair.Left.Key, selector(pair.Left.Key, pair.Left.Value, value)).Some();
                })
                .AsIntrospected()
        );
    }

    public IncrementalGroupingProvider<TKey, TResult> TransformValuesVia<TSource, TResult>(
        IncrementalKeyValueProvider<TValue, TSource> source,
        Func<TKey, TValue, TSource, TResult> selector,
        TSource defaultValue = default,
        bool includeDefault = false
    )
    {
        return new IncrementalGroupingProvider<TKey, TResult>(
            EntriesProvider
                .Combine(source.EntriesProvider.Collect())
                .SelectMany(ImmutableArray<(TKey, TResult)> (pair, _) =>
                {
                    if (!source.TryGetValue(pair.Left.Value, out var value))
                    {
                        if (!includeDefault)
                            return ImmutableArray<(TKey, TResult)>.Empty;

                        value = defaultValue;
                    }

                    return ImmutableArray.Create((pair.Left.Key, selector(pair.Left.Key, pair.Left.Value, value)));
                })
                .AsIntrospected()
        );
    }

    public IncrementalGroupingProvider<TKey, TSource> TransformValuesVia<TSource>(
        IncrementalKeyValueProvider<TValue, TSource> source,
        TSource defaultValue = default,
        bool includeDefault = false
    )
    {
        return new IncrementalGroupingProvider<TKey, TSource>(
            EntriesProvider
                .Combine(source.EntriesProvider.Collect())
                .SelectMany(ImmutableArray<(TKey, TSource)> (pair, _) =>
                {
                    if (!source.TryGetValue(pair.Left.Value, out var value))
                    {
                        return includeDefault
                            ? ImmutableArray.Create((pair.Left.Key, defaultValue))
                            : ImmutableArray<(TKey, TSource)>.Empty;
                    }

                    return ImmutableArray.Create((pair.Left.Key, value));
                })
                .AsIntrospected()
        );
    }

    public IncrementalGroupingProvider<TSource, TValue> TransformKeysVia<TSource>(
        IncrementalKeyValueProvider<TKey, TSource> source
    ) => new(
        EntriesProvider
            .Combine(source.EntriesProvider.Collect())
            .MaybeSelect(pair =>
            {
                if (!source.TryGetValue(pair.Left.Key, out var value))
                    return default;

                return (value, pair.Left.Value).Some();
            })
            .AsIntrospected()
    );

    public IncrementalKeyValueProvider<TKey, TResult> ToKeyed<TResult>(
        Func<TKey, ImmutableArray<TValue>, TResult> resultSelector
    ) => new(
        KeysProvider
            .Select(Keyed<TKey, TResult> (key, _) => (key, resultSelector(key, GetValuesOrEmpty(key))))
            .AsIntrospected()
    );

    public IncrementalKeyValueProvider<TNewKey, TResult> ToKeyed<TNewKey, TResult>(
        Func<TKey, ImmutableArray<TValue>, (TNewKey, TResult)> resultSelector
    ) => new(
        KeysProvider
            .Select(Keyed<TNewKey, TResult> (key, _) => resultSelector(key, GetValuesOrEmpty(key)))
            .AsIntrospected()
    );
}

public static class GroupingExtensions
{
    public static IncrementalGroupingProvider<TKey, TSource> GroupBy<TSource, TKey>(
        this IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector
    )
    {
        return new(
            source
                .Select((x, _) => (keySelector(x), x))
                .AsIntrospected()
        );
    }

    public static IncrementalGroupingProvider<TKey, TValue> GroupBy<TSource, TKey, TValue>(
        this IncrementalValuesProvider<TSource> source,
        Func<TSource, (TKey, TValue)> selector
    )
    {
        return new(
            source
                .Select((x, _) => selector(x))
                .AsIntrospected()
        );
    }

    public static IncrementalGroupingProvider<TKey, TValue> GroupManyBy<TSource, TKey, TValue>(
        this IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector,
        Func<TSource, IEnumerable<TValue>> valuesSelector,
        bool allowEmptyValues = false,
        TValue defaultValue = default)
    {
        return new(
            source
                .SelectMany(ImmutableArray<(TKey, TValue)> (x, _) =>
                {
                    var key = keySelector(x);
                    var values = valuesSelector(x).ToImmutableArray();

                    if (values.Length == 0)
                    {
                        if (!allowEmptyValues)
                            return ImmutableArray<(TKey, TValue)>.Empty;

                        return ImmutableArray.Create((key, defaultValue));
                    }

                    return ImmutableArray.CreateRange(values.Select(x => (key, x)));
                })
                .AsIntrospected()
        );
    }

    public static IncrementalValuesProvider<TResult> Select<TKey, TValue, TResult>(
        this IncrementalGroupingProvider<TKey, TValue> group,
        Func<TKey, ImmutableArray<TValue>, TResult> selector
    ) => group.KeysProvider.Select((key, _) => selector(key, group.GetValuesOrEmpty(key)));

    public static IncrementalValuesProvider<TResult> Select<TKey, TValue, TResult>(
        this IncrementalGroupingProvider<TKey, TValue> group,
        Func<TKey, TValue, TResult> selector
    ) => group.EntriesProvider.Select((entry, _) => selector(entry.Key, entry.Value));
}


// using System.Collections.Immutable;
// using Discord.Net.Hanz.Utils.Bakery;
// using Microsoft.CodeAnalysis;
//
// namespace Discord.Net.Hanz;
//
// public static class GroupingExtensions
// {
//     public static ImmutableEquatableArray<TElement> GetEntriesOrEmptyEquatable<TKey, TElement>(
//         this Grouping<TKey, TElement> grouping,
//         TKey key
//     )
//         where TElement : IEquatable<TElement>
//     {
//         if (grouping.TryGetEntries(key, out var entries))
//             return entries.ToImmutableEquatableArray();
//
//         return ImmutableEquatableArray<TElement>.Empty;
//     }
// }
//
// public sealed class Grouping<TKey, TElement>
// {
//     private enum State
//     {
//         Added,
//         Removed,
//         Cached
//     }
//
//     private readonly record struct Entry(
//         TKey Key,
//         TElement Element,
//         State State
//     );
//
//     public Dictionary<TKey, HashSet<TElement>>.KeyCollection Keys => _entries.Keys;
//     public IEnumerable<TElement> Elements => _entries.Values.SelectMany(x => x);
//
//     public IEnumerable<(TKey Key, TElement Element)> Entries
//         => Keys.SelectMany(key => _entries[key].Select(element => (key, element)));
//
//     private readonly Dictionary<TKey, HashSet<TElement>> _entries = [];
//
//     private readonly object _lock = new();
//
//     private int _version;
//
//     public bool Has((TKey Key, TElement Element) tuple)
//         => _entries.ContainsKey(tuple.Key) && _entries[tuple.Key].Contains(tuple.Element);
//
//     public bool TryGetEntries(TKey key, out HashSet<TElement> entries)
//         => _entries.TryGetValue(key, out entries) && entries.Count > 0;
//
//     public ImmutableArray<TElement> GetEntriesOrEmpty(TKey key)
//     {
//         if (_entries.TryGetValue(key, out var entries))
//             return entries.ToImmutableArray();
//
//         return ImmutableArray<TElement>.Empty;
//     }
//
//     private Grouping<TKey, TElement> ProcessBatch(
//         ImmutableArray<(TKey Key, TElement? Element, bool Use)> entries,
//         CancellationToken token)
//     {
//         lock (_lock)
//         {
//             var keys = new List<TKey>();
//             foreach (var grouping in entries.GroupBy(x => x.Key))
//             {
//                 keys.Add(grouping.Key);
//
//                 if (!_entries.TryGetValue(grouping.Key, out var set))
//                     _entries[grouping.Key] = set = [];
//                 
//                 set.Clear();
//                 set.UnionWith(grouping.Where(x => x.Use).Select(x => x.Element));
//             }
//
//             foreach (var removedKey in _entries.Keys.Except(keys))
//             {
//                 _entries.Remove(removedKey);
//             }
//
//             unchecked
//             {
//                 _version++;
//             }
//         }
//
//         return this;
//     }
//
//     public override int GetHashCode()
//         => _version;
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> CreateAround<TSource>(
//         IncrementalValuesProvider<TSource> provider,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, TElement> elementSelector
//     ) => Create(
//         provider.Select((source, _) => (keySelector(source), elementSelector(source), true))
//     );
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> CreateAround<TSource>(
//         IncrementalValueProvider<ImmutableArray<TSource>> provider,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, TElement> elementSelector
//     ) => Create(
//         provider.SelectMany((sources, _) =>
//             sources.Select(source => (keySelector(source), elementSelector(source), true))
//         )
//     );
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> CreateAround<TSource>(
//         IncrementalValueProvider<ImmutableEquatableArray<TSource>> provider,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, TElement> elementSelector
//     )
//         where TSource : IEquatable<TSource>
//     {
//         return Create(
//             provider.SelectMany((sources, _) =>
//                 sources.Select(source => (keySelector(source), elementSelector(source), true))
//             )
//         );
//     }
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> CreateAroundMany<TSource>(
//         IncrementalValuesProvider<TSource> provider,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, IEnumerable<TElement>> elementSelector
//     ) => Create(
//         provider.SelectMany((source, _) =>
//         {
//             var key = keySelector(source);
//             return elementSelector(source)
//                 .Select(element => (key, element, true))
//                 .Prepend((key, default(TElement), false));
//         })
//     );
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> CreateAroundMany<TSource>(
//         IncrementalValueProvider<ImmutableArray<TSource>> provider,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, IEnumerable<TElement>> elementSelector
//     ) => Create(
//         provider.SelectMany((source, _) => source
//             .SelectMany(source =>
//             {
//                 var key = keySelector(source);
//                 return elementSelector(source)
//                     .Select(element => (key, element, true))
//                     .Prepend((key, default(TElement), false));
//             })
//         )
//     );
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> Create(
//         IncrementalValuesProvider<(TKey, TElement, bool)> source
//     )
//     {
//         var grouping = new Grouping<TKey, TElement>();
//
//         return source
//             .Collect()
//             .Select(grouping.ProcessBatch);
//     }
// }
//
// public static partial class ProviderExtensions
// {
//     public static IncrementalValuesProvider<TElement> Values<TKey, TElement>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group
//     ) => group.SelectMany((x, _) => x.Elements);
//
//     public static IncrementalValueProvider<Grouping<TKey, TResult>> Map<TKey, TElement, TResult>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         Func<TElement, TResult> selector
//     ) => Map(group, (_, element) => selector(element));
//
//     public static IncrementalValueProvider<Grouping<TKey, TResult>> Map<TKey, TElement, TResult>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         Func<TKey, TElement, TResult> selector
//     )=> Map(group, (key, element, _) => selector(key, element));
//     
//     public static IncrementalValueProvider<Grouping<TKey, TResult>> Map<TKey, TElement, TResult>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         Func<TKey, TElement, Grouping<TKey, TElement>, TResult> selector
//     ) => Grouping<TKey, TResult>.Create(
//         group
//             .SelectMany((group, _) =>
//                 group.Keys
//                     .SelectMany(key => group
//                         .GetEntriesOrEmpty(key)
//                         .Select(entry => (key, selector(key, entry, group), true))
//                         .Prepend((key, default, false))
//                     )
//                     .ToImmutableArray()
//             )
//     );
//
//     public static IncrementalValuesProvider<TResult> Select<TKey, TElement, TResult>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         Func<TKey, TElement, TResult> selector
//     ) => group.SelectMany((group, _) => group
//         .Keys
//         .SelectMany(key => group
//             .GetEntriesOrEmpty(key)
//             .Select(element => selector(key, element))
//         )
//     );
//
//     public static IncrementalValuesProvider<TResult> Select<TKey, TElement, TResult>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         Func<TKey, ImmutableArray<TElement>, TResult> selector
//     ) => group.SelectMany((group, _) => group
//         .Keys
//         .Select(key => selector(key, group.GetEntriesOrEmpty(key)))
//     );
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupBy<TKey, TElement>(
//         this IncrementalValuesProvider<TElement> source,
//         Func<TElement, TKey> keySelector
//     ) => Grouping<TKey, TElement>.CreateAround(source, keySelector, x => x);
//
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupBy<TKey, TElement>(
//         this IncrementalValueProvider<ImmutableArray<TElement>> source,
//         Func<TElement, TKey> keySelector
//     ) => Grouping<TKey, TElement>.CreateAround(source, keySelector, x => x);
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupBy<TKey, TElement>(
//         this IncrementalValueProvider<ImmutableEquatableArray<TElement>> source,
//         Func<TElement, TKey> keySelector
//     )
//         where TElement : IEquatable<TElement>
//         => Grouping<TKey, TElement>.CreateAround(source, keySelector, x => x);
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(
//         this IncrementalValuesProvider<TSource> source,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, TElement> elementSelector
//     ) => Grouping<TKey, TElement>.CreateAround(source, keySelector, elementSelector);
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupManyBy<TSource, TKey, TElement>(
//         this IncrementalValuesProvider<TSource> source,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, IEnumerable<TElement>> elementSelector
//     ) => Grouping<TKey, TElement>.CreateAroundMany(source, keySelector, elementSelector);
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupManyBy<TSource, TKey, TElement>(
//         this IncrementalValueProvider<ImmutableArray<TSource>> source,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, IEnumerable<TElement>> elementSelector
//     ) => Grouping<TKey, TElement>.CreateAroundMany(source, keySelector, elementSelector);
//
//     public static IncrementalValueProvider<Grouping<TKey, TElement>> GroupBy<TSource, TKey, TElement>(
//         this IncrementalValueProvider<ImmutableArray<TSource>> source,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, TElement> elementSelector
//     ) => Grouping<TKey, TElement>.CreateAround(source, keySelector, elementSelector);
//
//     public static IncrementalValuesProvider<TResult> Pair<TKey, TElement, TSource, TResult>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         IncrementalValueProvider<Keyed<TKey, TSource>> source,
//         Func<TKey, TElement, TSource, TResult> mapper
//     )
//     {
//         return group
//             .Combine(source)
//             .SelectMany((pair, _) =>
//                 pair.Left.Keys
//                     .Where(pair.Right.ContainsKey)
//                     .SelectMany(key =>
//                         pair.Left
//                             .GetEntriesOrEmpty(key)
//                             .Select(element =>
//                                 mapper(key, element, pair.Right.GetValueOrDefault(key))
//                             )
//                     )
//             );
//     }
//
//     public static IncrementalValuesProvider<TResult> Pair<TKey, TElement, TSource, TResult>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         IncrementalValuesProvider<TSource> source,
//         Func<TSource, TKey> keySelector,
//         Func<TKey, TElement, TSource, TResult> mapper
//     )
//     {
//         return source
//             .Select((x, _) => (Source: x, Key: keySelector(x)))
//             .Combine(group)
//             .SelectMany((pair, token) =>
//             {
//                 if (!pair.Right.TryGetEntries(pair.Left.Key, out var entries))
//                     return [];
//
//                 return entries.Select(x => mapper(pair.Left.Key, x, pair.Left.Source));
//             });
//     }
//
//     public static IncrementalValueProvider<Grouping<TOther, TElement>> Pair<TKey, TElement, TOther>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         IncrementalValueProvider<Keyed<TKey, TOther>> other
//     )
//     {
//         return Grouping<TOther, TElement>.Create(
//             group
//                 .Combine(other)
//                 .SelectMany((pair, _) => pair
//                     .Right
//                     .Keys
//                     .SelectMany(key => pair
//                         .Left
//                         .GetEntriesOrEmpty(key)
//                         .Select(entry =>
//                             (
//                                 pair.Right.GetValue(key),
//                                 entry,
//                                 true
//                             )
//                         )
//                         .Prepend((pair.Right.GetValue(key), default, false))
//                     )
//                 )
//         );
//     }
//
//     public static IncrementalValueProvider<Keyed<TKey, TResult>> Join<TKey, TElement, TSource, TResult>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         IncrementalValueProvider<TSource> source,
//         Func<TKey, ImmutableArray<TElement>, TSource, TResult> resultSelector
//     )
//     {
//         return Keyed<TKey, TResult>.Create(
//             source
//                 .Combine(group)
//                 .Select((pair, _) =>
//                     pair.Right.Keys
//                         .Select(key =>
//                             (
//                                 Key: key,
//                                 Result: resultSelector(
//                                     key,
//                                     pair.Right.GetEntriesOrEmpty(key),
//                                     pair.Left
//                                 )
//                             )
//                         )
//                         .ToImmutableArray()
//                 )
//         );
//     }
//
//     public static IncrementalValueProvider<Grouping<TKey, TResult>> Mixin<TKey, TElement, TSource, TResult>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         IncrementalValueProvider<TSource> source,
//         Func<TKey, ImmutableArray<TElement>, TSource, IEnumerable<TResult>> resultSelector
//     )
//     {
//         return Grouping<TKey, TResult>.CreateAround(
//             source
//                 .Combine(group)
//                 .SelectMany((pair, _) =>
//                     pair.Right.Keys.SelectMany(key =>
//                         resultSelector(
//                             key,
//                             pair.Right.GetEntriesOrEmpty(key),
//                             pair.Left
//                         ).Select(x =>
//                             (Key: key, Result: x)
//                         )
//                     )
//                 ),
//             x => x.Key,
//             x => x.Result
//         );
//     }
//
//     public static IncrementalValueProvider<Grouping<TKey, TResult>> Mixin<TKey, TElement, TSource, TResult>(
//         this IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         IncrementalValuesProvider<TSource> source,
//         Func<TSource, TKey> keySelector,
//         Func<TKey, ImmutableArray<TElement>, TSource, IEnumerable<TResult>> resultSelector)
//     {
//         return Grouping<TKey, TResult>.CreateAround(
//             source
//                 .Select((x, _) => (Source: x, Key: keySelector(x)))
//                 .Combine(group)
//                 .SelectMany((pair, _) =>
//                     resultSelector(
//                         pair.Left.Key,
//                         pair.Right.GetEntriesOrEmpty(pair.Left.Key),
//                         pair.Left.Source
//                     ).Select(result =>
//                         (Key: pair.Left.Key, Result: result)
//                     )
//                 ),
//             x => x.Key,
//             x => x.Result
//         );
//     }
//
//     public static IncrementalValuesProvider<(TSource Left, ImmutableEquatableArray<TElement> Right)> Combine<
//         TSource,
//         TKey,
//         TElement
//     >(
//         this IncrementalValuesProvider<TSource> provider,
//         IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         Func<TSource, TKey> keySelector
//     )
//         where TElement : IEquatable<TElement>
//         => Combine(provider, group, keySelector, (a, b) => (a, b));
//
//     public static IncrementalValuesProvider<TResult> Combine<
//         TSource,
//         TKey,
//         TElement,
//         TResult
//     >(
//         this IncrementalValuesProvider<TSource> provider,
//         IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, ImmutableEquatableArray<TElement>, TResult> mapper
//     )
//         where TElement : IEquatable<TElement>
//     {
//         return provider
//             .Select((source, _) => (Source: source, Key: keySelector(source)))
//             .Combine(group)
//             .Select((pair, _) =>
//             {
//                 var entries = pair.Right.TryGetEntries(pair.Left.Key, out var elements)
//                     ? new ImmutableEquatableArray<TElement>(elements)
//                     : ImmutableEquatableArray<TElement>.Empty;
//
//                 return mapper(pair.Left.Source, entries);
//             });
//     }
//
//     public static IncrementalValuesProvider<TResult> Combine<
//         TSource,
//         TKey,
//         TElement,
//         TResult
//     >(
//         this IncrementalValuesProvider<TSource> provider,
//         IncrementalValueProvider<Grouping<TKey, TElement>> group,
//         Func<TSource, TKey> keySelector,
//         Func<TSource, ImmutableEquatableArray<TElement>, Grouping<TKey, TElement>, TResult> mapper
//     )
//         where TElement : IEquatable<TElement>
//     {
//         return provider
//             .Select((source, _) => (Source: source, Key: keySelector(source)))
//             .Combine(group)
//             .Select((pair, _) =>
//             {
//                 var entries = pair.Right.TryGetEntries(pair.Left.Key, out var elements)
//                     ? new ImmutableEquatableArray<TElement>(elements)
//                     : ImmutableEquatableArray<TElement>.Empty;
//
//                 return mapper(pair.Left.Source, entries, pair.Right);
//             });
//     }
// }