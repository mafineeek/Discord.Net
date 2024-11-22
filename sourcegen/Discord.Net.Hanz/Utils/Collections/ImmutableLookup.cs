using System.Collections;
using System.Diagnostics;

namespace Discord.Net.Hanz;

public sealed class ImmutableLookup<TKey, TValue> : 
    ILookup<TKey, TValue>,
    IEquatable<ImmutableLookup<TKey, TValue>>
{
    public int Count => _count;
    
    public IEnumerable<TValue> this[TKey key] => Get(key);

    private readonly Grouping? _root;
    
    private readonly int _count;
    private readonly int _keyCount;

    private readonly IEqualityComparer<TKey> _keyComparer;
    private readonly IEqualityComparer<TValue> _valueComparer;

    private ImmutableLookup(
        Grouping? root,
        int keyCount,
        int count,
        IEqualityComparer<TKey>? keyComparer = null,
        IEqualityComparer<TValue>? valueComparer = null)
    {
        _root = root;
        _keyCount = keyCount;
        _count = count;
        _keyComparer = keyComparer ?? EqualityComparer<TKey>.Default;
        _valueComparer = valueComparer ?? EqualityComparer<TValue>.Default;
    }

    public bool Contains(TKey key)
    {
        if (_root is null) return false;
        var head = _root;

        return TryGet(ref head, _keyComparer.GetHashCode(key));
    }

    public bool TryGetValues(TKey key, out Grouping grouping)
    {
        if (_root is null)
        {
            grouping = null!;
            return false;
        }

        grouping = _root;

        return TryGet(ref grouping, _keyComparer.GetHashCode(key));
    }

    public ImmutableLookup<TKey, TValue> Add(TKey key, TValue value)
    {
        if (_root is null)
            return Create(key, value);

        var node = _root;
        var keyHash = _keyComparer.GetHashCode(key);
        var valueHash = _valueComparer.GetHashCode(value);

        if (TryGet(ref node, keyHash))
        {
            if (node.ValueNode is null)
            {
                return new ImmutableLookup<TKey, TValue>(
                    GetRoot(node.WithValue(new(value, valueHash))).CloneGreen(),
                    _keyCount,
                    _count + 1,
                    _keyComparer,
                    _valueComparer
                );
            }

            var valueNode = node.ValueNode;

            if (!TryAdd(ref valueNode, value, valueHash))
                return this;

            return new ImmutableLookup<TKey, TValue>(
                GetRoot(node.WithValue(GetRoot(valueNode).CloneRed())).CloneRed(),
                _keyCount,
                _count + 1,
                _keyComparer,
                _valueComparer
            );
        }

        Debug.Assert(node != null, "cannot be null since we null check _root");

        var keyNode = new Grouping(key, keyHash, new ValueNode(value, valueHash), parent: node);

        return FromNode(
            keyHash < node.KeyHash
                ? node.WithLeft(keyNode)
                : node.WithRight(keyNode),
            _keyCount + 1,
            _count + 1
        );
    }

    public ImmutableLookup<TKey, TValue> AddRange(TKey key, params TValue[] values)
    {
        if (_root is null)
            return Create(key, values);

        switch (values.Length)
        {
            case 0: return this;
            case 1: return Add(key, values[0]);
            default:
                var count = 0;
                
                var keyHash = _keyComparer.GetHashCode(key);
                var node = _root;
                
                if (TryGet(ref node, keyHash))
                {
                    
                    if (node.ValueNode is null)
                        return FromNode(
                            node.WithValue(BuildValueNode(values, _valueComparer, out count)),
                            _keyCount,
                            _count + count
                        );
                    
                    var valueNode = node.ValueNode;

                    for (var i = 0; i < values.Length; i++)
                    {
                        if (TryAdd(ref valueNode, values[i], _valueComparer.GetHashCode(values[i])))
                            count++;

                        if (valueNode != node.ValueNode)
                            valueNode = GetRoot(valueNode.CloneRed());
                    }

                    if (count == 0)
                        return this;
                    
                    return FromNode(node.WithValue(valueNode), _keyCount, _count + count);
                }
                
                Debug.Assert(node != null, "cannot be null since we null check _root");

                var keyNode = new Grouping(key, keyHash, BuildValueNode(values, _valueComparer, out count), parent: node);

                return FromNode(
                    keyHash < node.KeyHash
                        ? node.WithLeft(keyNode)
                        : node.WithRight(keyNode),
                    _keyCount + 1,
                    _count + count
                );
        }
    }

    public ImmutableLookup<TKey, TValue> AddRange(IEnumerable<(TKey, TValue)> entries)
    {
        var lookup = this;

        foreach (var (key, value) in entries)
        {
            lookup = lookup.Add(key, value);
        }

        return lookup;
    }

    public ImmutableLookup<TKey, TValue> Remove(TKey key)
    {
        var keyHash = _keyComparer.GetHashCode(key);
        var node = _root;

        if (!TryGet(ref node, keyHash))
            return this;

        if (_keyCount == 1)
            return new(null, 0, 0, _keyComparer, _valueComparer);

        return FromNode(
            RemoveNode(node),
            _keyCount - 1,
            _count - node.Size,
            false
        );
    }

    public ImmutableLookup<TKey, TValue> Remove(TKey key, TValue value)
    {
        var keyHash = _keyComparer.GetHashCode(key);
        var node = _root;

        if (!TryGet(ref node, keyHash))
            return this;

        var valueHash = _valueComparer.GetHashCode(value);
        var valueNode = node.ValueNode;

        if (!TryGet(ref valueNode, valueHash))
            return this;
        
        if (_keyCount == 1 && node.Size == 1)
            return new(null, 0, 0, _keyComparer, _valueComparer);

        if (node.Size == 1)
        {
            return FromNode(
                RemoveNode(node),
                _keyCount - 1,
                _count - 1,
                false
            );
        }
        
        return FromNode(
            node.WithValue(GetRoot(RemoveNode(valueNode).CloneRed())),
            _keyCount,
            _count - 1,
            false
        ); 
    }

    public ImmutableLookup<TKey, TValue> Remove(TValue value)
    {
        if(_count == 1)
            return new(null, 0, 0, _keyComparer, _valueComparer);
        
        var hash = _valueComparer.GetHashCode(value);
        
        foreach (var group in this)
        {
            if(group.ValueNode is null) continue;

            var node = group.ValueNode;

            if (TryGet(ref node, hash))
            {
                if (group.Size == 1)
                    return Remove(group.Key);

                return Remove(group.Key, value);
            }
        }

        return this;
    }

    private TNode? RemoveNode<TNode>(TNode? node)
        where TNode : class, IBinaryNode<TNode>
    {
        if (node is null)
            return null;

        switch (node.Parent, node.Left, node.Right)
        {
            case (null, null, null): return null;
            case (_, not null, not null):
                var newHead = node.Left;

                while (newHead.Right is not null)
                    newHead = newHead.Right;

                var leaf = newHead.WithRight(node.Right);

                return node.Parent is null
                    ? leaf.CloneRed()
                    : node.Parent.Left == node
                        ? node.Parent.WithLeft(leaf).CloneRed()
                        : node.Parent.WithRight(leaf).CloneRed();  
            
            case (null, _, _):
                return node.Left ?? node.Right;
            
            case (not null, _, _):
                return node.Parent.Left == node
                    ? node.Parent.WithLeft(node.Left ?? node.Right).CloneRed()
                    : node.Parent.WithRight(node.Left ?? node.Right).CloneRed();
                
        }
    }

    private ImmutableLookup<TKey, TValue> FromNode(
        Grouping? node, 
        int keyCount,
        int count,
        bool cloneRed = true)
        => new(GetRoot(cloneRed ? node.CloneRed() : node), keyCount, count, _keyComparer, _valueComparer);

    public static ImmutableLookup<TKey, TValue> Create(
        TKey key,
        TValue value,
        IEqualityComparer<TKey>? keyComparer = null,
        IEqualityComparer<TValue>? valueComparer = null)
    {
        keyComparer ??= EqualityComparer<TKey>.Default;
        valueComparer ??= EqualityComparer<TValue>.Default;

        return new ImmutableLookup<TKey, TValue>(
            new(
                key,
                keyComparer.GetHashCode(key),
                new(
                    value,
                    valueComparer.GetHashCode(value)
                )
            ),
            1,
            1,
            keyComparer,
            valueComparer
        );
    }

    public static ImmutableLookup<TKey, TValue> Create(
        TKey key,
        TValue[] values,
        IEqualityComparer<TKey>? keyComparer = null,
        IEqualityComparer<TValue>? valueComparer = null)
    {
        keyComparer ??= EqualityComparer<TKey>.Default;
        valueComparer ??= EqualityComparer<TValue>.Default;

        return new(
            new(
                key,
                keyComparer.GetHashCode(key),
                BuildValueNode(values, valueComparer, out var count)
            ),
            1,
            count,
            keyComparer,
            valueComparer
        );
    }

    private static ValueNode? BuildValueNode(
        TValue[] values,
        IEqualityComparer<TValue> valueComparer,
        out int count)
    {
        count = 0;

        if (values.Length == 0)
            return null;

        var keys = new int[values.Length];

        for (var i = 0; i < values.Length; i++)
            keys[i] = valueComparer.GetHashCode(values[i]);

        Array.Sort(keys, values);

        ValueNode? current = null;
        for (var i = 0; i < keys.Length; i++)
        {
            var value = values[i];
            var hash = keys[i];

            if (current?.ValueHash == hash)
                continue;

            current = new(
                value,
                hash,
                current is not null && current.ValueHash < hash ? current : null,
                current is not null && current.ValueHash > hash ? current : null
            );

            count++;
        }

        return current;
    }

    private Grouping Get(TKey key)
    {
        if (_root is null)
            throw new KeyNotFoundException();

        var node = _root;

        var keyHash = _keyComparer.GetHashCode(key);

        if (!TryGet(ref node, keyHash))
            throw new KeyNotFoundException();

        return node;
    }

    private bool TryGet<TNode>(ref TNode node, int hash)
        where TNode : class, IBinaryNode<TNode>
    {
        if (node.Parent is not null)
            node = GetRoot(node);

        while (true)
        {
            if (hash == node.ValueHash)
                return true;

            if (hash < node.ValueHash)
            {
                if (node.Left is null)
                    return false;

                node = node.Left;
            }
            else
            {
                if (node.Right is null)
                    return false;

                node = node.Right;
            }
        }
    }

    private bool TryAdd<TNode, TValue>(ref TNode node, TValue value, int hashCode)
        where TNode : class, IBinaryNode<TNode, TValue>
    {
        if (TryGet(ref node, hashCode) || node is null)
            return false;

        if (hashCode < node.ValueHash && node.Left is null)
        {
            node = node.WithLeft(value, hashCode);
            return true;
        }
        else if (hashCode > node.ValueHash && node.Right is null)
        {
            node = node.WithRight(value, hashCode);
            return true;
        }

        return false;
    }

    private TNode? GetRoot<TNode>(TNode? node)
        where TNode : class, IBinaryNode<TNode>
    {
        if (node is null)
            return null;

        var current = node;

        while (current.Parent is not null)
            current = current.Parent;

        return current;
    }

    private static BinaryTreeEnumerator<TNode, TValue> CreateNodeEnumerator<TNode, TValue>(TNode node)
        where TNode : class, IBinaryNode<TNode, TValue>
        => new(node);

    public sealed class Grouping :
        IGrouping<TKey, TValue>,
        IBinaryNode<Grouping, TKey>,
        IBinaryNode<Grouping, ValueNode?>
    {
        public TKey Key { get; }

        public int Size => ValueNode?.Size ?? 0;
        internal ValueNode? ValueNode { get; }

        internal int KeyHash { get; }
        private Grouping? Left { get; }
        private Grouping? Right { get; }
        private Grouping? Parent { get; }

        private int _hashCode;

        internal Grouping(
            TKey key,
            int hashCode,
            ValueNode? valueNode = null,
            Grouping? left = null,
            Grouping? right = null,
            Grouping? parent = null)
        {
            Key = key;
            KeyHash = hashCode;
            ValueNode = valueNode;
            Left = left;
            Right = right;
            Parent = parent;
            
            _hashCode = System.HashCode.Combine(hashCode, left?._hashCode ?? 0, right?._hashCode ?? 0, valueNode?.GetHashCode() ?? 0);
        }

        public override int GetHashCode()
            => _hashCode;

        internal Grouping CloneRed()
            => new(Key, KeyHash, ValueNode, Left, Right, Parent.CloneRed());

        internal Grouping CloneGreen()
            => new(Key, KeyHash, ValueNode, Left?.CloneGreen(), Right?.CloneGreen(), Parent);

        Grouping IBinaryNode<Grouping>.CloneGreen() => CloneGreen();
        Grouping IBinaryNode<Grouping>.CloneRed() => CloneRed();

        TKey IBinaryNode<Grouping, TKey>.Value => Key;

        int IBinaryNode<Grouping>.ValueHash => KeyHash;

        Grouping? IBinaryNode<Grouping>.Left => Left;

        Grouping? IBinaryNode<Grouping>.Right => Right;

        Grouping? IBinaryNode<Grouping>.Parent => Parent;


        Grouping IBinaryNode<Grouping>.WithLeft(Grouping? left) => WithLeft(left);

        internal Grouping WithLeft(Grouping? left)
            => new(Key, KeyHash, ValueNode, left, Right, Parent);

        Grouping IBinaryNode<Grouping>.WithRight(Grouping? right) => WithRight(right);

        internal Grouping WithRight(Grouping? right)
            => new(Key, KeyHash, ValueNode, Left, right, Parent);

        Grouping IBinaryNode<Grouping>.WithParent(Grouping? parent) => WithParent(parent);

        internal Grouping WithParent(Grouping? parent)
            => new(Key, KeyHash, ValueNode, Left, Right, parent);

        Grouping IBinaryNode<Grouping, TKey>.WithValue(TKey key, int hash) => WithKey(key, hash);

        internal Grouping WithKey(TKey key, int hash)
            => new(key, hash, ValueNode, Left, Right, Parent);

        internal Grouping WithValue(ValueNode? node)
            => new(Key, KeyHash, node, Left, Right, Parent);

        Grouping IBinaryNode<Grouping, TKey>.WithLeft(TKey key, int hashCode)
            => WithLeft(new(key, hashCode, parent: this));

        Grouping IBinaryNode<Grouping, TKey>.WithRight(TKey key, int hashCode)
            => WithRight(new(key, hashCode, parent: this));

        ValueNode? IBinaryNode<Grouping, ValueNode?>.Value => ValueNode;

        Grouping IBinaryNode<Grouping, ValueNode?>.WithValue(ValueNode? value, int hash)
            => new(Key, KeyHash, value, Left, Right, Parent);

        Grouping IBinaryNode<Grouping, ValueNode?>.WithLeft(ValueNode? value, int hashCode)
            => throw new InvalidOperationException();

        Grouping IBinaryNode<Grouping, ValueNode?>.WithRight(ValueNode? value, int hashCode)
            => throw new InvalidOperationException();

        public struct Enumerator : IEnumerator<TValue>
        {
            public TValue Current => _enumerator.Current;

            object? IEnumerator.Current => Current;

            private readonly BinaryTreeEnumerator<ValueNode, TValue> _enumerator;

            internal Enumerator(ValueNode? root)
            {
                _enumerator = CreateNodeEnumerator<ValueNode, TValue>(root);
            }

            public bool MoveNext()
                => _enumerator.MoveNext();

            public void Reset()
                => _enumerator.Reset();

            public void Dispose()
                => _enumerator.Dispose();
        }

        public Enumerator GetEnumerator() => new(ValueNode);

        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed class ValueNode : IBinaryNode<ValueNode, TValue>
    {
        public TValue Value { get; }

        public int ValueHash { get; }

        public int Size => 1 + (Left?.Size ?? 0) + (Right?.Size ?? 0);

        public ValueNode? Left { get; }
        public ValueNode? Right { get; }
        public ValueNode? Parent { get; }

        private readonly int _hashCode;

        public ValueNode(
            TValue value,
            int hashCode,
            ValueNode? left = null,
            ValueNode? right = null,
            ValueNode? parent = null)
        {
            ValueHash = hashCode;

            Left = left?.WithParent(this);
            Right = right?.WithParent(this);
            Parent = parent;

            Value = value;
            
            _hashCode = System.HashCode.Combine(hashCode, left?._hashCode ?? 0, right?._hashCode ?? 0);
        }

        public override int GetHashCode()
            => _hashCode;

        internal ValueNode CloneRed()
            => new(Value, ValueHash, Left, Right, Parent.CloneRed());

        internal ValueNode CloneGreen()
            => new(Value, ValueHash, Left?.CloneGreen(), Right?.CloneGreen(), Parent);

        ValueNode IBinaryNode<ValueNode>.CloneGreen() => CloneGreen();
        ValueNode IBinaryNode<ValueNode>.CloneRed() => CloneRed();

        public ValueNode WithLeft(ValueNode? left)
            => new(Value, ValueHash, left, Right, Parent);

        public ValueNode WithRight(ValueNode? right)
            => new(Value, ValueHash, Left, right, Parent);

        public ValueNode WithParent(ValueNode? parent)
        {
            if (parent == Parent)
                return this;

            return new(Value, ValueHash, Left, Right, parent);
        }

        public ValueNode WithValue(TValue value, int hash)
            => new(value, hash, Left, Right, Parent);

        public ValueNode WithLeft(TValue value, int hashCode)
            => new(value, ValueHash, new(value, hashCode), Right, Parent);

        public ValueNode WithRight(TValue value, int hashCode)
            => new(value, ValueHash, Left, new(value, hashCode), Parent);
    }

    private struct BinaryTreeEnumerator<TNode, TValue> : IEnumerator<TValue>
        where TNode : class, IBinaryNode<TNode, TValue>
    {
        public TValue Current { get; private set; }
        object? IEnumerator.Current => Current;

        public TNode? CurrentNode => _currentNode;

        private readonly TNode? _rootNode;
        private TNode? _currentNode;

        public BinaryTreeEnumerator(TNode? rootNode)
        {
            _rootNode = rootNode;

            if (rootNode is not null)
            {
                _currentNode = rootNode;
                IterateLeft(ref _currentNode);
            }
        }

        private void IterateLeft(ref TNode node)
        {
            while (node.Left is not null)
                node = node.Left;
        }

        public bool MoveNext()
        {
            if (_currentNode is null) return false;

            Current = _currentNode.Value;

            if (_currentNode.Right is not null)
            {
                _currentNode = _currentNode.Right;
                IterateLeft(ref _currentNode);
            }
            else
            {
                _currentNode = _currentNode.Parent;
            }

            return true;
        }

        public void Reset()
        {
            _currentNode = _rootNode;
            Current = default;
        }

        public void Dispose()
        {
            _currentNode = null;
            Current = default;
        }
    }

    public struct Enumerator : IEnumerator<Grouping>
    {
        public Grouping Current => _enumerator.CurrentNode;

        object? IEnumerator.Current => Current;

        private readonly BinaryTreeEnumerator<Grouping, ValueNode?> _enumerator;

        public Enumerator(Grouping? root)
        {
            _enumerator = new(root);
        }

        public bool MoveNext()
            => _enumerator.MoveNext();

        public void Reset()
            => _enumerator.Reset();

        public void Dispose()
            => _enumerator.Dispose();
    }

    public Enumerator GetEnumerator() => new(_root);

    IEnumerator<IGrouping<TKey, TValue>> IEnumerable<IGrouping<TKey, TValue>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public bool Equals(ImmutableLookup<TKey, TValue> other)
        => GetHashCode() == other?.GetHashCode();

    public override int GetHashCode()
        => System.HashCode.Combine(_root, _keyComparer, _valueComparer);
}

internal interface IBinaryNode<TNode>
{
    int ValueHash { get; }

    TNode? Left { get; }
    TNode? Right { get; }
    TNode? Parent { get; }

    TNode WithLeft(TNode? left);
    TNode WithRight(TNode? right);

    TNode WithParent(TNode? parent);

    TNode CloneGreen();
    TNode CloneRed();
}

internal interface IBinaryNode<TNode, TValue> :
    IBinaryNode<TNode>
{
    TValue Value { get; }
    TNode WithValue(TValue value, int hash);


    TNode WithLeft(TValue value, int hashCode);
    TNode WithRight(TValue value, int hashCode);
}


// using System.Collections;
// using System.Collections.Immutable;
// using System.Diagnostics;
// using System.Runtime.CompilerServices;
// using Discord.Net.Hanz.Nodes;
// using Microsoft.CodeAnalysis;
//
// namespace Discord.Net.Hanz.Utils.Bakery;
//
// public readonly struct ImmutableLookup<TKey, TElement> : ILookup<TKey, TElement>
// {
//     internal sealed class Node<T>
//     {
//         public Node<T> Root => Parent?.Root;
//
//         public readonly T Value;
//         public readonly int HashCode;
//
//         public readonly Node<T>? Left;
//         public readonly Node<T>? Right;
//         public readonly Node<T>? Parent;
//
//         public Node(
//             T value,
//             int hashCode,
//             Node<T>? left = null,
//             Node<T>? right = null,
//             Node<T>? parent = null)
//         {
//             Value = value;
//             HashCode = hashCode;
//             Left = left?.WithParent(this);
//             Right = right?.WithParent(this);
//             Parent = parent;
//         }
//
//         public Node<T> WithChild(Node<T> child, int? hash = null)
//             => (hash ?? child.HashCode) < HashCode ? WithLeft(child) : WithRight(child);
//
//         public Node<T> WithParent(Node<T> parent)
//             => new Node<T>(Value, HashCode, Left, Right, parent);
//
//         public Node<T> WithLeft(Node<T> left)
//             => new Node<T>(Value, HashCode, left, Right, Parent);
//
//         public Node<T> WithRight(Node<T> right)
//             => new Node<T>(Value, HashCode, Left, right, Parent);
//
//         public Node<T> WithValue(T value)
//             => new Node<T>(value, HashCode, Left, Right, Parent);
//
//         public Node<T> CloneTree()
//         {
//             if (Parent is not null)
//                 return Root.CloneTree();
//
//             return CloneGreen();
//         }
//
//         private Node<T> CloneGreen()
//             => new Node<T>(Value, HashCode, Left?.CloneGreen(), Right?.CloneGreen(), Parent);
//
//         public override int GetHashCode()
//         {
//             return base.GetHashCode();
//         }
//     }
//
//     public KeysCollection Keys { get; }
//     public ElementsCollection Elements { get; }
//
//     public int Count { get; }
//
//     public IEnumerable<TElement> this[TKey key]
//         => _table.TryGetValue(_keyComparer.GetHashCode(key), out var value)
//             ? value.Elements
//             : [];
//
//     private readonly NodeCollection<(TKey Key, NodeCollection<TElement> Elements)> _table;
//
//     private readonly IEqualityComparer<TKey> _keyComparer;
//     private readonly IEqualityComparer<TElement> _elementComparer;
//
//     public ImmutableLookup(
//         IEqualityComparer<TKey>? keyComparer = null,
//         IEqualityComparer<TElement>? elementComparer = null)
//     {
//         _keyComparer = keyComparer;
//         _elementComparer = elementComparer;
//         _table = NodeCollection<(TKey Key, NodeCollection<TElement> Elements)>.Empty;
//
//         Keys = new(this);
//         Elements = new(this);
//     }
//
//     private ImmutableLookup(
//         NodeCollection<(TKey, NodeCollection<TElement>)> table,
//         int count,
//         IEqualityComparer<TKey>? keyComparer = null,
//         IEqualityComparer<TElement>? elementComparer = null
//     )
//     {
//         Count = count;
//
//         _keyComparer = keyComparer;
//         _elementComparer = elementComparer;
//         _table = table;
//     }
//
//     public bool Contains(TKey key)
//         => _table.Contains(_keyComparer.GetHashCode(key));
//
//     public ImmutableLookup<TKey, TElement> Add(TKey key, TElement element)
//     {
//         if(_)
//     }
//     
//     public Enumerator GetEnumerator() => new(this);
//     
//     IEnumerator<IGrouping<TKey, TElement>> IEnumerable<IGrouping<TKey, TElement>>.GetEnumerator() => GetEnumerator();
//     IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//
//     public struct Enumerator : IEnumerator<Grouping>
//     {
//         public Grouping Current { get; private set; }
//
//         object? IEnumerator.Current => Current;
//
//         private NodeCollection<(TKey Key, NodeCollection<TElement> Elements)>.Enumerator _enumerator;
//
//         public Enumerator(ImmutableLookup<TKey, TElement> lookup)
//         {
//             _enumerator = lookup._table.GetEnumerator();
//         }
//
//         public bool MoveNext()
//         {
//             if (!_enumerator.MoveNext())
//                 return false;
//
//             Current = new(_enumerator.Current.Key, _enumerator.Current.Elements);
//             return true;
//         }
//
//         public void Reset()
//             => _enumerator.Reset();
//
//
//         public void Dispose()
//             => _enumerator.Dispose();
//     }
//
//     public sealed class Grouping : IGrouping<TKey, TElement>
//     {
//         public TKey Key { get; }
//
//         private NodeCollection<TElement> _elements;
//
//         internal Grouping(TKey key, NodeCollection<TElement> elements)
//         {
//             Key = key;
//             _elements = elements;
//         }
//
//         public struct Enumerator : IEnumerator<TElement>
//         {
//             public TElement Current => _enumerator.Current;
//
//             object? IEnumerator.Current => Current;
//
//             private readonly NodeCollection<TElement>.Enumerator _enumerator;
//
//             public Enumerator(Grouping grouping)
//             {
//                 _enumerator = grouping._elements.GetEnumerator();
//             }
//
//             public bool MoveNext()
//                 => _enumerator.MoveNext();
//
//             public void Reset()
//                 => _enumerator.Reset();
//
//             public void Dispose()
//                 => _enumerator.Dispose();
//         }
//
//         public Enumerator GetEnumerator() => new Enumerator(this);
//         IEnumerator<TElement> IEnumerable<TElement>.GetEnumerator() => GetEnumerator();
//         IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//     }
//
//     internal readonly struct NodeCollection<T> :
//         IImmutableSet<T>
//     {
//         public static NodeCollection<T> Empty = new(null, 0, EqualityComparer<T>.Default);
//
//         public int Count => _count;
//
//         public bool IsEmpty => _count == 0 || _root is null;
//
//         private readonly Node<T>? _root;
//         private readonly int _count;
//
//         private readonly IEqualityComparer<T> _comparer;
//
//         public NodeCollection(Node<T>? root, int count, IEqualityComparer<T> comparer)
//         {
//             _root = root;
//             _count = count;
//             _comparer = comparer;
//         }
//
//         public struct Enumerator : IEnumerator<T>
//         {
//             public T Current { get; private set; }
//             object? IEnumerator.Current => Current;
//
//
//             private Node<T>? _root;
//
//             private Node<T>? _current;
//
//             public Enumerator(Node<T>? root)
//             {
//                 _root = root;
//
//                 _current = _root;
//
//                 IterateLeft(ref _current);
//             }
//
//             [MethodImpl(MethodImplOptions.AggressiveInlining)]
//             private void IterateLeft(ref Node<T> node)
//             {
//                 while (node?.Left is not null)
//                 {
//                     node = node.Left;
//                 }
//             }
//
//
//             public bool MoveNext()
//             {
//                 if (_current is null) return false;
//
//                 Current = _current.Value;
//
//                 if (_current.Right is not null)
//                 {
//                     _current = _current.Right;
//                     IterateLeft(ref _current);
//                 }
//                 else
//                 {
//                     _current = _current.Parent;
//                 }
//
//                 return true;
//             }
//
//             public void Reset()
//             {
//                 _current = _root;
//                 IterateLeft(ref _current);
//             }
//
//
//             public void Dispose()
//             {
//                 _root = null;
//                 _current = null;
//                 Current = default;
//             }
//         }
//
//         private bool TryFindNode(int hash, out Node<T>? node, Node<T>? root = null)
//         {
//             node = root ?? _root;
//
//             if (node is null) return false;
//
//             while (true)
//             {
//                 if (node.HashCode == hash)
//                     return true;
//
//                 if (hash < node.HashCode)
//                 {
//                     if (node.Left is null) return false;
//
//                     node = node.Left;
//                     continue;
//                 }
//
//                 if (node.Right is null) return false;
//                 node = node.Right;
//             }
//         }
//
//         private bool TryAddToNode(T value, int hash, out Node<T>? tree)
//         {
//             tree = null;
//
//             if (TryFindNode(hash, out tree))
//                 return false;
//
//             if (tree is null)
//             {
//                 tree = new(value, hash);
//                 return true;
//             }
//
//             AddValueToNode(value, hash, ref tree);
//             return true;
//         }
//
//         private bool TryAddToNode(T value, out Node<T>? tree)
//             => TryAddToNode(value, _comparer.GetHashCode(value), out tree);
//
//         private void AddValueToNode(T value, int hash, ref Node<T> node)
//         {
//             if (hash == node.HashCode)
//                 return;
//
//             var leaf = new Node<T>(value, hash);
//
//             node = hash < node.HashCode ? node.WithLeft(leaf) : node.WithRight(leaf);
//         }
//
//         public bool TryGetValue(int hash, out T value)
//         {
//             if (TryFindNode(hash, out var node))
//             {
//                 value = node.Value;
//                 return true;
//             }
//
//             value = default;
//             return false;
//         }
//
//         public bool TryAdd(T value, out NodeCollection<T> collection)
//         {
//             if (TryAddToNode(value, out var node))
//             {
//                 collection = new(node.CloneTree(), _count + 1, _comparer);
//                 return true;
//             }
//
//             collection = this;
//             return false;
//         }
//
//         public NodeCollection<T> Add(T value)
//         {
//             if (TryAddToNode(value, out var node))
//                 return new(node.CloneTree(), _count + 1, _comparer);
//
//             return this;
//         }
//
//         public NodeCollection<T> AddRange(params T[] values)
//         {
//             switch (values.Length)
//             {
//                 case 0: return this;
//                 case 1: return Add(values[0]);
//                 default:
//                     if (_root is null)
//                         return Create(values, _comparer);
//
//                     var keys = new int[values.Length];
//
//                     for (var i = 0; i < values.Length; i++)
//                         keys[i] = _comparer.GetHashCode(values[i]);
//
//                     Array.Sort(keys, values);
//
//                     var valuesAdded = 0;
//                     var root = _root;
//
//                     for (var i = 0; i < values.Length; i++)
//                     {
//                         var hash = keys[i];
//                         var value = values[i];
//
//                         if (TryFindNode(hash, out var node, root))
//                             continue;
//
//                         AddValueToNode(value, hash, ref node);
//
//                         root = node.CloneTree();
//                         valuesAdded++;
//                     }
//
//                     return new(root, valuesAdded, _comparer);
//             }
//         }
//
//         public NodeCollection<T> AddRange(IEnumerable<T> values)
//             => AddRange(values.ToArray());
//
//         public IImmutableSet<T> Clear()
//         {
//             throw new NotImplementedException();
//         }
//
//         public bool Contains(int hash) => TryFindNode(hash, out _);
//         public bool Contains(T value) => Contains(_comparer.GetHashCode(value));
//
//         IImmutableSet<T> IImmutableSet<T>.Add(T value)
//         {
//             return Add(value);
//         }
//
//         public NodeCollection<T> Remove(T value)
//         {
//             if (_root is null) return this;
//
//             var hash = _comparer.GetHashCode(value);
//
//             if (!TryFindNode(hash, out var node))
//                 return this;
//
//             var parent = node.Parent;
//             var left = node.Left;
//             var right = node.Right;
//
//             switch (parent, left, right)
//             {
//                 case (null, null, null): return Empty;
//                 case (null, _, _):
//                     return new NodeCollection<T>(
//                         (left ?? right).CloneTree(),
//                         _count - 1,
//                         _comparer
//                     );
//                 case (_, not null, not null):
//                     if (TryFindNode(right.HashCode, out var searchNode, left))
//                         throw new InvalidOperationException();
//
//                     return new NodeCollection<T>(
//                         searchNode.WithChild(right).CloneTree(),
//                         _count - 1,
//                         _comparer
//                     );
//                     break;
//                 case (not null, null, null):
//                     return new NodeCollection<T>(
//                         parent.WithChild(null, node.HashCode).CloneTree(),
//                         _count - 1,
//                         _comparer
//                     );
//                 case (not null, _, _):
//                     return new NodeCollection<T>(
//                         parent.WithChild(left ?? right, node.HashCode).CloneTree(),
//                         _count - 1,
//                         _comparer
//                     );
//             }
//         }
//
//         public NodeCollection<T> RemoveRange(params T[] values)
//         {
//             var collection = this;
//
//             foreach (var value in values)
//             {
//                 collection = collection.Remove(value);
//
//                 if (collection._count == 0) return collection;
//             }
//
//             return collection;
//         }
//
//         public NodeCollection<T> RemoveRange(IEnumerable<T> values)
//         {
//             var collection = this;
//
//             foreach (var value in values)
//             {
//                 collection = collection.Remove(value);
//
//                 if (collection._count == 0) return collection;
//             }
//
//             return collection;
//         }
//
//         public static NodeCollection<T> Create(IEnumerable<T> values, IEqualityComparer<T>? comparer = null)
//             => Create(values.ToArray(), comparer);
//
//         public static NodeCollection<T> Create(T[] values, IEqualityComparer<T>? comparer = null)
//         {
//             comparer ??= EqualityComparer<T>.Default;
//
//             var keys = new int[values.Length];
//
//             for (var i = 0; i < values.Length; i++)
//                 keys[i] = comparer.GetHashCode(values[i]);
//
//             Array.Sort(keys, values);
//
//             Node<T>? currentNode = null;
//             var count = 0;
//
//             for (var i = 0; i < values.Length; i++)
//             {
//                 if (currentNode?.HashCode == keys[i])
//                     continue;
//
//                 currentNode = new Node<T>(
//                     values[i],
//                     keys[i],
//                     currentNode is not null && currentNode.HashCode < keys[i] ? currentNode : null,
//                     currentNode is not null && currentNode.HashCode > keys[i] ? currentNode : null
//                 );
//
//                 count++;
//             }
//
//             return new NodeCollection<T>(currentNode, count, comparer);
//         }
//
//         public Enumerator GetEnumerator()
//             => new(_root);
//
//         IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
//         IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//
//         IImmutableSet<T> IImmutableSet<T>.Remove(T value) => Remove(value);
//
//         public bool TryGetValue(T equalValue, out T actualValue)
//         {
//             if (TryFindNode(_comparer.GetHashCode(equalValue), out var node))
//             {
//                 actualValue = node.Value;
//                 return true;
//             }
//
//             actualValue = default;
//             return false;
//         }
//
//         IImmutableSet<T> IImmutableSet<T>.Intersect(IEnumerable<T> other) => Intersect(other);
//
//         public NodeCollection<T> Intersect(IEnumerable<T> other)
//             => Create(other.Where(Contains));
//
//         IImmutableSet<T> IImmutableSet<T>.Except(IEnumerable<T> other) => Except(other);
//
//         public NodeCollection<T> Except(IEnumerable<T> other)
//             => RemoveRange(other);
//
//         IImmutableSet<T> IImmutableSet<T>.SymmetricExcept(IEnumerable<T> other) => SymmetricExcept(other);
//
//         public NodeCollection<T> SymmetricExcept(IEnumerable<T> other)
//         {
//             var collection = this;
//             foreach (var item in other)
//             {
//                 if (!collection.TryAdd(item, out collection))
//                     collection = collection.Remove(item);
//             }
//
//             return collection;
//         }
//
//         IImmutableSet<T> IImmutableSet<T>.Union(IEnumerable<T> other) => Union(other);
//
//         public NodeCollection<T> Union(NodeCollection<T> other)
//         {
//             var collection = this;
//             foreach (var item in other)
//             {
//                 collection = collection.Add(item);
//             }
//
//             return collection;
//         }
//
//         public NodeCollection<T> Union(IEnumerable<T> other)
//             => AddRange(other);
//
//         public bool SetEquals(NodeCollection<T> other)
//         {
//             if (other.Count != Count)
//                 return false;
//
//             var left = GetEnumerator();
//             var right = other.GetEnumerator();
//
//             while (true)
//             {
//                 var leftMove = left.MoveNext();
//                 var rightMove = right.MoveNext();
//
//                 if (leftMove != rightMove) return false;
//
//                 if (!_comparer.Equals(left.Current, right.Current)) return false;
//             }
//
//             return true;
//         }
//
//         public bool SetEquals(IEnumerable<T> other)
//         {
//             if (other is NodeCollection<T> collection)
//                 return SetEquals(collection);
//
//             var left = GetEnumerator();
//             var right = other.GetEnumerator();
//
//             while (true)
//             {
//                 var leftMove = left.MoveNext();
//                 var rightMove = right.MoveNext();
//
//                 if (leftMove != rightMove) return false;
//
//                 if (!_comparer.Equals(left.Current, right.Current)) return false;
//             }
//
//             return true;
//         }
//
//         public bool IsProperSubsetOf(NodeCollection<T> other)
//         {
//             if (Count >= other.Count)
//                 return false;
//
//             foreach (var item in this)
//             {
//                 if (!other.Contains(item))
//                     return false;
//             }
//
//             return true;
//         }
//
//         public bool IsProperSubsetOf(IEnumerable<T> other)
//         {
//             if (other is NodeCollection<T> collection)
//                 return IsProperSubsetOf(collection);
//
//             return IsProperSubsetOf(Create(other, _comparer));
//         }
//
//         public bool IsProperSupersetOf(NodeCollection<T> other)
//         {
//             if (Count <= other.Count)
//                 return false;
//
//             return other.IsSubsetOf(this);
//         }
//
//         public bool IsProperSupersetOf(IEnumerable<T> other)
//         {
//             var count = 0;
//
//             foreach (var item in other)
//             {
//                 if (!Contains(item))
//                     return false;
//                 count++;
//             }
//
//             return count < Count;
//         }
//
//         public bool IsSubsetOf(NodeCollection<T> other)
//         {
//             if (Count > other.Count) return false;
//
//             foreach (var item in this)
//             {
//                 if (!other.Contains(item))
//                     return false;
//             }
//
//             return true;
//         }
//
//         public bool IsSubsetOf(IEnumerable<T> other)
//             => IsSubsetOf(other is NodeCollection<T> collection ? collection : Create(other, _comparer));
//
//
//         public bool IsSupersetOf(NodeCollection<T> other)
//             => other.IsSubsetOf(this);
//
//         public bool IsSupersetOf(IEnumerable<T> other)
//         {
//             foreach (var item in other)
//             {
//                 if (!Contains(item))
//                     return false;
//             }
//
//             return true;
//         }
//
//         public bool Overlaps(NodeCollection<T> other)
//         {
//             if (Count == 0 || other.Count == 0) return false;
//
//             foreach (var item in this)
//             {
//                 if (other.Contains(item))
//                     return true;
//             }
//
//             return false;
//         }
//
//         public bool Overlaps(IEnumerable<T> other)
//         {
//             foreach (var item in other)
//             {
//                 if (Contains(item))
//                     return true;
//             }
//
//             return false;
//         }
//     }
//
//     public sealed class ElementsCollection : IReadOnlyCollection<(TKey Key, TElement Element)>
//     {
//         public int Count => _table.Count;
//         private readonly NodeCollection<(TKey Key, NodeCollection<TElement> Elements)> _table;
//
//         internal ElementsCollection(ImmutableLookup<TKey, TElement> lookup)
//         {
//             _table = lookup._table;
//         }
//
//         public struct Enumerator : IEnumerator<(TKey Key, TElement Element)>
//         {
//             public (TKey Key, TElement Element) Current { get; private set; }
//
//             private NodeCollection<(TKey Key, NodeCollection<TElement> Elements)>.Enumerator _keyEnumerator;
//             private NodeCollection<TElement>.Enumerator? _elementEnumerator;
//
//             public Enumerator(ElementsCollection collection)
//             {
//                 _keyEnumerator = collection._table.GetEnumerator();
//             }
//
//
//             public bool MoveNext()
//             {
//                 if (_elementEnumerator is null)
//                 {
//                     if (!_keyEnumerator.MoveNext())
//                         return false;
//
//                     _elementEnumerator = _keyEnumerator.Current.Elements.GetEnumerator();
//                 }
//
//                 moveNextElement:
//                 if (!_elementEnumerator.Value.MoveNext())
//                 {
//                     _elementEnumerator.Value.Dispose();
//
//                     if (_keyEnumerator.MoveNext())
//                     {
//                         _elementEnumerator = _keyEnumerator.Current.Elements.GetEnumerator();
//                         goto moveNextElement;
//                     }
//
//                     return false;
//                 }
//
//                 Current = (_keyEnumerator.Current.Key, _elementEnumerator.Value.Current);
//                 return true;
//             }
//
//             public void Reset()
//             {
//                 if (_elementEnumerator.HasValue)
//                 {
//                     _elementEnumerator.Value.Dispose();
//                     _elementEnumerator = null;
//                 }
//
//                 _keyEnumerator.Reset();
//             }
//
//
//             object? IEnumerator.Current => Current;
//
//             public void Dispose()
//             {
//                 if (_elementEnumerator.HasValue)
//                 {
//                     _elementEnumerator.Value.Dispose();
//                     _elementEnumerator = null;
//                 }
//
//                 _keyEnumerator.Dispose();
//             }
//         }
//
//         public Enumerator GetEnumerator() => new Enumerator(this);
//         IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//
//         IEnumerator<(TKey Key, TElement Element)> IEnumerable<(TKey Key, TElement Element)>.GetEnumerator()
//             => GetEnumerator();
//     }
//
//     public sealed class KeysCollection : IReadOnlyCollection<TKey>
//     {
//         public int Count => _table.Count;
//
//         private readonly NodeCollection<(TKey Key, NodeCollection<TElement> Elements)> _table;
//
//         internal KeysCollection(ImmutableLookup<TKey, TElement> lookup)
//         {
//             _table = lookup._table;
//         }
//
//         public struct Enumerator : IEnumerator<TKey>
//         {
//             public TKey Current { get; private set; }
//
//             object? IEnumerator.Current => Current;
//
//             private NodeCollection<(TKey Key, NodeCollection<TElement> Elements)>.Enumerator _enumerator;
//
//             public Enumerator(KeysCollection collection)
//             {
//                 _enumerator = collection._table.GetEnumerator();
//             }
//
//             public bool MoveNext()
//             {
//                 var result = _enumerator.MoveNext();
//                 Current = _enumerator.Current.Key;
//                 return result;
//             }
//
//             public void Reset()
//             {
//                 _enumerator.Reset();
//                 Current = default;
//             }
//
//             public void Dispose()
//             {
//                 Current = default;
//                 _enumerator.Dispose();
//             }
//         }
//
//         public Enumerator GetEnumerator()
//             => new(this);
//
//         IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
//
//         IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => GetEnumerator();
//     }
// }