using System;
using System.Collections.Generic;
using Domain.ValueObject;
using Domain.Exception;

namespace Domain.Entity;

public class Classroom : EntityBase
{
    public ClassroomId Id { get; private set; }
    public string JoinCode { get; private set; }
    public string Name { get; private set; }
    public UserId TeacherId { get; private set; }
    
    private readonly List<UserId> _studentIds = new();
    public IReadOnlyList<UserId> StudentIds => _studentIds.AsReadOnly();

    public Classroom(ClassroomId id, string name, UserId teacherId)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Classroom name cannot be empty.");
        
        Id = id;
        Name = name;
        TeacherId = teacherId;
        JoinCode = GenerateNewJoinCode();
    }

    private string GenerateNewJoinCode() => Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
    
    public void ResetJoinCode()
    {
        JoinCode = GenerateNewJoinCode();
    }

    public void AddStudent(UserId studentId) 
    { 
        if (_studentIds.Contains(studentId)) throw new DomainException("Student is already in the classroom.");
        _studentIds.Add(studentId); 
    }
    
    public void RemoveStudent(UserId studentId) 
    {
        if (!_studentIds.Contains(studentId)) throw new DomainException("Student not found in the classroom.");
        _studentIds.Remove(studentId);
    }
}
