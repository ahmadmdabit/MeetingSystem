using MeetingSystem.Model;

using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace MeetingSystem.Context;

/// <summary>
/// Defines the contract for the Unit of Work pattern, which manages repositories and database transactions.
/// </summary>
/// <remarks>
/// This pattern ensures that all operations within a single business transaction are handled as a single atomic unit.
/// It provides a single point of entry for saving all changes to the database.
/// </remarks>
public interface IUnitOfWork : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the repository for User entities.
    /// </summary>
    IGenericRepository<User> Users { get; }

    /// <summary>
    /// Gets the repository for Role entities.
    /// </summary>
    IGenericRepository<Role> Roles { get; }

    /// <summary>
    /// Gets the repository for UserRole entities.
    /// </summary>
    IGenericRepository<UserRole> UserRoles { get; }

    /// <summary>
    /// Gets the repository for Meeting entities.
    /// </summary>
    IGenericRepository<Meeting> Meetings { get; }

    /// <summary>
    /// Gets the repository for MeetingFiles entities.
    /// </summary>
    IGenericRepository<MeetingFile> MeetingFiles { get; }

    /// <summary>
    /// Gets the repository for MeetingParticipants entities.
    /// </summary>
    IGenericRepository<MeetingParticipant> MeetingParticipants { get; }

    /// <summary>
    /// Gets the repository for MeetingsLog entities.
    /// </summary>
    IGenericRepository<MeetingsLog> MeetingsLog { get; }

    /// <summary>
    /// Gets the repository for RevokedTokens entities.
    /// </summary>
    IGenericRepository<RevokedToken> RevokedTokens { get; }

    /// <summary>
    /// Asynchronously saves all changes tracked by the DbContext to the database.
    /// This method does not commit a transaction; use CommitTransactionAsync for that.
    /// It is useful for scenarios where an ID needs to be generated before the full transaction is complete.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The number of state entries written to the database.</returns>
    Task<int> CompleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begins a new database transaction if one is not already active.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the active database transaction, saving all pending changes.
    /// If an error occurs, the transaction is automatically rolled back.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if no transaction is active.</exception>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the active database transaction, discarding all pending changes.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements the Unit of Work pattern to manage transactions and provide access to repositories.
/// This implementation uses explicit database transactions to ensure atomicity across multiple operations.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly MeetingSystemDbContext _context;
    private readonly ILogger<UnitOfWork> _logger;
    private IDbContextTransaction? _transaction;
    private bool _disposed;

    public IGenericRepository<User> Users { get; }
    public IGenericRepository<Role> Roles { get; }
    public IGenericRepository<UserRole> UserRoles { get; }
    public IGenericRepository<Meeting> Meetings { get; }
    public IGenericRepository<MeetingFile> MeetingFiles { get; }
    public IGenericRepository<MeetingParticipant> MeetingParticipants { get; }
    public IGenericRepository<MeetingsLog> MeetingsLog { get; }
    public IGenericRepository<RevokedToken> RevokedTokens { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWork"/> class.
    /// </summary>
    /// <param name="context">The database context to be used for this unit of work.</param>
    /// <param name="logger">The logger for recording transaction events.</param>
    public UnitOfWork(MeetingSystemDbContext context, ILogger<UnitOfWork> logger)
    {
        _context = context;
        _logger = logger;

        Users = new GenericRepository<User>(_context);
        Roles = new GenericRepository<Role>(_context);
        UserRoles = new GenericRepository<UserRole>(_context);
        Meetings = new GenericRepository<Meeting>(_context);
        MeetingFiles = new GenericRepository<MeetingFile>(_context);
        MeetingParticipants = new GenericRepository<MeetingParticipant>(_context);
        MeetingsLog = new GenericRepository<MeetingsLog>(_context);
        RevokedTokens = new GenericRepository<RevokedToken>(_context);
    }

    /// <inheritdoc />
    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            _logger.LogInformation("A transaction is already in progress.");
            return;
        }
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("New database transaction started with ID: {TransactionId}", _transaction.TransactionId);
    }

    /// <inheritdoc />
    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            throw new InvalidOperationException("Cannot commit a transaction that has not been started. Call BeginTransactionAsync first.");
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Database transaction {TransactionId} committed successfully.", _transaction.TransactionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred during transaction commit for {TransactionId}. Rolling back.", _transaction.TransactionId);
            await RollbackTransactionAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }
        finally
        {
            await DisposeTransactionAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
        {
            _logger.LogWarning("Rollback was requested, but no active transaction was found.");
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogWarning("Database transaction {TransactionId} was rolled back.", _transaction.TransactionId);
        }
        finally
        {
            await DisposeTransactionAsync().ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task<int> CompleteAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Safely disposes the current transaction object.
    /// </summary>
    private async ValueTask DisposeTransactionAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }

    /// <summary>
    /// Disposes the DbContext and any active transaction.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Asynchronously disposes the DbContext and any active transaction.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected implementation of the Dispose pattern.
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _transaction?.Dispose();
                _context.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Protected implementation of the asynchronous Dispose pattern.
    /// </summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        if (!_disposed)
        {
            await DisposeTransactionAsync().ConfigureAwait(false);
            await _context.DisposeAsync().ConfigureAwait(false);
            _disposed = true;
        }
    }
}