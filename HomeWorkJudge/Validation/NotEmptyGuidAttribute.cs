using System;
using System.ComponentModel.DataAnnotations;

namespace HomeWorkJudge.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute
{
    public NotEmptyGuidAttribute()
    {
        ErrorMessage = "The field {0} is required.";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is Guid guid && guid != Guid.Empty)
        {
            return ValidationResult.Success;
        }

        var message = FormatErrorMessage(validationContext.DisplayName);
        return new ValidationResult(message);
    }
}
