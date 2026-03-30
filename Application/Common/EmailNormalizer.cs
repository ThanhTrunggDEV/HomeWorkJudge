using System;

namespace Application.Common;

internal static class EmailNormalizer
{
    public static string Normalize(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        return email.Trim().ToLowerInvariant();
    }
}
