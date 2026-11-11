using System.Text.Json.Serialization;
using Asp.Versioning;
using CitSync.Rules.Api.ErrorHandling;
using CitSync.Rules.Api.OpenApi;
using CitSync.Rules.Application;
using CitSync.Rules.Infrastructure;
using CitSync.Rules.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddControllers(options =>
    {
        // With <Nullable>enable</Nullable>, MVC would add an implicit [Required] to every
        // non-nullable string and answer 400 before FluentValidation runs. We want ONE source of
        // validation rules (the validators), so we turn that off.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// RFC 7807: every error (unhandled exception, 404, 400...) is returned as application/problem+json.
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Instance ??=
            $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}");
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = false;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddSwaggerGen();
builder.Services.ConfigureOptions<ConfigureSwaggerOptions>();

builder.Services.AddHealthChecks()
    .AddDbContextCheck<RulesDbContext>("sqlserver", tags: ["ready"]);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        foreach (var description in app.DescribeApiVersions())
        {
            options.SwaggerEndpoint(
                $"/swagger/{description.GroupName}/swagger.json",
                description.GroupName.ToUpperInvariant());
        }
    });
}

app.MapControllers();

app.MapHealthChecks("/health");
// TODO(Marcus): add two more endpoints next to /health:
//   /health/live  -> runs NO checks (only proves the process answers HTTP)
//   /health/ready -> runs only the checks tagged "ready" (SQL Server)
// Hint: MapHealthChecks accepts a HealthCheckOptions with a Predicate.
// Be ready to explain in the interview why Kubernetes wants liveness and readiness separated.

app.Run();
