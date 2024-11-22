using System.Collections;
using System.Text;
using Discord.Net.Hanz.Tasks.Actors.Nodes;
using Discord.Net.Hanz.Utils.Bakery;

namespace Discord.Net.Hanz;

public interface IPathedState
{
    TypePath Path { get; }
}

public readonly record struct TypePath(
    ImmutableEquatableArray<TypePath.Part> Parts
) : IReadOnlyCollection<TypePath.Part>
{
    public readonly record struct Part(
        Type Type,
        string Name
    )
    {
        public override string ToString() => Name;

        public static implicit operator Part((Type, string) tuple) => new(tuple.Item1, tuple.Item2);

        public static IEnumerable<TypePath> operator +(Part a, IEnumerable<TypePath> paths)
            => paths.Select(x => a + x);
        
        public static IEnumerable<TypePath> operator +(IEnumerable<TypePath> paths, Part part)
            => paths.Select(x => x + part);
    }

    public static readonly TypePath Empty = new([]);

    public bool IsEmpty => Parts.Count == 0;
    public int Count => Parts.Count;

    public Part? Last => Parts.Count == 0 ? null : Parts[Parts.Count - 1];
    public Part? First => Parts.Count == 0 ? null : Parts[0];

    #region Products

    /// <summary>
    ///     Computes the ordered cartesian product of all the parts in the path.
    ///     <br/><br/>
    ///     The cartesian product is defined to be all unique combinations of each part in the path that retains the
    ///     order they appeared in the current path. <br/>
    ///     Ex, {a, b, c} would have the following products:
    ///     <code>
    ///     {a}
    ///     {b}
    ///     {c}
    ///     {a, b}
    ///     {a, c}
    ///     {b, c}
    ///     {a, b, c}
    ///     </code>
    /// </summary>
    /// <param name="removeLast">Whether to remove the last entry, usually the current path.</param>
    /// <returns>Each cartesian composition of the current path.</returns>
    public IEnumerable<TypePath> CartesianProduct(bool removeLast = true)
    {
        if (IsEmpty) return [];

        var parts = Parts;

        return Enumerable
            .Range(1, (1 << Parts.Count) - (removeLast ? 2 : 1))
            .Select(index =>
                new TypePath(parts.Where((_, i) => (index & (1 << i)) != 0).ToImmutableEquatableArray())
            );
    }

    /// <summary>
    ///     Computes the semantical product of the current path.
    ///     <br/><br/>
    ///     The semantical product is defined as all unique combinations of each part that excludes subsets of the
    ///     cartesian part. <br/>
    ///     Ex, {a, b, c} would have the following products:
    ///     <code>
    ///     {c},
    ///     {b, c}
    ///     {a, b},
    ///     </code>
    /// </summary>
    /// <returns>Each semantical product of the current path.</returns>
    public IEnumerable<TypePath> SemanticalProduct()
    {
        var products = CartesianProduct().ToArray();

        if (products.Length == 0) yield break;

        foreach (var product in products)
        {
            if (products.Any(x => product != x && (product | x) == product))
                continue;

            yield return product;
        }
    }

    #endregion

    #region Equality and comparisons

    public bool Equals(params Type[] semantic)
    {
        if (Count != semantic.Length)
            return false;

        for (var i = 0; i < Parts.Count; i++)
        {
            if (Parts[i].Type != semantic[i])
                return false;
        }

        return true;
    }

    public bool IsParentTo(TypePath path)
    {
        if (IsEmpty || !path.Last.HasValue) return false;

        if (Count + 1 != path.Count) return false;

        return SliceEquals(0, path.Take(path.Count - 1).Select(x => x.Type).ToArray());
    }

    public bool StartsWith(params Type[] semantic)
        => SliceEquals(0, semantic);

    public bool EndsWith(params Type[] semantic)
        => SliceEquals(Count - semantic.Length, semantic);

    public bool SliceEquals(int start, params Type[] semantic)
    {
        if (start < 0 || start + semantic.Length > Count || semantic.Length == 0)
            return false;

        for (var i = 0; i < semantic.Length; i++)
        {
            if (Parts[start + i].Type != semantic[i])
                return false;
        }

        return true;
    }

    public bool Contains<TPart>()
        => Parts.Any(x => x.Type == typeof(TPart));

    public bool Contains(Part part)
        => Parts.Any(x => x == part);

    #endregion

    #region Mutation

    public TypePath Add<TPart>(string name)
        => new(Parts.Add(new(typeof(TPart), name)));

    public TypePath Add(Part part)
        => new(Parts.Add(part));

    public TypePath AddRange(IEnumerable<Part> parts)
        => new(Parts.AddRange(parts));

    public TypePath Slice(int start = 0, int count = int.MaxValue)
    {
        if (start >= Count || count <= 0)
            return Empty;

        return new(new(Parts.Skip(start).Take(Math.Min(count, Parts.Count - start))));
    }

    #endregion

    #region Filtering

    public int CountOfType<TPart>() => Parts.Count(x => x.Type == typeof(TPart));
    public int CountOfType(Type type) => Parts.Count(x => x.Type == type);

    public TypePath OfType<T>()
        => new(new(Parts.Where(x => x.Type == typeof(T))));

    public TypePath Filter(
        Type[]? include = null,
        Type[]? exclude = null,
        int from = 0,
        int to = int.MaxValue
    )
    {
        if (Parts.Count == 0) return Empty;

        if (to <= 0 || from >= Count)
            return Empty;

        if (include is null && exclude is null && from == 0 && to >= Count)
            return this;

        var result = new List<Part>(Count);

        var upper = Math.Min(Count, to);

        for (var i = from; i < upper; i++)
        {
            var part = Parts[i];

            if (exclude is not null && exclude.Contains(part.Type))
                continue;

            if (include is not null && !include.Contains(part.Type))
                continue;

            result.Add(part);
        }

        return new(result.ToImmutableEquatableArray());
    }

    #endregion


    #region Formatting

    public override string ToString()
        => Format();

    public string FormatRelative()
        => Format(exclude: [typeof(ActorNode)]);

    public string FormatParent()
        => Format(to: Count - 1);

    public string Format(
        Type[]? include = null,
        Type[]? exclude = null,
        int from = 0,
        int to = int.MaxValue,
        bool prefixDot = false
    )
    {
        var filtered = include is null && exclude is null && from == 0 && to >= Count
            ? this
            : Filter(include, exclude, from, to);

        if (filtered.IsEmpty) return string.Empty;

        var builder = new StringBuilder();

        if (prefixDot) builder.Append('.');

        return builder.Append(string.Join(".", filtered.Parts)).ToString();
    }

    #endregion

    #region Operators

    #region Addition

    public static TypePath operator +(TypePath a, TypePath b)
        => new(a.Parts.AddRange(b.Parts));

    public static TypePath operator +(TypePath a, Part b)
        => new(a.Parts.Add(b));

    public static TypePath operator +(TypePath a, (Type, string) b)
        => new(a.Parts.Add(b));

    public static IEnumerable<TypePath> operator +(TypePath a, IEnumerable<TypePath> b)
        => b.Select(x => a + x);

    public static TypePath operator +(Part a, TypePath b)
        => new(new([a, ..b.Parts]));

    public static TypePath operator +((Type, string) a, TypePath b)
        => new(new([a, ..b.Parts]));

    #endregion

    #region Subtraction

    public static TypePath operator --(TypePath path)
        => path.Slice(count: path.Count - 1);

    public static TypePath operator -(TypePath path)
        => path.Slice(count: path.Count - 1);

    #endregion

    #region Bitwise

    public static TypePath operator &(TypePath a, TypePath b)
    {
        if (a.IsEmpty || b.IsEmpty)
            return Empty;

        var bounds = Math.Min(a.Count, b.Count);

        var result = new List<Part>(bounds);

        for (int i = 0; i < bounds; i++)
        {
            var left = a.Parts[i];
            var right = b.Parts[i];

            if (left != right)
                break;

            result.Add(left);
        }

        if (result.Count == 0) return Empty;

        return new(Parts: result.ToImmutableEquatableArray());
    }

    public static TypePath operator |(TypePath a, TypePath b)
    {
        if (a.IsEmpty || b.IsEmpty) return Empty;

        var result = new List<Part>(a.Count);

        foreach (var part in a.Parts)
        {
            if (b.Contains(part))
                result.Add(part);
        }

        if (result.Count == 0) return Empty;

        return new(result.ToImmutableEquatableArray());
    }

    #endregion

    #region Conversion

    public static implicit operator string(TypePath path) => path.ToString();
    public static implicit operator TypePath(Part part) => Empty.Add(part);

    #endregion

    #endregion


    public IEnumerator<Part> GetEnumerator()
    {
        return Parts.GetUnderlyingEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return ((IEnumerable) Parts).GetEnumerator();
    }
}