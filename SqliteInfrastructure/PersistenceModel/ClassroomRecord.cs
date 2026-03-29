using System;

namespace SqliteDataAccess.PersistenceModel;

public sealed class ClassroomRecord
{
    public Guid Id { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid TeacherId { get; set; }
}
