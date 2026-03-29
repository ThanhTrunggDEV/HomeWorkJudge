using System;

namespace SqliteDataAccess.PersistenceModel;

public sealed class ClassroomStudentRecord
{
    public Guid ClassroomId { get; set; }
    public Guid StudentId { get; set; }
}
