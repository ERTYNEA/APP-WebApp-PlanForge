using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using PlanForge.Contracts;
using PlanForge.Web.Api;
using PlanForge.Web.Components;
using PlanForge.Web.Security;

DateTimeOffset processStartedAtUtc;
using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
	processStartedAtUtc = new DateTimeOffset(process.StartTime.ToUniversalTime());
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always);
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
	options.SetDefaultCulture("en").AddSupportedCultures(AccountCultureProvider.SupportedLanguages.ToArray()).AddSupportedUICultures(AccountCultureProvider.SupportedLanguages.ToArray());
	options.RequestCultureProviders = [new AccountCultureProvider()];
});
builder.Services.AddScoped<ApiTransport>();
builder.Services.AddScoped<PlanForgeClient>();
builder.Services.AddScoped<PlanForge.Web.State.BreadcrumbState>();
builder.Services.AddScoped<PlanForge.Web.State.PlanExplorerState>();
var apiPolicy = new ApiEndpointPolicy(builder.Configuration["PlanForgeApi:BaseUrl"], builder.Environment.IsDevelopment());
builder.Services.AddSingleton(apiPolicy);
builder.Services.AddTransient<ApiDestinationGuard>();
builder.Services.AddHttpClient("PlanForgeApi", client => { client.BaseAddress = apiPolicy.BaseUri; client.Timeout = TimeSpan.FromSeconds(120); })
	.ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
	.AddHttpMessageHandler<ApiDestinationGuard>()
	.RedactLoggedHeaders(_ => true);
string keyPath = builder.Configuration["PlanForge:DataProtectionPath"] ?? (Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID") is not null
	? Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? "/home", "ASP.NET", "DataProtection-Keys")
	: Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "../../.secrets/web-keys")));
Directory.CreateDirectory(keyPath);
if (!OperatingSystem.IsWindows())
	File.SetUnixFileMode(keyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
builder.Services.AddDataProtection().SetApplicationName("PlanForge.Web").PersistKeysToFileSystem(new DirectoryInfo(keyPath));
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
	options.Cookie.Name = "PlanForge.Session";
	options.Cookie.HttpOnly = true;
	options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
	options.Cookie.SameSite = SameSiteMode.Lax;
	options.LoginPath = "/login";
	options.SlidingExpiration = false;
	options.Events.OnValidatePrincipal = async context =>
	{
		string? token = context.Principal?.FindFirst(WebSession.TokenClaim)?.Value;
		if (token is null) { context.RejectPrincipal(); return; }
		try { await context.HttpContext.RequestServices.GetRequiredService<ApiTransport>().SendAsync<object>(HttpMethod.Get, "auth/session", token, cancellationToken: context.HttpContext.RequestAborted); }
		catch (ApiException exception) when (exception.StatusCode is 401 or 403)
		{
			context.RejectPrincipal();
			await context.HttpContext.SignOutAsync();
		}
	};
});
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/error");
	app.UseHsts();
}
app.Use(async (context, next) =>
{
	context.Response.Headers["X-Content-Type-Options"] = "nosniff";
	context.Response.Headers["Referrer-Policy"] = "same-origin";
	context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' wss:; frame-ancestors 'none'; object-src 'none'; base-uri 'self'; form-action 'self'";
	context.Response.Headers["Cache-Control"] = "no-store";
	await next();
});
app.UseAuthentication();
app.UseRequestLocalization();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
WebSession.MapAccountEndpoints(app);
FileProxy.MapFileEndpoints(app);
app.MapGet("/health", () => Results.Json(new { status = "ready", version = ProductVersion.Informational, processStartedAtUtc }));
app.MapGet("/error", () => Results.Problem(PlanForge.Web.UiText.Get("The page could not be loaded. Please try again."), statusCode: 500));
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

public partial class Program;
