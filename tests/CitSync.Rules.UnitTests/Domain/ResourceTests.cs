using CitSync.Rules.Domain.Resources;
using FluentAssertions;

namespace CitSync.Rules.UnitTests.Domain;

public class ResourceTests
{
    [Fact]
    public void Constructor_creates_an_active_resource_with_trimmed_values()
    {
        var resource = new Resource("  Laboratorio de Cómputo 1 ", ResourceCategory.Lab, 30, " Edificio A ");

        resource.Id.Should().NotBeEmpty();
        resource.Name.Should().Be("Laboratorio de Cómputo 1");
        resource.Building.Should().Be("Edificio A");
        resource.Capacity.Should().Be(30);
        resource.IsActive.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_rejects_blank_name(string name)
    {
        var act = () => new Resource(name, ResourceCategory.Classroom, 1);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Constructor_rejects_non_positive_capacity(int capacity)
    {
        var act = () => new Resource("Proyector Epson", ResourceCategory.Equipment, capacity);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("capacity");
    }

    [Fact]
    public void Deactivate_marks_the_resource_as_inactive()
    {
        var resource = new Resource("Aula 3", ResourceCategory.Classroom, 40);

        resource.Deactivate();

        resource.IsActive.Should().BeFalse();
    }
}