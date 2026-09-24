namespace Matador.Core.Domain.Chance;

public enum ChanceActionType
{
    ReceiveMoney,
    PayMoney,
    MoveToSpace,
    MoveSteps,
    GoToJail,
    GetOutOfJailCard,
    PayPerHouseAndHotel,
    ReceiveFromAllPlayers,
    MatadorGrant
}

public class ChanceCard
{
    public int Id { get; }
    public string Text { get; }
    public ChanceActionType ActionType { get; }
    public int Amount { get; }
    public int TargetSpaceIndex { get; }
    public int HouseCost { get; }
    public int HotelCost { get; }

    public ChanceCard(
        int id,
        string text,
        ChanceActionType actionType,
        int amount = 0,
        int targetSpaceIndex = 0,
        int houseCost = 0,
        int hotelCost = 0)
    {
        Id = id;
        Text = text;
        ActionType = actionType;
        Amount = amount;
        TargetSpaceIndex = targetSpaceIndex;
        HouseCost = houseCost;
        HotelCost = hotelCost;
    }

    public override string ToString() => Text;
}

public class ChanceDeck
{
    private readonly Queue<ChanceCard> _cards = new();
    private readonly Random _random;

    public ChanceDeck(Random? random = null)
    {
        _random = random ?? new Random();
        InitializeDeck();
    }

    private void InitializeDeck()
    {
        var cardList = new List<ChanceCard>
        {
            // Matador Legat
            new(101, "De modtager Matador-legatet for værdig trængende på kr. 40.000. Ved værdig trængende forstås, at Deres formue (kontanter og ejendomme) ikke overstiger kr. 15.000.", ChanceActionType.MatadorGrant, amount: 40000),

            // Frikort (flere i spillet)
            new(102, "I anledning af Kongens fødselsdag benådes De herved for fængsel. Dette kort kan opbevares, indtil De får brug for det, eller De kan sælge det til en medspiller.", ChanceActionType.GetOutOfJailCard),
            new(103, "De løslades uden erstatning. Bevar dette kort, indtil De får brug for det, eller sælg det.", ChanceActionType.GetOutOfJailCard),

            // Gevinster & Indtægter
            new(1, "De modtager Deres aktieudbytte. Modtag kr. 1.000 af banken.", ChanceActionType.ReceiveMoney, amount: 1000),
            new(2, "De har vundet i Klasselotteriet. Modtag kr. 1.000 af banken.", ChanceActionType.ReceiveMoney, amount: 1000),
            new(3, "Kommunen har eftergivet et skattebeløb. Modtag kr. 3.000 af banken.", ChanceActionType.ReceiveMoney, amount: 3000),
            new(4, "De har solgt nogle antikviteter. Modtag kr. 1.500.", ChanceActionType.ReceiveMoney, amount: 1500),
            new(5, "Det er Deres fødselsdag. Modtag af hver medspiller kr. 500.", ChanceActionType.ReceiveFromAllPlayers, amount: 500),
            new(6, "De har solgt nogle gamle møbler på auktion. Modtag kr. 1.000 af banken.", ChanceActionType.ReceiveMoney, amount: 1000),
            new(7, "Deres præmieobligation er udtrukket. De modtager kr. 1.000 af banken.", ChanceActionType.ReceiveMoney, amount: 1000),
            new(8, "Grundet dyrtiden har De fået gageforhøjelse. Modtag kr. 1.000.", ChanceActionType.ReceiveMoney, amount: 1000),

            // Regninger & Bøder
            new(9, "De har kørt frem mod fuldt stop. Betal kr. 1.000 i bøde.", ChanceActionType.PayMoney, amount: 1000),
            new(10, "Betal for vask og strygning af Deres tøj kr. 500.", ChanceActionType.PayMoney, amount: 500),
            new(11, "De har modtaget Deres tandlægeregning. Betal kr. 2.000.", ChanceActionType.PayMoney, amount: 2000),
            new(12, "De har været en tur i udlandet og indført for mange cigaretter. Betal kr. 200 i told.", ChanceActionType.PayMoney, amount: 200),
            new(13, "De har parkeret ulovligt. Betal kr. 200 i bøde.", ChanceActionType.PayMoney, amount: 200),
            new(14, "De har købt 2 kasser luksusøl. Betal kr. 200.", ChanceActionType.PayMoney, amount: 200),
            new(15, "Deres bil skal til syn og reparation. Betal kr. 3.000.", ChanceActionType.PayMoney, amount: 3000),

            // Ejendomsskatter & reparationer
            new(16, "Ejendomsskatterne er steget. Betal kr. 800 pr. hus, kr. 2.300 pr. hotel.", ChanceActionType.PayPerHouseAndHotel, houseCost: 800, hotelCost: 2300),
            new(17, "Kul- og kokspriserne er steget. Betal kr. 500 pr. hus, kr. 2.000 pr. hotel.", ChanceActionType.PayPerHouseAndHotel, houseCost: 500, hotelCost: 2000),

            // Bevægelse
            new(18, "Ryk frem til START. Modtag kr. 4.000.", ChanceActionType.MoveToSpace, targetSpaceIndex: 0),
            new(19, "Ryk frem til Rådhuspladsen.", ChanceActionType.MoveToSpace, targetSpaceIndex: 39),
            new(20, "Ryk frem til Frederiksberg Allé. Hvis De passerer START, modtag kr. 4.000.", ChanceActionType.MoveToSpace, targetSpaceIndex: 11),
            new(21, "Ryk frem til Strandvejen. Hvis De passerer START, modtag kr. 4.000.", ChanceActionType.MoveToSpace, targetSpaceIndex: 19),
            new(22, "Ryk frem til Grønningen. Hvis De passerer START, modtag kr. 4.000.", ChanceActionType.MoveToSpace, targetSpaceIndex: 24),
            new(23, "Tag med færgen: Ryk frem til D.F.D.S. Hvis De passerer START, modtag kr. 4.000.", ChanceActionType.MoveToSpace, targetSpaceIndex: 15),
            new(24, "Ryk 3 felter tilbage.", ChanceActionType.MoveSteps, amount: -3),
            new(25, "Gå i fængsel. Ryk direkte til fængslet. Selv om De passerer START, indkasseres ikke kr. 4.000.", ChanceActionType.GoToJail)
        };

        // Bland kortene
        var shuffled = cardList.OrderBy(_ => _random.Next()).ToList();
        foreach (var card in shuffled)
        {
            _cards.Enqueue(card);
        }
    }

    public ChanceCard DrawCard()
    {
        var card = _cards.Dequeue();
        // Hvis det ikke er frikort der gemmes, lægges det tilbage i bunden af dækket
        if (card.ActionType != ChanceActionType.GetOutOfJailCard)
        {
            _cards.Enqueue(card);
        }
        return card;
    }

    public void ReturnJailCard(ChanceCard card)
    {
        _cards.Enqueue(card);
    }
}
