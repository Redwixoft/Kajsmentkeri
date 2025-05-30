using Kajsmentkeri.Application.Interfaces;
using Kajsmentkeri.Application.Services;
using Kajsmentkeri.Domain;
using Kajsmentkeri.Infrastructure.Persistence;
using Kajsmentkeri.Web.Areas.Identity;
using Kajsmentkeri.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging();

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<AppUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;

    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = true;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddHttpContextAccessor();

builder.Services.AddRazorPages();
builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();

builder.Services.AddScoped<IPredictionScoringService, PredictionScoringService>();
builder.Services.AddScoped<IPredictionService, PredictionService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<ILeaderboardService, LeaderboardService>();
builder.Services.AddScoped<IChampionshipService, ChampionshipService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

var app = builder.Build();

try
{
    app.UseMigrationsEndPoint();
    app.UseDeveloperExceptionPage();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        /*app.UseMigrationsEndPoint();
        app.UseDeveloperExceptionPage();*/
    }
    else
    {
        //app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
    }

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        var identityDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        identityDb.Database.Migrate();
    }

    app.UseHttpsRedirection();

    app.UseStaticFiles();

    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapRazorPages();

    app.Run();
}
catch (Exception ex)
{
    var logger = builder.Build().Services.GetRequiredService<ILogger<Program>>();
    logger.LogError(ex, "Startup migration failed: {Message}", ex.Message);
    throw; // Rethrow to fail fast
}