namespace Matador.Core.Domain;

public abstract class Space
{
    public int Index { get; }
    public string Name { get; }
    public SpaceType Type { get; }

    protected Space(int index, string name, SpaceType type)
    {
        Index = index;
        Name = name;
        Type = type;
    }

    public override string ToString() => $"[{Index}] {Name} ({Type})";
}

public abstract class OwnableSpace : Space
{
    public int Price { get; }
    public int MortgageValue { get; }
    public bool IsMortgaged { get; set; }
    public Player? Owner { get; set; }

    protected OwnableSpace(int index, string name, SpaceType type, int price, int mortgageValue)
        : base(index, name, type)
    {
        Price = price;
        MortgageValue = mortgageValue;
    }

    public abstract int CalculateRent(Board board, int diceRoll);
}

public class StreetSpace : OwnableSpace
{
    public PropertyGroup Group { get; }
    public int BaseRent { get; }
    public int RentWith1House { get; }
    public int RentWith2Houses { get; }
    public int RentWith3Houses { get; }
    public int RentWith4Houses { get; }
    public int RentWithHotel { get; }
    public int HousePrice { get; }

    public int HouseCount { get; set; } // 0-4 = huse, 5 = hotel
    public bool HasHotel => HouseCount == 5;

    public StreetSpace(
        int index,
        string name,
        PropertyGroup group,
        int price,
        int housePrice,
        int baseRent,
        int r1,
        int r2,
        int r3,
        int r4,
        int rHotel)
        : base(index, name, SpaceType.Street, price, price / 2)
    {
        Group = group;
        HousePrice = housePrice;
        BaseRent = baseRent;
        RentWith1House = r1;
        RentWith2Houses = r2;
        RentWith3Houses = r3;
        RentWith4Houses = r4;
        RentWithHotel = rHotel;
    }

    public override int CalculateRent(Board board, int diceRoll)
    {
        if (IsMortgaged) return 0;

        if (HouseCount == 0)
        {
            // Dobbelt leje hvis spilleren ejer hele farvegruppen
            bool ownsAllInGroup = board.PlayerOwnsAllInGroup(Owner!, Group);
            return ownsAllInGroup ? BaseRent * 2 : BaseRent;
        }

        return HouseCount switch
        {
            1 => RentWith1House,
            2 => RentWith2Houses,
            3 => RentWith3Houses,
            4 => RentWith4Houses,
            5 => RentWithHotel,
            _ => BaseRent
        };
    }
}

public class ShippingSpace : OwnableSpace
{
    public ShippingSpace(int index, string name, int price = 4000)
        : base(index, name, SpaceType.Shipping, price, price / 2)
    {
    }

    public override int CalculateRent(Board board, int diceRoll)
    {
        if (IsMortgaged || Owner == null) return 0;

        int count = board.GetOwnedShippingCount(Owner);
        return count switch
        {
            1 => 500,
            2 => 1000,
            3 => 2000,
            4 => 4000,
            _ => 0
        };
    }
}

public class BrewerySpace : OwnableSpace
{
    public BrewerySpace(int index, string name, int price = 3000)
        : base(index, name, SpaceType.Brewery, price, price / 2)
    {
    }

    public override int CalculateRent(Board board, int diceRoll)
    {
        if (IsMortgaged || Owner == null) return 0;

        int count = board.GetOwnedBreweryCount(Owner);
        // Ejer 1: 100 x terningeslag. Ejer 2: 200 x terningeslag.
        int multiplier = count >= 2 ? 200 : 100;
        return diceRoll * multiplier;
    }
}

public class TaxSpace : Space
{
    public int Amount { get; }
    public double? Percentage { get; }

    public TaxSpace(int index, string name, int amount, double? percentage = null)
        : base(index, name, SpaceType.Tax)
    {
        Amount = amount;
        Percentage = percentage;
    }
}

public class ChanceSpace : Space
{
    public ChanceSpace(int index, string name = "Prøv Lykken")
        : base(index, name, SpaceType.Chance)
    {
    }
}

public class ActionSpace : Space
{
    public ActionSpace(int index, string name, SpaceType type)
        : base(index, name, type)
    {
    }
}
