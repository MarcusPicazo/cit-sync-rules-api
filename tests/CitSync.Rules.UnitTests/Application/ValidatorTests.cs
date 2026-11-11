using CitSync.Rules.Application.Reservations;
using CitSync.Rules.Application.Resources;
using CitSync.Rules.Domain.Reservations;
using CitSync.Rules.Domain.Resources;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace CitSync.Rules.UnitTests.Application;

public class CreateResourceRequestValidatorTests
{
    private readonly CreateResourceRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.TestValidate(new CreateResourceRequest("Laboratorio 2", ResourceCategory.Lab, 25, "Edificio B"));

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Blank_name_unknown_category_and_zero_capacity_fail()
    {
        var result = _validator.TestValidate(new CreateResourceRequest(" ", (ResourceCategory)99, 0, null));

        result.ShouldHaveValidationErrorFor(r => r.Name);
        result.ShouldHaveValidationErrorFor(r => r.Category);
        result.ShouldHaveValidationErrorFor(r => r.Capacity);
    }
}

public class CreateReservationRequestValidatorTests
{
    private static readonly DateTimeOffset Start = new(2030, 1, 15, 9, 0, 0, TimeSpan.FromHours(-6));

    private readonly CreateReservationRequestValidator _validator = new();

    private static CreateReservationRequest Valid() => new(
        Guid.NewGuid(), "A01234", RequesterRole.Alumno, Start, Start.AddHours(1), 1, "Química");

    [Fact]
    public void Valid_request_passes()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void End_before_start_fails_on_End()
    {
        var request = Valid() with { End = Start.AddMinutes(-30) };

        _validator.TestValidate(request)
            .ShouldHaveValidationErrorFor(r => r.End)
            .WithErrorMessage("'End' must be after 'Start'.");
    }

    [Fact]
    public void Missing_fields_fail()
    {
        var request = Valid() with
        {
            ResourceId = Guid.Empty,
            RequesterId = "",
            Subject = "",
            Quantity = 0,
            RequesterRole = 0,
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(r => r.ResourceId);
        result.ShouldHaveValidationErrorFor(r => r.RequesterId);
        result.ShouldHaveValidationErrorFor(r => r.Subject);
        result.ShouldHaveValidationErrorFor(r => r.Quantity);
        result.ShouldHaveValidationErrorFor(r => r.RequesterRole);
    }
}

public class ListReservationsQueryValidatorTests
{
    private readonly ListReservationsQueryValidator _validator = new();

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public void Invalid_paging_fails(int page, int pageSize)
    {
        var result = _validator.TestValidate(new ListReservationsQuery { Page = page, PageSize = pageSize });

        result.IsValid.Should().BeFalse();
    }

    [Fact(Skip = "TODO(Marcus): remove after adding the From/To rule in ListReservationsQueryValidator")]
    public void To_must_be_after_From()
    {
        var from = new DateTimeOffset(2030, 1, 15, 0, 0, 0, TimeSpan.Zero);

        var result = _validator.TestValidate(new ListReservationsQuery { From = from, To = from.AddDays(-1) });

        result.ShouldHaveValidationErrorFor(q => q.To);
    }

    [Fact]
    public void Only_From_is_fine()
    {
        var result = _validator.TestValidate(new ListReservationsQuery { From = DateTimeOffset.UnixEpoch });

        result.ShouldNotHaveAnyValidationErrors();
    }
}
