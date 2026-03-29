using System.Threading;
using System.Threading.Tasks;
using Domain.Ports;

namespace SqliteDataAccess;

public sealed class SqliteUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;

    public SqliteUnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
