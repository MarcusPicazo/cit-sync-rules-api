using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Domain.Resources;
using FluentValidation;

namespace CitSync.Rules.Application.Resources;

public interface IResourceService
{
    Task<PagedResult<ResourceResponse>> ListAsync(PageQuery query, CancellationToken cancellationToken);

    Task<ResourceResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ResourceResponse> CreateAsync(CreateResourceRequest request, CancellationToken cancellationToken);
}

public sealed class ResourceService(
    IResourceRepository resources,
    IUnitOfWork unitOfWork,
    IValidator<PageQuery> pageValidator,
    IValidator<CreateResourceRequest> createValidator) : IResourceService
{
    public async Task<PagedResult<ResourceResponse>> ListAsync(PageQuery query, CancellationToken cancellationToken)
    {
        await pageValidator.ValidateAndThrowAsync(query, cancellationToken);
        var page = await resources.ListAsync(query, cancellationToken);
        return page.Map(ResourceResponse.From);
    }

    public async Task<ResourceResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var resource = await resources.GetByIdAsync(id, cancellationToken)
            ?? throw NotFoundException.For(nameof(Resource), id);
        return ResourceResponse.From(resource);
    }

    public async Task<ResourceResponse> CreateAsync(CreateResourceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var resource = new Resource(request.Name, request.Category, request.Capacity, request.Building);

        // The unique index on Name is the real guarantee; this check just gives a friendly 409.
        if (await resources.NameExistsAsync(resource.Name, cancellationToken))
        {
            throw new ConflictException($"A resource named '{resource.Name}' already exists.");
        }

        resources.Add(resource);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ResourceResponse.From(resource);
    }
}
