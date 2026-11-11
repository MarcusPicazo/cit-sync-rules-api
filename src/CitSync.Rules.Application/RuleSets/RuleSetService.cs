using CitSync.Rules.Application.Abstractions;
using CitSync.Rules.Application.Common;
using CitSync.Rules.Application.Rules.Engine;
using CitSync.Rules.Domain.RuleSets;
using FluentValidation;
using FluentValidation.Results;

namespace CitSync.Rules.Application.RuleSets;

public interface IRuleSetService
{
    Task<PagedResult<RuleSetSummaryResponse>> ListAsync(ListRuleSetsQuery query, CancellationToken cancellationToken);

    Task<RuleSetResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<RuleSetResponse> CreateAsync(CreateRuleSetRequest request, CancellationToken cancellationToken);

    Task<RuleSetVersionResponse> GetVersionAsync(Guid id, int number, CancellationToken cancellationToken);

    Task<RuleSetVersionResponse> AddVersionAsync(
        Guid id, CreateRuleSetVersionRequest request, CancellationToken cancellationToken);

    Task<RuleSetVersionResponse> SubmitAsync(
        Guid id, int number, byte[]? expectedVersion, CancellationToken cancellationToken);

    Task<RuleSetVersionResponse> ApproveAsync(
        Guid id, int number, byte[]? expectedVersion, CancellationToken cancellationToken);

    Task<RuleSetVersionResponse> RejectAsync(
        Guid id, int number, RejectRuleSetVersionRequest request, byte[]? expectedVersion, CancellationToken cancellationToken);
}

public sealed class RuleSetService(
    IRuleSetRepository ruleSets,
    IResourceRepository resources,
    IUnitOfWork unitOfWork,
    ICurrentActor actor,
    TimeProvider timeProvider,
    IValidator<ListRuleSetsQuery> listValidator,
    IValidator<CreateRuleSetRequest> createValidator,
    IValidator<CreateRuleSetVersionRequest> versionValidator,
    IValidator<RejectRuleSetVersionRequest> rejectValidator,
    IRuleParametersValidator parametersValidator) : IRuleSetService
{
    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    public async Task<PagedResult<RuleSetSummaryResponse>> ListAsync(
        ListRuleSetsQuery query, CancellationToken cancellationToken)
    {
        await listValidator.ValidateAndThrowAsync(query, cancellationToken);
        var page = await ruleSets.ListAsync(query, cancellationToken);
        return page.Map(RuleSetSummaryResponse.From);
    }

    public async Task<RuleSetResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var ruleSet = await ruleSets.GetByIdAsync(id, cancellationToken)
            ?? throw NotFoundException.For(nameof(RuleSet), id);
        return RuleSetResponse.From(ruleSet);
    }

    public async Task<RuleSetResponse> CreateAsync(CreateRuleSetRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var scopeKey = request.ScopeKey;
        if (request.Scope == RuleScope.Resource)
        {
            var resourceId = Guid.Parse(request.ScopeKey!);
            if (await resources.GetByIdAsync(resourceId, cancellationToken) is null)
            {
                throw new ValidationException(
                    [new ValidationFailure(nameof(request.ScopeKey), $"Resource '{resourceId}' does not exist.")]);
            }

            scopeKey = resourceId.ToString(); // canonical lowercase "D" format, so the unique index works
        }

        var ruleSet = new RuleSet(request.Name, request.Description, request.Scope, scopeKey, actor.Name, UtcNow);

        if (await ruleSets.NameExistsAsync(ruleSet.Name, cancellationToken))
        {
            throw new ConflictException($"A rule set named '{ruleSet.Name}' already exists.");
        }

        if (await ruleSets.ScopeExistsAsync(ruleSet.Scope, ruleSet.ScopeKey, cancellationToken))
        {
            throw new ConflictException(
                $"There is already a rule set for {ruleSet.Scope} '{ruleSet.ScopeKey}'. Add a new version to it instead.");
        }

        ruleSets.Add(ruleSet);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RuleSetResponse.From(ruleSet);
    }

    public async Task<RuleSetVersionResponse> GetVersionAsync(Guid id, int number, CancellationToken cancellationToken)
    {
        var ruleSet = await ruleSets.GetByIdAsync(id, cancellationToken)
            ?? throw NotFoundException.For(nameof(RuleSet), id);
        var version = ruleSet.FindVersion(number)
            ?? throw NotFoundException.For(nameof(RuleSetVersion), $"{id}/v{number}");
        return RuleSetVersionResponse.From(version);
    }

    public async Task<RuleSetVersionResponse> AddVersionAsync(
        Guid id, CreateRuleSetVersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await versionValidator.ValidateAndThrowAsync(request, cancellationToken);

        // Each rule's strategy knows the shape of its own parameters; reject bad ones NOW,
        // not when someone tries to book months later.
        var parameterErrors = request.Rules
            .SelectMany((rule, index) => parametersValidator
                .Validate(rule.Type, rule.Parameters.GetRawText())
                .Select(message => new ValidationFailure($"Rules[{index}].Parameters", message)))
            .ToList();
        if (parameterErrors.Count > 0)
        {
            throw new ValidationException(parameterErrors);
        }

        var ruleSet = await ruleSets.GetForUpdateAsync(id, cancellationToken)
            ?? throw NotFoundException.For(nameof(RuleSet), id);

        var rules = request.Rules.Select(r => new RuleDefinition(r.Type, r.Parameters.GetRawText()));
        var version = ruleSet.AddVersion(rules, actor.Name, UtcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RuleSetVersionResponse.From(version);
    }

    public async Task<RuleSetVersionResponse> SubmitAsync(
        Guid id, int number, byte[]? expectedVersion, CancellationToken cancellationToken)
    {
        var (_, version) = await LoadVersionForUpdateAsync(id, number, expectedVersion, cancellationToken);
        version.Submit(actor.Name, UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RuleSetVersionResponse.From(version);
    }

    public async Task<RuleSetVersionResponse> ApproveAsync(
        Guid id, int number, byte[]? expectedVersion, CancellationToken cancellationToken)
    {
        var (ruleSet, _) = await LoadVersionForUpdateAsync(id, number, expectedVersion, cancellationToken);
        var approved = ruleSet.ApproveVersion(number, actor.Name, UtcNow); // also retires the previous Active
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RuleSetVersionResponse.From(approved);
    }

    public async Task<RuleSetVersionResponse> RejectAsync(
        Guid id,
        int number,
        RejectRuleSetVersionRequest request,
        byte[]? expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await rejectValidator.ValidateAndThrowAsync(request, cancellationToken);

        var (_, version) = await LoadVersionForUpdateAsync(id, number, expectedVersion, cancellationToken);
        version.Reject(actor.Name, UtcNow, request.Comment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return RuleSetVersionResponse.From(version);
    }

    private async Task<(RuleSet RuleSet, RuleSetVersion Version)> LoadVersionForUpdateAsync(
        Guid id, int number, byte[]? expectedVersion, CancellationToken cancellationToken)
    {
        var ruleSet = await ruleSets.GetForUpdateAsync(id, cancellationToken)
            ?? throw NotFoundException.For(nameof(RuleSet), id);
        var version = ruleSet.FindVersion(number)
            ?? throw NotFoundException.For(nameof(RuleSetVersion), $"{id}/v{number}");

        ConcurrencyGuard.EnsureVersion(expectedVersion, version.RowVersion, nameof(RuleSetVersion), $"{id}/v{number}");
        return (ruleSet, version);
    }
}
