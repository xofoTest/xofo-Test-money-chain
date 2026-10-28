using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using MoneyChain.Api;

var builder = WebApplication.CreateBuilder(args);

// --- Data (SQLite for the runnable local prototype; canonical PostgreSQL schema + RLS ship in /database) ---
var dbPath = builder.Configuration["ConnectionStrings:Default"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "moneychain.db");
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite($"Data Source={dbPath}"));

// --- Auth (JWT bearer; passwords hashed with ASP.NET Core Identity's PBKDF2 hasher) ---
var jwtKey = builder.Configuration["Jwt:Key"] ?? "MoneyChainSAS-local-prototype-signing-key-0123456789";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "moneychain";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "moneychain";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("AdminOnly", p => p.RequireRole("admin"));
    o.AddPolicy("MemberOrAdmin", p => p.RequireAuthenticatedUser());
});

builder.Services.AddCors(o => o.AddPolicy("dev", p =>
    p.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173", "http://localhost:5200")
     .AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddSingleton<SecurityService>();
builder.Services.AddScoped<PlacementService>();
builder.Services.AddScoped<MemberService>();
builder.Services.AddScoped<RewardService>();
builder.Services.AddScoped<AdminService>();
builder.Services.AddScoped<Seeder>();

var app = builder.Build();

// Uniform JSON error responses for domain errors; never leak internal exceptions.
app.Use(async (ctx, next) =>
{
    try
    {
        await next();
    }
    catch (DomainException ex)
    {
        ctx.Response.StatusCode = ex.StatusCode;
        await ctx.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
    catch (Exception ex)
    {
        ctx.Response.StatusCode = StatusCodes.Status500InternalServerError;
        string? detail = app.Environment.IsDevelopment() ? ex.ToString() : null;
        await ctx.Response.WriteAsJsonAsync(new { error = "An unexpected error occurred.", detail });
    }
});

// Seed fictional demo data (idempotent).
using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<Seeder>().SeedAsync();
}

app.UseCors("dev");
app.UseAuthentication();
app.UseAuthorization();

app.MapApiEndpoints();

// Serve the built React SPA (frontend/dist) with an SPA fallback, if present.
var dist = Path.GetFullPath(Path.Combine(app.Environment.ContentRootPath, "..", "..", "frontend", "dist"));
if (Directory.Exists(dist))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(dist),
        ServeUnknownFileTypes = false
    });
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new { error = "Not found" });
            return;
        }
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(Path.Combine(dist, "index.html"));
    });
}

app.Run();

// Exposed for the integration-test factory.
public partial class Program { }
