using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using CriptoPortfolio.Infrastructure.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace CriptoPortfolio.Api.Extensions;

public static class ApiServiceCollectionExtensions
{
    public const string CorsPolicyName = "Frontend";
    public const string AuthRateLimitPolicy = "auth";
    public const string PublicRateLimitPolicy = "public";
    public const string PrivateRateLimitPolicy = "private";

    private static readonly HashSet<string> EnglishTitles = new(StringComparer.Ordinal)
    {
        "Bad Request", "Unauthorized", "Forbidden", "Not Found", "Method Not Allowed", "Unsupported Media Type",
        "Too Many Requests", "Internal Server Error", "An error occurred while processing your request."
    };

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string[] corsOrigins)
    {
        // Los enums viajan como texto ("Buy", "Sell"...); también se aceptan como número.
        services.AddControllers()
            .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))
            .ConfigureApiBehaviorOptions(o => o.InvalidModelStateResponseFactory = _ =>
            {
                // Los mensajes automáticos de ASP.NET vienen en inglés: se responde con uno fijo en español.
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Los datos enviados no son válidos.",
                    Detail = "Revisa los campos e inténtalo de nuevo."
                };
                problem.Extensions["errors"] = new[] { "Revisa los campos e inténtalo de nuevo." };
                return new BadRequestObjectResult(problem) { ContentTypes = { "application/problem+json" } };
            });
        services.AddProblemDetails(o => o.CustomizeProblemDetails = context =>
        {
            // Solo se traducen los títulos genéricos del framework; los que ponen los controladores ya van en español.
            var problem = context.ProblemDetails;
            if (problem.Title is null || EnglishTitles.Contains(problem.Title))
            {
                problem.Title = problem.Status switch
                {
                    StatusCodes.Status400BadRequest => "La solicitud no es válida.",
                    StatusCodes.Status401Unauthorized => "Tu sesión no es válida o ha caducado.",
                    StatusCodes.Status403Forbidden => "No tienes permiso para hacer esto.",
                    StatusCodes.Status404NotFound => "No se encontró lo que pedías.",
                    StatusCodes.Status405MethodNotAllowed => "Esa operación no está permitida.",
                    StatusCodes.Status415UnsupportedMediaType => "El formato de los datos no es válido.",
                    StatusCodes.Status429TooManyRequests => "Demasiadas peticiones. Espera un momento.",
                    >= 500 => "Ha ocurrido un error en el servidor. Inténtalo de nuevo.",
                    _ => problem.Title
                };
            }
        });
        services.AddEndpointsApiExplorer();

        services.AddJwtAuthentication(configuration);
        services.AddSwaggerWithJwt();
        services.AddRateLimiting();

        if (corsOrigins.Length > 0)
        {
            services.AddCors(options => options.AddPolicy(CorsPolicyName, policy => policy
                .WithOrigins(corsOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()));
        }

        return services;
    }

    private static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // Falla al arrancar, con un mensaje claro, si la clave falta o es corta.
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
        jwt.EnsureValid();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Los claims se quedan con su nombre original del token: "sub", "email"...
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = "sub"
                };
            });

        services.AddAuthorization();
        return services;
    }

    private static IServiceCollection AddSwaggerWithJwt(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "CriptoPortfolio API", Version = "v1" });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Pega solo el token JWT (sin la palabra Bearer)."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }

    /// <summary>
    /// Limita por IP: registro y login (10/min) y lectura pública del catálogo (120/min); y por usuario la zona privada (120/min). OJO: detrás de Next.js la IP que ve la API es la del servidor
    /// de Next, no la del usuario; la IP real (X-Forwarded-For) se resuelve en la Etapa 8.
    /// </summary>
    private static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Demasiadas peticiones. Espera un momento.",
                    Detail = "Has hecho demasiadas peticiones seguidas. Espera un momento y vuelve a intentarlo."
                };
                problem.Extensions["errors"] = new[] { problem.Detail };
                await response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken: ct);
            };
            options.AddPolicy(AuthRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy(PublicRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Zona privada: 120/min por usuario (la autenticación va antes que el limitador); sin usuario, por IP.
            options.AddPolicy(PrivateRateLimitPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.User.GetUserId() is { } userId
                        ? "user:" + userId
                        : "ip:" + (httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }
}
