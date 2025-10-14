using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace MeetingSystem.Context;

/// <summary>
/// Defines the contract for a generic repository for querying entities of type T.
/// </summary>
/// <typeparam name="T">The type of the entity.</typeparam>
public interface IGenericRepository<T> where T : class
{
    /// <summary>
    /// Gets an entity by its primary key.
    /// </summary>
    /// <param name="id">The primary key of the entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entity, or null if not found.</returns>
    ValueTask<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the number of entities in the repository.
    /// </summary>
    /// <param name="predicate">An optional predicate to filter the entities.</param>
    /// <returns>The number of entities matching the predicate, or the total number of entities if no predicate is provided.</returns>
    Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);

    /// <summary>
    /// Gets all entities of this type.
    /// </summary>
    /// <returns>An IQueryable of all entities.</returns>
    IQueryable<T> GetAll();

    /// <summary>
    /// Finds entities based on a predicate.
    /// </summary>
    /// <param name="expression">The filter expression.</param>
    /// <returns>An IQueryable of matching entities.</returns>
    IQueryable<T> Find(Expression<Func<T, bool>> expression);

    /// <summary>
    /// Adds a new entity to the context.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    void Add(T entity);

    /// <summary>
    /// Adds multiple entities to the context.
    /// </summary>
    /// <param name="entities"></param>
    void AddRange(IEnumerable<T> entities);

    /// <summary>
    /// Removes an entity from the context.
    /// </summary>
    /// <param name="entity">The entity to remove.</param>
    void Remove(T entity);

    /// <summary>
    /// Removes multiple entities from the context.
    /// </summary>
    /// <param name="entities">The entities to remove.</param>
    void RemoveRange(IEnumerable<T> entities);
}

/// <summary>
/// Provides a generic implementation of the <see cref="IGenericRepository{T}"/> interface using Entity Framework Core.
/// </summary>
/// <typeparam name="T">The type of the entity.</typeparam>
public class GenericRepository<T> : IGenericRepository<T> where T : class
{
    protected readonly MeetingSystemDbContext _context;
    protected readonly DbSet<T> _dbSet;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenericRepository{T}"/> class.
    /// </summary>
    /// <param name="context">The database context to be used.</param>
    public GenericRepository(MeetingSystemDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    /// <inheritdoc />
    public ValueTask<T?> GetByIdAsync(object id, CancellationToken cancellationToken = default) => 
        _dbSet.FindAsync([id], cancellationToken);

    /// <inheritdoc />
    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null) => predicate == null ? _dbSet.CountAsync() : _dbSet.CountAsync(predicate);

    /// <inheritdoc />
    public IQueryable<T> GetAll() => _dbSet.AsQueryable();

    /// <inheritdoc />
    public IQueryable<T> Find(Expression<Func<T, bool>> expression) => _dbSet.Where(expression);

    /// <inheritdoc />
    public void Add(T entity) => _dbSet.Add(entity);

    /// <inheritdoc />
    public void AddRange(IEnumerable<T> entities) => _dbSet.AddRange(entities);

    /// <inheritdoc />
    public void Remove(T entity) => _dbSet.Remove(entity);

    /// <inheritdoc />
    public void RemoveRange(IEnumerable<T> entities) => _dbSet.RemoveRange(entities);
}