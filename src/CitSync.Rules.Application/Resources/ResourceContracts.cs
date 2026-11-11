using CitSync.Rules.Domain.Resources;
using FluentValidation;

namespace CitSync.Rules.Application.Resources;

public sealed record CreateResourceRequest(string Name, ResourceCategory Category, int Capacity, string? Building);

public sealed record ResourceResponse(
    Guid Id,
    string Name,
    ResourceCategory Category,
    int Capacity,
    string? Building,
    bool IsActive)
{
    public static ResourceResponse From(Resource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return new(
            resource.Id,
            resource.Name,
            resource.Category,
            resource.Capacity,
            resource.Building,
            resource.IsActive);
    }
}

public sealed class CreateResourceRequestValidator : AbstractValidator<CreateResourceRequest>
{
    public const int MaxCapacity = 1000;

    public CreateResourceRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(Resource.NameMaxLength);
        RuleFor(r => r.Category).IsInEnum();
        RuleFor(r => r.Capacity).InclusiveBetween(1, MaxCapacity);
        RuleFor(r => r.Building).MaximumLength(Resource.BuildingMaxLength);
    }
}
