using HardwareStore.Api.Hubs;
using HardwareStore.Core.Interfaces;
using HardwareStore.Core.Services;
using HardwareStore.Infrastructure;
using HardwareStore.Infrastructure.CosmosDb;
using HardwareStore.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<CosmosDbSettings>(builder.Configuration.GetSection("CosmosDb"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));
builder.Services.Configure<NaturalLanguageSettings>(builder.Configuration.GetSection("NaturalLanguage"));

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

// SignalR – override the no-op notifier with the real SignalR implementation
builder.Services.AddSignalR();
builder.Services.AddScoped<ISearchStatusNotifier, SignalRSearchStatusNotifier>();

// Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-super-secret-key-change-in-production-min-32-chars";
var authBuilder = builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "HardwareStore",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "HardwareStoreUsers",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
        // Allow JWT to be passed via the query string for WebSocket (SignalR) connections
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme);

var facebookAppId = builder.Configuration["Facebook:AppId"];
var facebookAppSecret = builder.Configuration["Facebook:AppSecret"];
if (!string.IsNullOrEmpty(facebookAppId) && !string.IsNullOrEmpty(facebookAppSecret))
{
    authBuilder.AddFacebook(FacebookDefaults.AuthenticationScheme, options =>
    {
        options.AppId = facebookAppId;
        options.AppSecret = facebookAppSecret;
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.Fields.Add("email");
        options.Fields.Add("name");
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
    options.AddPolicy("ActiveUser", policy => policy.RequireClaim("status", "Active"));
});

builder.Services.AddControllers();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:3000", "http://localhost:5173" };
        policy.WithOrigins(origins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HardwareStore API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using Bearer scheme",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("api", config =>
    {
        config.Window = TimeSpan.FromMinutes(1);
        config.PermitLimit = 60;
        config.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        config.QueueLimit = 5;
    });
    options.RejectionStatusCode = 429;
});

var app = builder.Build();

// Initialize CosmosDB
try
{
    using var scope = app.Services.CreateScope();
    var cosmos = scope.ServiceProvider.GetRequiredService<CosmosDbContext>();
    await cosmos.InitializeAsync();

    // Seed default retailers
    await SeedRetailersAsync(scope.ServiceProvider);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Failed to initialize CosmosDB - running with in-memory/limited mode");
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRateLimiter();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SearchStatusHub>("/hubs/search-status");

// SPA fallback for React frontend (static files served from wwwroot in production)
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

static async Task SeedRetailersAsync(IServiceProvider services)
{
    var retailerRepo = services.GetRequiredService<HardwareStore.Core.Interfaces.IRetailerRepository>();
    var existing = await retailerRepo.GetAllAsync();
    
    if (!existing.Any(r => r.Id == "homedepot"))
    {
        await retailerRepo.CreateAsync(new HardwareStore.Core.Models.Retailer
        {
            Id = "homedepot",
            Name = "Home Depot",
            BaseUrl = "https://www.homedepot.com",
            LogoUrl = "/logos/homedepot.png",
            IsEnabled = true,
            IsAvailableToAll = true,
            ScraperType = "HomeDepot"
        });
    }
    
    if (!existing.Any(r => r.Id == "lowes"))
    {
        await retailerRepo.CreateAsync(new HardwareStore.Core.Models.Retailer
        {
            Id = "lowes",
            Name = "Lowe's",
            BaseUrl = "https://www.lowes.com",
            LogoUrl = "/logos/lowes.png",
            IsEnabled = true,
            IsAvailableToAll = true,
            ScraperType = "Lowes"
        });
    }
}

public partial class Program { }
