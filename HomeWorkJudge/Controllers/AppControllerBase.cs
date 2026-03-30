using System;
using System.Security.Claims;
using Domain.Exception;
using Microsoft.AspNetCore.Mvc;
using Ports.DTO.Common;

namespace HomeWorkJudge.Controllers;

public abstract class AppControllerBase : Controller
{
    protected Guid? CurrentUserId
    {
        get
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(raw, out var id) ? id : null;
        }
    }

    protected string CurrentUserRole => User.FindFirstValue(ClaimTypes.Role) ?? "Student";

    protected UserRoleDto CurrentUserRoleDto
        => Enum.TryParse<UserRoleDto>(CurrentUserRole, out var role) ? role : UserRoleDto.Student;

    protected void SetSuccess(string message) => TempData["SuccessMessage"] = message;

    protected void SetError(string message) => TempData["ErrorMessage"] = message;

    protected bool TryHandleDomainException(Exception ex)
    {
        if (ex is not DomainException domainException)
        {
            return false;
        }

        ModelState.AddModelError(string.Empty, domainException.Message);
        return true;
    }

    protected IActionResult? ToAccessActionResult(ResourceAccessDecisionDto decision)
    {
        return decision switch
        {
            ResourceAccessDecisionDto.Allowed => null,
            ResourceAccessDecisionDto.NotFound => NotFound(),
            _ => Forbid()
        };
    }
}
