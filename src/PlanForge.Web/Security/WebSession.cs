namespace PlanForge.Web.Security;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PlanForge.Contracts.Accounts;
using PlanForge.Web.Api;
using System.Security.Claims;

public static class WebSession
{
	public const string TokenClaim = "planforge:session";

	public static void MapAccountEndpoints(WebApplication app)
	{
		app.MapPost("/account/{operation}", HandleAsync);
	}

	private static async Task<IResult> HandleAsync(HttpContext context, string operation, IAntiforgery antiforgery, ApiTransport api)
	{
		if (operation is not ("login" or "register" or "logout" or "password"))
			return Results.NotFound();
		try { await antiforgery.ValidateRequestAsync(context); }
		catch (AntiforgeryValidationException) { return Results.BadRequest(UiText.Get("The form expired. Reload and try again.")); }
		if (operation == "logout")
		{
			string? token = context.User.FindFirst(TokenClaim)?.Value;
			if (token is not null)
			{
				try { await api.SendAsync<object>(HttpMethod.Post, "auth/logout", token, cancellationToken: context.RequestAborted); }
				catch (ApiException exception) when (exception.StatusCode == 401) { /* Already revoked. */ }
			}
			await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
			return Results.LocalRedirect("/login");
		}
		IFormCollection form = await context.Request.ReadFormAsync(context.RequestAborted);
		string errorPath = operation == "password" ? "settings" : operation;
		try
		{
			if (operation == "password")
			{
				string? token = context.User.FindFirst(TokenClaim)?.Value;
				if (token is null) return Results.Unauthorized();
				if (form["newPassword"].ToString() != form["confirmPassword"].ToString()) return Results.LocalRedirect("/settings?error=" + Uri.EscapeDataString(UiText.Get("The new passwords do not match.")));
				await api.SendAsync<object>(HttpMethod.Post, "users/me/password", token, new ChangePasswordRequest(form["currentPassword"].ToString(), form["newPassword"].ToString()), cancellationToken: context.RequestAborted);
				await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
				return Results.LocalRedirect("/login?notice=password-changed");
			}
			object request = operation == "register" ? new RegisterRequest(form["email"].ToString(), form["password"].ToString(), form["displayName"].ToString()) : new LoginRequest(form["email"].ToString(), form["password"].ToString());
			AccessGrant grant = await api.SendAsync<AccessGrant>(HttpMethod.Post, "auth/" + operation, body: request, cancellationToken: context.RequestAborted);
			var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, grant.User.Id.ToString()), new Claim(ClaimTypes.Name, grant.User.DisplayName), new Claim(TokenClaim, grant.Token), new Claim(AccountCultureProvider.LanguageClaim, grant.User.Language)], CookieAuthenticationDefaults.AuthenticationScheme);
			await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = true, ExpiresUtc = grant.ExpiresAt, AllowRefresh = false });
			return Results.LocalRedirect("/");
		}
		catch (ApiException exception)
		{
			return Results.LocalRedirect("/" + errorPath + "?error=" + Uri.EscapeDataString(exception.Message));
		}
		catch (HttpRequestException)
		{
			return Results.LocalRedirect("/" + errorPath + "?error=" + Uri.EscapeDataString(UiText.Get("The service is temporarily unavailable. Try again.")));
		}
	}
}
