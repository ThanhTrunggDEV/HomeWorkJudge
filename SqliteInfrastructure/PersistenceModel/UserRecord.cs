using System;

namespace SqliteDataAccess.PersistenceModel;

public sealed class UserRecord
{
    public Guid Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int Role { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
}
