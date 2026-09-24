namespace Matador.Core.Domain;

public enum CityTheme
{
    Copenhagen, // Original (København)
    Aarhus,     // Smilets By (Aarhus)
    Odense,     // H.C. Andersens By (Odense)
    Aalborg     // Nordens Paris (Aalborg)
}

public class BoardFactory
{
    public static Board CreateBoard(CityTheme city = CityTheme.Copenhagen)
    {
        return city switch
        {
            CityTheme.Aarhus => CreateAarhusBoard(),
            CityTheme.Odense => CreateOdenseBoard(),
            CityTheme.Aalborg => CreateAalborgBoard(),
            _ => new Board() // Standard København
        };
    }

    private static Board CreateAarhusBoard()
    {
        var board = new Board();

        // Blå
        SetStreet(board, 1, "Hasle Torv");
        SetStreet(board, 3, "Viby Torv");
        // Transport & Bryggeri
        SetShipping(board, 5, "Mols-Linien");
        // Orange
        SetStreet(board, 6, "Silkeborgvej");
        SetStreet(board, 8, "Viborgvej");
        SetStreet(board, 9, "Paludan Müllers Vej");
        // Grøn
        SetStreet(board, 11, "Trøjborgvej");
        SetBrewery(board, 12, "Ceres Bryghus");
        SetStreet(board, 13, "Nørre Allé");
        SetStreet(board, 14, "Vesterbro Torv");
        // Transport
        SetShipping(board, 15, "Aarhus Letbane");
        // Grå
        SetStreet(board, 16, "Skovvejen");
        SetStreet(board, 18, "Marselisborg Allé");
        SetStreet(board, 19, "Strandvejen (Aarhus)");
        // Rød
        SetStreet(board, 21, "Mejlgade");
        SetStreet(board, 23, "Guldsmedgade");
        SetStreet(board, 24, "Vestergade (Aarhus)");
        // Transport
        SetShipping(board, 25, "Aarhus Havn");
        // Hvid
        SetStreet(board, 26, "Åboulevarden");
        SetStreet(board, 27, "Store Torv");
        SetBrewery(board, 28, "Aarhus Bryghus");
        SetStreet(board, 29, "Sankt Clemens Torv");
        // Gul
        SetStreet(board, 31, "Telefontorvet");
        SetStreet(board, 32, "Frederiksgade");
        SetStreet(board, 34, "Søndergade");
        // Transport
        SetShipping(board, 35, "Djurslands Færgen");
        // Lilla (De dyreste)
        SetStreet(board, 37, "Strøget (Aarhus)");
        SetStreet(board, 39, "Ryesgade");

        return board;
    }

    private static Board CreateOdenseBoard()
    {
        var board = new Board();

        // Blå
        SetStreet(board, 1, "Tarupvej");
        SetStreet(board, 3, "Vollsmose Allé");
        // Transport
        SetShipping(board, 5, "Odense Letbane");
        // Orange
        SetStreet(board, 6, "Hjallesevej");
        SetStreet(board, 8, "Middelfartvej");
        SetStreet(board, 9, "Dalumvej");
        // Grøn
        SetStreet(board, 11, "Skibhusvej");
        SetBrewery(board, 12, "Albani Bryggeri");
        SetStreet(board, 13, "Kochsgade");
        SetStreet(board, 14, "Nørregade");
        // Transport
        SetShipping(board, 15, "DSB Fyn");
        // Grå
        SetStreet(board, 16, "Læssøegade");
        SetStreet(board, 18, "Hunderupvej");
        SetStreet(board, 19, "Langelinie (Odense)");
        // Rød
        SetStreet(board, 21, "Klaregade");
        SetStreet(board, 23, "Vindegade");
        SetStreet(board, 24, "Gravene");
        // Transport
        SetShipping(board, 25, "Svendborg-færgen");
        // Hvid
        SetStreet(board, 26, "Kongensgade");
        SetStreet(board, 27, "Klingenberg");
        SetBrewery(board, 28, "Munkebo Brewery");
        SetStreet(board, 29, "Overgade");
        // Gul
        SetStreet(board, 31, "Brandts Torv");
        SetStreet(board, 32, "Vestergade (Odense)");
        SetStreet(board, 34, "Mageløs");
        // Transport
        SetShipping(board, 35, "Bøjden-Fynshav");
        // Lilla (De dyreste)
        SetStreet(board, 37, "H.C. Andersens Vej");
        SetStreet(board, 39, "Flakhaven");

        return board;
    }

    private static Board CreateAalborgBoard()
    {
        var board = new Board();

        // Blå
        SetStreet(board, 1, "Vejgaard Bymidte");
        SetStreet(board, 3, "Nørresundby Torv");
        // Transport
        SetShipping(board, 5, "Limfjordsbroen");
        // Orange
        SetStreet(board, 6, "Hobrovej");
        SetStreet(board, 8, "Hadsundvej");
        SetStreet(board, 9, "Gugvej");
        // Grøn
        SetStreet(board, 11, "Kastetvej");
        SetBrewery(board, 12, "Aalborg Akvavit (Spritten)");
        SetStreet(board, 13, "Reberbansgade");
        SetStreet(board, 14, "Sankt Jørgens Gade");
        // Transport
        SetShipping(board, 15, "Egholm Færgen");
        // Grå
        SetStreet(board, 16, "Svalegårdsvej");
        SetStreet(board, 18, "Hasserisvej");
        SetStreet(board, 19, "Constabelvej (Hasseris)");
        // Rød
        SetStreet(board, 21, "Boulevarden");
        SetStreet(board, 23, "Bispensgade");
        SetStreet(board, 24, "Bredegade");
        // Transport
        SetShipping(board, 25, "Aalborg Havn");
        // Hvid
        SetStreet(board, 26, "Algade (Aalborg)");
        SetStreet(board, 27, "Østerågade");
        SetBrewery(board, 28, "Søgaards Bryghus");
        SetStreet(board, 29, "Nytorv (Aalborg)");
        // Gul
        SetStreet(board, 31, "Gabels Torv");
        SetStreet(board, 32, "Slotsgade");
        SetStreet(board, 34, "C.W. Obels Plads");
        // Transport
        SetShipping(board, 35, "Hals-Egense Færgen");
        // Lilla (De dyreste)
        SetStreet(board, 37, "Jomfru Ane Gade");
        SetStreet(board, 39, "Slotspladsen");

        return board;
    }

    private static void SetStreet(Board b, int index, string name)
    {
        if (b[index] is StreetSpace s)
        {
            // Opretter en ny StreetSpace med samme priser og lejer, men lokalt navn
            var field = typeof(Board).GetField("_spaces", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field?.GetValue(b) is Space[] spaces)
            {
                spaces[index] = new StreetSpace(index, name, s.Group, s.Price, s.HousePrice, s.BaseRent,
                    s.RentWith1House, s.RentWith2Houses, s.RentWith3Houses, s.RentWith4Houses, s.RentWithHotel);
            }
        }
    }

    private static void SetShipping(Board b, int index, string name)
    {
        var field = typeof(Board).GetField("_spaces", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(b) is Space[] spaces && spaces[index] is ShippingSpace s)
        {
            spaces[index] = new ShippingSpace(index, name, s.Price);
        }
    }

    private static void SetBrewery(Board b, int index, string name)
    {
        var field = typeof(Board).GetField("_spaces", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field?.GetValue(b) is Space[] spaces && spaces[index] is BrewerySpace s)
        {
            spaces[index] = new BrewerySpace(index, name, s.Price);
        }
    }
}
