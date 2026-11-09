namespace CitSync.Rules.Domain.Resources;

/// <summary>
/// Something that can be reserved: a lab, a classroom or a piece of equipment.
/// </summary>
public sealed class Resource
{
    public const int NameMaxLength = 120;
    public const int BuildingMaxLength = 80;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public ResourceCategory Category { get; private set; }

    /// <summary>
    /// How many units can be booked at the same time (seats in a lab, laptops in a cart...).
    /// </summary>
    public int Capacity { get; private set; }

    public string? Building { get; private set; }
    public bool IsActive { get; private set; }

    public Resource(string name, ResourceCategory category, int capacity, string? building = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        // TODO(Marcus): validate that capacity is at least 1 and throw ArgumentOutOfRangeException otherwise
        // (hint: ArgumentOutOfRangeException has a static helper for this, like the one used for name above).
        // Then make the test Constructor_rejects_non_positive_capacity in the UnitTests project pass.

        Id = Guid.NewGuid();
        Name = name.Trim();
        Category = category;
        Capacity = capacity;
        Building = string.IsNullOrWhiteSpace(building) ? null : building.Trim();
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
