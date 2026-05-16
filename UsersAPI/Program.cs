using System.Text;
using Contracts.IntegrationEvents;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using UsersAPI.API.Endpoints;
using UsersAPI.Infrastructure.Auth;
using UsersAPI.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Logging detalhado para diagnosticar publish/outbox
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("MassTransit", LogLevel.Debug);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Information);

# region Configuration
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

var rabbitHost = builder.Configuration["RabbitMq:Host"];
var rabbitUser = builder.Configuration["RabbitMq:Username"];
var rabbitPass = builder.Configuration["RabbitMq:Password"];
var rabbitVhost = builder.Configuration["RabbitMq:VirtualHost"] ?? "/";
# endregion

# region Database
builder.Services.AddDbContext<UsersDbContext>(opt =>
{
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
});
# endregion

# region Identity
builder.Services
    .AddIdentityCore<UsersAPI.Domain.Entity.AppUser>(opt =>
    {
        opt.Password.RequiredLength = 8;
        opt.User.RequireUniqueEmail = true;
        opt.Lockout.MaxFailedAccessAttempts = 5;
    })
    .AddRoles<Microsoft.AspNetCore.Identity.IdentityRole<Guid>>()
    .AddEntityFrameworkStores<UsersDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();
# endregion

# region JWT
var jwtSection = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false; // dev
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSection.Issuer,
            ValidAudience = jwtSection.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection.Key)),
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();

# endregion

# region MassTransit + RabbitMQ + EF Outbox
builder.Services.AddMassTransit(x =>
{
    // Outbox (garante publicação confiável vinculada à transação do Postgres)
    x.AddEntityFrameworkOutbox<UsersDbContext>(o =>
    {
        o.QueryDelay = TimeSpan.FromSeconds(10);
        o.UsePostgres();
        o.UseBusOutbox();
    });

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, rabbitVhost, h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });
        
        cfg.Message<UserCreatedEventV1>(x =>
        {
            x.SetEntityName("fcg.users");
        });
        
        cfg.Publish<UserCreatedEventV1>(x =>
        {
            x.ExchangeType = "topic";
        });

        cfg.ConfigureEndpoints(context);
    });
});
# endregion

builder.Services.AddHealthChecks()
    .AddDbContextCheck<UsersDbContext>("usersdb");

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapAuthEndpoints();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<UsersDbContext>();
    await db.Database.MigrateAsync();
    await UsersAPI.Infrastructure.Identity.RoleSeeder.SeedAsync(services);
}

app.Run();