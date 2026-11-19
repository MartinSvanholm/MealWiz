namespace TheMealWorkshop.Shared.Amounts;

/// <summary>
/// Which amounts can be added together. Mass and Volume convert exactly between their units; Cup, Teaspoon and
/// Tablespoon each hold one unit and convert to nothing, not even each other. Count is "no unit".
/// </summary>
public enum UnitFamily
{
    Count,
    Mass,
    Volume,
    Cup,
    Teaspoon,
    Tablespoon
}
