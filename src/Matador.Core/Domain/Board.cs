namespace Matador.Core.Domain;

public class Board
{
    private readonly Space[] _spaces = new Space[40];

    public IReadOnlyList<Space> Spaces => _spaces;

    public Board()
    {
        InitializeBoard();
    }

    public Space this[int index] => _spaces[(index % 40 + 40) % 40];

    private void InitializeBoard()
    {
        // 0: Start
        _spaces[0] = new ActionSpace(0, "START", SpaceType.Start);

        // 1: Rødovrevej (Blå)
        _spaces[1] = new StreetSpace(1, "Rødovrevej", PropertyGroup.Blue, 1200, 1000, 50, 250, 750, 2250, 4000, 6000);

        // 2: Prøv Lykken
        _spaces[2] = new ChanceSpace(2);

        // 3: Hvidovrevej (Blå)
        _spaces[3] = new StreetSpace(3, "Hvidovrevej", PropertyGroup.Blue, 1200, 1000, 50, 250, 750, 2250, 4000, 6000);

        // 4: Indkomstskat (Betal 10% eller 4.000 kr.)
        _spaces[4] = new TaxSpace(4, "Indkomstskat", 4000, 0.10);

        // 5: Rederi: Ø.K. (Hals-Egense / Øresund)
        _spaces[5] = new ShippingSpace(5, "Ø.K.");

        // 6: Roskildevej (Orange)
        _spaces[6] = new StreetSpace(6, "Roskildevej", PropertyGroup.Orange, 2000, 1000, 100, 600, 1800, 5400, 8000, 11000);

        // 7: Prøv Lykken
        _spaces[7] = new ChanceSpace(7);

        // 8: Valby Langgade (Orange)
        _spaces[8] = new StreetSpace(8, "Valby Langgade", PropertyGroup.Orange, 2000, 1000, 100, 600, 1800, 5400, 8000, 11000);

        // 9: Allégade (Orange)
        _spaces[9] = new StreetSpace(9, "Allégade", PropertyGroup.Orange, 2400, 1000, 150, 800, 2000, 6000, 9000, 12000);

        // 10: Fængsel (På besøg)
        _spaces[10] = new ActionSpace(10, "Fængsel (På besøg)", SpaceType.VisitJail);

        // 11: Frederiksberg Allé (Grøn)
        _spaces[11] = new StreetSpace(11, "Frederiksberg Allé", PropertyGroup.Green, 2800, 2000, 200, 1000, 3000, 9000, 12500, 15000);

        // 12: Tuborg (Bryggeri)
        _spaces[12] = new BrewerySpace(12, "Tuborg");

        // 13: Bülowsvej (Grøn)
        _spaces[13] = new StreetSpace(13, "Bülowsvej", PropertyGroup.Green, 2800, 2000, 200, 1000, 3000, 9000, 12500, 15000);

        // 14: Gl. Kongevej (Grøn)
        _spaces[14] = new StreetSpace(14, "Gl. Kongevej", PropertyGroup.Green, 3200, 2000, 250, 1250, 3750, 10000, 14000, 18000);

        // 15: D.F.D.S. (Rederi)
        _spaces[15] = new ShippingSpace(15, "D.F.D.S.");

        // 16: Bernstorffsvej (Grå)
        _spaces[16] = new StreetSpace(16, "Bernstorffsvej", PropertyGroup.Grey, 3600, 2000, 300, 1400, 4000, 11000, 15000, 19000);

        // 17: Prøv Lykken
        _spaces[17] = new ChanceSpace(17);

        // 18: Hellerupvej (Grå)
        _spaces[18] = new StreetSpace(18, "Hellerupvej", PropertyGroup.Grey, 3600, 2000, 300, 1400, 4000, 11000, 15000, 19000);

        // 19: Strandvejen (Grå)
        _spaces[19] = new StreetSpace(19, "Strandvejen", PropertyGroup.Grey, 4000, 2000, 350, 1600, 4400, 12000, 16000, 20000);

        // 20: Parkering (Gratis parkering / Hellere der end i fængsel)
        _spaces[20] = new ActionSpace(20, "Parkering", SpaceType.FreeParking);

        // 21: Trianglen (Rød)
        _spaces[21] = new StreetSpace(21, "Trianglen", PropertyGroup.Red, 4400, 3000, 350, 1800, 5000, 14000, 17500, 21000);

        // 22: Prøv Lykken
        _spaces[22] = new ChanceSpace(22);

        // 23: Østerbrogade (Rød)
        _spaces[23] = new StreetSpace(23, "Østerbrogade", PropertyGroup.Red, 4400, 3000, 350, 1800, 5000, 14000, 17500, 21000);

        // 24: Grønningen (Rød)
        _spaces[24] = new StreetSpace(24, "Grønningen", PropertyGroup.Red, 4800, 3000, 400, 2000, 6000, 15000, 18500, 22000);

        // 25: Ø.S. (Østasiatisk Kompagni / Mols-Linien / Rederi)
        _spaces[25] = new ShippingSpace(25, "Ø.S.");

        // 26: Bredgade (Hvid)
        _spaces[26] = new StreetSpace(26, "Bredgade", PropertyGroup.White, 5200, 3000, 450, 2200, 6600, 16000, 19500, 23000);

        // 27: Kgs. Nytorv (Hvid)
        _spaces[27] = new StreetSpace(27, "Kgs. Nytorv", PropertyGroup.White, 5200, 3000, 450, 2200, 6600, 16000, 19500, 23000);

        // 28: Carlsberg (Bryggeri)
        _spaces[28] = new BrewerySpace(28, "Carlsberg");

        // 29: Østergade (Hvid)
        _spaces[29] = new StreetSpace(29, "Østergade", PropertyGroup.White, 5600, 3000, 500, 2400, 7200, 17000, 20500, 24000);

        // 30: De fængsles (Gå i fængsel)
        _spaces[30] = new ActionSpace(30, "Gå i Fængsel", SpaceType.GoToJail);

        // 31: Amagertorv (Gul)
        _spaces[31] = new StreetSpace(31, "Amagertorv", PropertyGroup.Yellow, 6000, 4000, 550, 2600, 7800, 18000, 22000, 25000);

        // 32: Vimmelskaftet (Gul)
        _spaces[32] = new StreetSpace(32, "Vimmelskaftet", PropertyGroup.Yellow, 6000, 4000, 550, 2600, 7800, 18000, 22000, 25000);

        // 33: Prøv Lykken
        _spaces[33] = new ChanceSpace(33);

        // 34: Nygade (Gul)
        _spaces[34] = new StreetSpace(34, "Nygade", PropertyGroup.Yellow, 6400, 4000, 600, 3000, 9000, 20000, 24000, 28000);

        // 35: Bornholm (D/S Bornholm 1866 / Rederi)
        _spaces[35] = new ShippingSpace(35, "D/S Bornholm");

        // 36: Prøv Lykken
        _spaces[36] = new ChanceSpace(36);

        // 37: Frederiksberggade (Lilla)
        _spaces[37] = new StreetSpace(37, "Frederiksberggade", PropertyGroup.Purple, 7000, 4000, 700, 3500, 10000, 22000, 26000, 30000);

        // 38: Ekstraordinær Statsskat
        _spaces[38] = new TaxSpace(38, "Statsskat", 2000);

        // 39: Rådhuspladsen (Lilla)
        _spaces[39] = new StreetSpace(39, "Rådhuspladsen", PropertyGroup.Purple, 8000, 4000, 1000, 4000, 12000, 28000, 34000, 40000);
    }

    public bool PlayerOwnsAllInGroup(Player player, PropertyGroup group)
    {
        var groupSpaces = _spaces.OfType<StreetSpace>().Where(s => s.Group == group).ToList();
        return groupSpaces.Count > 0 && groupSpaces.All(s => s.Owner == player);
    }

    public int GetOwnedShippingCount(Player player)
    {
        return _spaces.OfType<ShippingSpace>().Count(s => s.Owner == player);
    }

    public int GetOwnedBreweryCount(Player player)
    {
        return _spaces.OfType<BrewerySpace>().Count(s => s.Owner == player);
    }
}
