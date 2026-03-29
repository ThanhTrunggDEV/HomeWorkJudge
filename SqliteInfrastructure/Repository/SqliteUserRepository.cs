using System.Threading.Tasks;
using Domain.Entity;
using Domain.Ports;
using Domain.ValueObject;
using Microsoft.EntityFrameworkCore;

namespace SqliteDataAccess.Repository;

public sealed class SqliteUserRepository : IUserRepository
{
    private readonly AppDbContext _context;

    public SqliteUserRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByIdAsync(UserId id)
    {
        var record = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id.Value);

        return record is null ? null : SqliteEntityMapper.ToDomain(record);
    }

    public Task AddAsync(User user)
    {
        _context.Users.Add(SqliteEntityMapper.ToRecord(user));
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(User user)
    {
        var record = SqliteEntityMapper.ToRecord(user);
        var exists = await _context.Users.AnyAsync(x => x.Id == record.Id);

        if (exists)
        {
            _context.Users.Update(record);
            return;
        }

        await _context.Users.AddAsync(record);
    }
}
