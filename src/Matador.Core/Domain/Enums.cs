namespace Matador.Core.Domain;

public enum PropertyGroup
{
    Blue,        // Rødovrevej, Hvidovrevej
    Orange,      // Roskildevej, Valby Langgade, Allégade
    Green,       // Frederiksberg Allé, Bülowsvej, Gl. Kongevej
    Grey,        // Bernstorffsvej, Hellerupvej, Strandvejen
    Red,         // Trianglen, Østerbrogade, Grønningen
    White,       // Bredgade, Kgs. Nytorv, Østergade
    Yellow,      // Amagertorv, Vimmelskaftet, Nygade
    Purple,      // Frederiksberggade, Rådhuspladsen
    Shipping,    // DFDS, Mols-Linien, Scandlines, etc.
    Brewery      // Tuborg, Carlsberg
}

public enum SpaceType
{
    Start,
    Street,
    Shipping,
    Brewery,
    Chance,
    Tax,
    Jail,
    VisitJail,
    FreeParking,
    GoToJail
}
