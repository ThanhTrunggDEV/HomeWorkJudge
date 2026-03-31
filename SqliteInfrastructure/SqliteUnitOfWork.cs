using System.Threading;
using System.Threading.Tasks;
using Domain.Ports;

namespace SqliteDataAccess;

/// <summary>
/// Unit of Work cho SQLite.
/// Bọc SaveChanges trong explicit transaction để đảm bảo atomicity.
///
/// Domain events KHÔNG được dispatch ở đây — Application Use Cases tự
/// collect events từ entities và gọi IDomainEventDispatcher sau khi SaveChanges.
/// </summary>
public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public SqliteUnitOfWork(AppDbContext context) => _context = context;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
