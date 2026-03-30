using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Domain.Exception;
using HomeWorkJudge.Models.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Ports.DTO.Common;
using Ports.DTO.User;
using Ports.InBoundPorts.User;

namespace HomeWorkJudge.Controllers;

public sealed class AuthController : AppControllerBase
{
    private readonly IRegisterUserUseCase _registerUserUseCase;
    private readonly ILoginUseCase _loginUseCase;

    public AuthController(
        IRegisterUserUseCase registerUserUseCase,
        ILoginUseCase loginUseCase)
    {
        _registerUserUseCase = registerUserUseCase;
        _loginUseCase = loginUseCase;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() => View(new RegisterViewModel());

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var response = await _registerUserUseCase.HandleAsync(
                new RegisterUserRequestDto(model.Email, model.FullName, UserRoleDto.Student, model.Password));

            SetSuccess($"Registered successfully for {response.Email}. Please login.");
            return RedirectToAction(nameof(Login));
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var loginResponse = await _loginUseCase.HandleAsync(new LoginRequestDto(model.Email, model.Password));

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, loginResponse.UserId.ToString()),
                new(ClaimTypes.Name, model.Email.Trim()),
                new(ClaimTypes.Role, loginResponse.Role.ToString()),
                new("expires_at", loginResponse.ExpiresAt.ToString("O"))
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = new DateTimeOffset(loginResponse.ExpiresAt)
                });

            SetSuccess("Login successful.");

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [Authorize]
    [HttpGet]
    public IActionResult Profile()
    {
        var model = new ProfileViewModel
        {
            UserId = CurrentUserId ?? Guid.Empty,
            Email = User.Identity?.Name ?? string.Empty,
            Role = Enum.TryParse<UserRoleDto>(CurrentUserRole, out var role) ? role : UserRoleDto.Student,
            SessionExpiresAt = DateTimeOffset.TryParse(User.FindFirst("expires_at")?.Value, out var expiresAt) ? expiresAt : null
        };

        return View(model);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        SetSuccess("Logged out.");
        return RedirectToAction(nameof(Login));
    }
}
