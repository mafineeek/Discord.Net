using System;
namespace Discord
{
    // dev note: split up the iface for cases when we have ids present and don't need to load them.

    /// <summary>
    ///     Represents a collection wrapper of <see cref="IEntitySource{TEntity, TId}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The ID type of the entity.</typeparam>
    public interface IEntityEnumerableSource<TEntity, TId> : ILazyEntityEnumerableSource<TEntity, TId>
        where TEntity : IEntity<TId>
        where TId : IEquatable<TId>
    {
        /// <summary>
        ///     Gets a collection of IDs represented for this entity collection source.
        /// </summary>
        IReadOnlyCollection<TId> Ids { get; }
    }

    /// <summary>
    ///     Represents a collection wrapper of <see cref="IEntitySource{TEntity, TId}"/>.
    /// </summary>
    /// <typeparam name="TEntity">The entity type.</typeparam>
    /// <typeparam name="TId">The ID type of the entity.</typeparam>
    public interface ILazyEntityEnumerableSource<TEntity, TId> : IAsyncEnumerable<IEntitySource<TEntity, TId>>
        where TEntity : IEntity<TId>
        where TId : IEquatable<TId>
    {
        /// <summary>
        ///     Gets a collection of IDs for the entities that this collection represents.
        /// </summary>
        /// <returns>
        ///     A <see cref="ValueTask"/> representing the asynchronous operation of
        ///     getting the IDs. The result of the <see cref="ValueTask"/> is a
        ///     readonly collection of entity IDs.
        /// </returns>
        ValueTask<IReadOnlyCollection<TId>> GetIdsAsync(CancellationToken token = default);

        /// <summary>
        ///     Flattens this <see cref="IAsyncEnumerable{T}"/> of <see cref="IEntitySource{TEntity, TId}"/>
        ///     to a collection of <see cref="TEntity"/>s.
        /// </summary>
        /// <returns>
        ///     A <see cref="ValueTask"/> that represents the asynchronous operation
        ///     of flattening this <see cref="IAsyncEnumerable{T}"/>. The result of the
        ///     <see cref="ValueTask"/> is a readonly collection of <see cref="TEntity"/>s.
        /// </returns>
        ValueTask<IReadOnlyCollection<TEntity>> FlattenAsync(RequestOptions? option = null, CancellationToken token = default);
    }
}

