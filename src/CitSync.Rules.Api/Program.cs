using System.Text.Json.Serialization;
using Asp.Versioning;
using CitSync.Rules.Api.ErrorHandling;
using CitSync.Rules.Api.Http;
using CitSync.Rules.Api.OpenApi;
using CitSync.Rules.Application;
using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Infrastructure;
using CitSync.Rules.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Who is calling (X-Actor header for now): used by the approval workflow and the audit trail.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, HttpCurrentActor>();

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

// Full check: runs every registered check.
app.MapHealthChecks("/health");

// Liveness: runs NO checks. It only proves the process is up and answering HTTP.
// It must not depend on anything external, or a database outage would make
// Kubernetes restart every pod in a loop.
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

// Readiness: runs only the checks tagged "ready" (SQL Server).
// If it fails, Kubernetes takes the pod out of the load balancer without restarting it.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.Run();