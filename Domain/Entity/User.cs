using System;
using Domain.ValueObject;

namespace Domain.Entity;

public class User : EntityBase
{
    public UserId Id { get; private set; }
    public string Email { get; private set; }
    public string FullName { get; private set; }
    public UserRole Role { get; private set; }
    public string PasswordHash { get; private set; }

    public User(UserId id, string email, string fullName, UserRole role, string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("Email cannot be empty or whitespace.");
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("FullName cannot be empty or whitespace.");
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("PasswordHash cannot be empty or whitespace.");
        
        Id = id;
        Email = email;
        FullName = fullName;
        Role = role;
        PasswordHash = passwordHash;
    }

    public void UpdateProfile(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("FullName cannot be empty or whitespace.");
        FullName = fullName;
    }
    
    public void ChangeRole(UserRole role)
    {
        Role = role;
    }

    public void UpdatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash)) throw new ArgumentException("PasswordHash cannot be empty or whitespace.");
        PasswordHash = passwordHash;
    }
}
