using System.Collections.Immutable;
using Discord.Net.Hanz.Utils.Bakery;
using Microsoft.CodeAnalysis;

namespace Discord.Net.Hanz;

public readonly record struct Keyed<TKey, TValue>(
    TKey Key,
    TValue Value
);

public sealed class KeyedCollection<TKey, TValue>
{
    private readonly Func<TValue, TKey> _keySelector;
    private readonly Dictionary<TKey, TValue> _dictionary;
    private HashSet<TValue> _buffer;

    private int _hash;
    
    private KeyedCollection(Func<TValue, TKey> keySelector)
    {
        _keySelector = keySelector;
        _dictionary = [];
        _buffer = [];
    }

    private TValue OnModified(TValue value)
    {
        _buffer.Add(value);
        return value;
    }

    private KeyedCollection<TKey, TValue> OnBatch(ImmutableArray<TValue> values)
    {
        _hash = HashCode.OfEach(values);
        
        if (_dictionary.Count == 0)
        {
            foreach (var value in values)
            {
                _dictionary[_keySelector(value)] = value;
            }

            _buffer.Clear();
            return this;
        }

        if (values.Length - _buffer.Count < _dictionary.Count)
        {
            // some were removed
            foreach (var value in values)
            {
                if (!_dictionary.ContainsValue(value))
                    _dictionary.Remove(_keySelector(value));
            }
        }

        foreach (var value in _buffer)
        {
            _dictionary[_keySelector(value)] = value;
        }

        _buffer.Clear();

        return this;
    }

    public static IncrementalValueProvider<KeyedCollection<TKey, TValue>> Create(
        IncrementalValuesProvider<TValue> provider,
        Func<TValue, TKey> keySelector
    )
    {
        var collection = new KeyedCollection<TKey, TValue>(keySelector);

        return provider
            .Select((x, _) => collection.OnModified(x))
            .Collect()
            .Select((values, _) => collection.OnBatch(values));
    }

    public static IncrementalValuesProvider<Keyed<TSource, TValue>> Join<TSource, TKey, TValue>(
        IncrementalValuesProvider<TSource> provider,
        IncrementalValueProvider<KeyedCollection<TKey, TValue>> collection,
        Func<TSource, TKey> keySelector)
    {
        return provider
            .Select((value, _) => (Key: keySelector(value), Value: value))
            .Combine(collection)
            .SelectMany((x, _) =>
                x.Right._dictionary.TryGetValue(x.Left.Key, out var match)
                    ? ImmutableArray.Create(new Keyed<TSource, TValue>(x.Left.Value, match))
                    : ImmutableArray<Keyed<TSource, TValue>>.Empty
            );
    }

    public override int GetHashCode()
        => _hash;

    public override bool Equals(object? obj)
        => obj is KeyedCollection<TKey, TValue> collection && collection._hash == _hash;
}

public static partial class ProviderExtensions
{
    public static IncrementalValueProvider<KeyedCollection<TKey, TValue>> ToKeyed<TKey, TValue>(
        this IncrementalValuesProvider<TValue> provider,
        Func<TValue, TKey> keySelector
    ) => KeyedCollection<TKey, TValue>.Create(provider, keySelector);

    public static IncrementalValuesProvider<Keyed<TSource, TValue>> Join<TSource, TKey, TValue>(
        this IncrementalValueProvider<KeyedCollection<TKey, TValue>> collection,
        IncrementalValuesProvider<TSource> source,
        Func<TSource, TKey> keySelector
    ) => KeyedCollection<TKey, TValue>.Join(source, collection, keySelector);
}