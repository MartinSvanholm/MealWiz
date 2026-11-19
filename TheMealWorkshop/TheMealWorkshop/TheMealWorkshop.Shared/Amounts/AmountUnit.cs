namespace TheMealWorkshop.Shared.Amounts;

/// <summary>One of the ten stored units. A count has no unit (<c>amount_unit</c> is null).</summary>
public sealed class AmountUnit
{
    private AmountUnit(string code, UnitFamily family, decimal factor)
    {
        Code = code;
        Family = family;
        Factor = factor;
    }

    /// <summary>The code stored in <c>amount_unit</c>: g, kg, oz, lb, ml, dl, l, cup, tsk or spsk.</summary>
    public string Code { get; }

    public UnitFamily Family { get; }

    /// <summary>Base units (g or ml) in one of this unit; 1 for cup, tsk and spsk, which have no other unit.</summary>
    public decimal Factor { get; }

    public static IReadOnlyList<AmountUnit> All { get; } =
    [
        new("g", UnitFamily.Mass, 1m),
        new("kg", UnitFamily.Mass, 1000m),
        new("oz", UnitFamily.Mass, 28.349523125m),
        new("lb", UnitFamily.Mass, 453.59237m),
        new("ml", UnitFamily.Volume, 1m),
        new("dl", UnitFamily.Volume, 100m),
        new("l", UnitFamily.Volume, 1000m),
        new("cup", UnitFamily.Cup, 1m),
        new("tsk", UnitFamily.Teaspoon, 1m),
        new("spsk", UnitFamily.Tablespoon, 1m),
    ];

    public static AmountUnit? FromCode(string? code) => All.FirstOrDefault(u => u.Code == code);

    public static IEnumerable<AmountUnit> InFamily(UnitFamily family) => All.Where(u => u.Family == family);

    public override string ToString() => Code;
}
