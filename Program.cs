using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentHubPro;
using RentHubPro.Components;
using RentHubPro.Data;
using RentHubPro.Data.Entities;
using RentHubPro.Hubs;
using RentHubPro.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpContextAccessor();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Не задана строка подключения 'DefaultConnection'.");

var serverVersion = new MySqlServerVersion(new Version(8, 0, 0));

builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, serverVersion));

builder.Services.AddScoped<ApplicationDbContext>(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityConstants.ApplicationScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
})
.AddIdentityCookies();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.User.RequireUniqueEmail = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddUserValidator<BelarusPhoneUserValidator>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppUserClaimsPrincipalFactory>();

builder.Services.AddSignalR();

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", p => p.RequireRole(Roles.Admin));
    options.AddPolicy("LandlordOnly", p => p.RequireRole(Roles.Landlord));
    options.AddPolicy("TenantOnly", p => p.RequireRole(Roles.Tenant));
});

builder.Services.AddScoped<ContractService>();
builder.Services.AddScoped<InvoiceService>();
builder.Services.AddScoped<PremiseService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddScoped<ChatService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    var logger = sp.GetRequiredService<ILogger<Program>>();
    try
    {
        var db = sp.GetRequiredService<ApplicationDbContext>();
        if (!db.Database.GetMigrations().Any())
        {
            logger.LogError(
                "Не найдено ни одной миграции EF Core. Создайте начальную миграцию командой " +
                "'dotnet ef migrations add InitialCreate' и запустите приложение снова.");
        }
        else
        {
            await db.Database.MigrateAsync();
            await DbSeeder.SeedAsync(sp, app.Configuration);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ошибка инициализации базы данных. Проверьте, что MySQL запущен и строка подключения корректна.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapHub<ChatHub>("/chathub");

app.MapPost("/theme/set", async (HttpContext http, string value, UserManager<ApplicationUser> userManager) =>
{
    var theme = value?.ToLowerInvariant() == "dark" ? AppTheme.Dark : AppTheme.Light;

    http.Response.Cookies.Append("theme", theme.ToString().ToLowerInvariant(), new CookieOptions
    {
        Expires = DateTimeOffset.UtcNow.AddYears(1),
        HttpOnly = false,
        SameSite = SameSiteMode.Lax
    });

    if (http.User.Identity?.IsAuthenticated == true)
    {
        var user = await userManager.GetUserAsync(http.User);
        if (user is not null && user.Theme != theme)
        {
            user.Theme = theme;
            await userManager.UpdateAsync(user);
        }
    }
    return Results.NoContent();
});

var reports = app.MapGroup("/reports").RequireAuthorization("LandlordOnly");

reports.MapGet("/financial", async (HttpContext http, ReportService reportSvc,
    UserManager<ApplicationUser> userManager, DateOnly? from, DateOnly? to) =>
{
    var user = await userManager.GetUserAsync(http.User);
    if (user is null) return Results.Unauthorized();

    var fromDate = (from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(-1))).ToDateTime(TimeOnly.MinValue);
    var toDate = (to ?? DateOnly.FromDateTime(DateTime.UtcNow)).ToDateTime(TimeOnly.MinValue);

    var bytes = await reportSvc.BuildFinancialReportAsync(user.Id, user.FullName, fromDate, toDate);
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        $"Финансовый_отчёт_{DateTime.Now:yyyyMMdd}.docx");
});

reports.MapGet("/occupancy", async (HttpContext http, ReportService reportSvc,
    UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(http.User);
    if (user is null) return Results.Unauthorized();

    var bytes = await reportSvc.BuildOccupancyReportAsync(user.Id, user.FullName);
    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        $"Отчёт_заполняемость_{DateTime.Now:yyyyMMdd}.docx");
});

app.MapGet("/invoices/{id:int}/download", async (int id, HttpContext http,
    ReportService reportSvc, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(http.User);
    if (user is null) return Results.Unauthorized();

    var bytes = await reportSvc.BuildInvoiceAsync(id, user.Id);
    if (bytes is null) return Results.NotFound();

    return Results.File(bytes,
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        $"Счёт_{id}.docx");
}).RequireAuthorization();

app.Run();
