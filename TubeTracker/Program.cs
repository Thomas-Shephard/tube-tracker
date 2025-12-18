using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MySqlConnector;
using Scalar.AspNetCore;
using TubeTracker.API.Extensions;
using TubeTracker.API.Repositories;
using TubeTracker.API.Services;
using TubeTracker.API.Services.Background;
using TubeTracker.API.Settings;

namespace TubeTracker.API;

[ExcludeFromCodeCoverage]
public static class Program
{
    public static void Main(string[] args)
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

        // Load environment variables into configuration
        builder.Configuration.AddEnvironmentVariables();

        // Configure Forwarded Headers for Reverse Proxy (Nginx/Docker)
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        // Add standard web services
        builder.Services.AddControllers();
        builder.Services.AddOpenApi();

        // Configure Settings
        DatabaseSettings dbSettings = builder.Services.AddAndConfigureFromEnv<DatabaseSettings>(builder.Configuration, "DB");
        JwtSettings jwtSettings = builder.Services.AddAndConfigureFromEnv<JwtSettings>(builder.Configuration, "JWT");
        TflSettings tflSettings = builder.Services.AddAndConfigureFromEnv<TflSettings>(builder.Configuration, "TFL");
        TokenDenySettings tokenDenySettings = builder.Services.AddAndConfigure<TokenDenySettings>(builder.Configuration, "TokenDenySettings");

        // Register Database Connection
        builder.Services.AddScoped<IDbConnection>(_ =>
        {
            MySqlConnection connection = new(dbSettings.ConnectionString);
            connection.Open();
            return connection;
        });

        // Register Repositories
        builder.Services.AddScoped<IPasswordResetRepository, PasswordResetRepository>();
        builder.Services.AddScoped<IUserVerificationRepository, UserVerificationRepository>();
        builder.Services.AddScoped<ITokenDenyRepository, TokenDenyRepository>();
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ITrackedLineRepository, TrackedLineRepository>();
        builder.Services.AddScoped<ITrackedStationRepository, TrackedStationRepository>();
        builder.Services.AddScoped<ILineRepository, LineRepository>();
        builder.Services.AddScoped<IStationRepository, StationRepository>();
        builder.Services.AddScoped<ILineStatusHistoryRepository, LineStatusHistoryRepository>();
        builder.Services.AddScoped<IStationStatusHistoryRepository, StationStatusHistoryRepository>();

        // Register Services
        builder.Services.AddHttpClient<ITflService, TflService>();
        builder.Services.AddSingleton<ITokenService, TokenService>(); // Assuming TokenService takes JwtSettings via ctor or is configured elsewhere if not added here

        // Conditional Email Service Registration
        bool useMockEmailService = bool.TryParse(builder.Configuration["USE_MOCK_EMAIL_SERVICE"], out bool useMock) && useMock;
        if (useMockEmailService)
        {
            builder.Services.AddSingleton<IEmailService, MockEmailService>();
        }
        else
        {
            builder.Services.AddAndConfigureFromEnv<EmailSettings>(builder.Configuration, "SMTP");
            builder.Services.AddSingleton<ISmtpClientFactory, SmtpClientFactory>();
            builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
        }

        // Register Background Services
        builder.Services.AddSingleton<IEmailQueue, EmailQueue>();
        builder.Services.AddHostedService<EmailBackgroundService>();
        builder.Services.AddHostedService<TubeStatusBackgroundService>();
        builder.Services.AddHostedService<TubeMetadataBackgroundService>();

        // Register background services/handlers that use TimeProvider
        builder.Services.AddSingleton(TimeProvider.System); // Register TimeProvider for consistency
        builder.Services.AddSingleton<ITokenDenyService>(sp => new TokenDenyService(tokenDenySettings, TimeProvider.System, sp.GetRequiredService<IServiceScopeFactory>()));

        // Configure JWT Authentication
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidAudience = jwtSettings.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret))
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    ITokenDenyService denylist = context.HttpContext.RequestServices.GetRequiredService<ITokenDenyService>();
                    string? jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                    if (jti != null && await denylist.IsDeniedAsync(jti))
                    {
                        context.Fail("Token is denied.");
                    }
                }
            };
        });

        builder.Services.AddAuthorization();


        WebApplication app = builder.Build();

        app.UseForwardedHeaders();

        DatabaseMigrator.ApplyMigrations(dbSettings.ConnectionString);

        app.MapOpenApi(); // Uses Swagger UI if in development
        app.MapScalarApiReference("/scalar");

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.MapFallbackToFile("index.html");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}
