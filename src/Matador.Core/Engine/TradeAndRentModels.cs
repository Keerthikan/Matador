using Matador.Core.Domain;

namespace Matador.Core.Engine;

public class RentClaim
{
    public Player Creditor { get; }
    public Player Debtor { get; }
    public OwnableSpace Property { get; }
    public int Amount { get; }
    public bool IsClaimed { get; set; }

    public RentClaim(Player creditor, Player debtor, OwnableSpace property, int amount)
    {
        Creditor = creditor;
        Debtor = debtor;
        Property = property;
        Amount = amount;
    }
}

public enum TradeStatus
{
    Pending,
    Accepted,
    Rejected
}

public class TradeOffer
{
    public int Id { get; }
    public Player FromPlayer { get; }
    public Player ToPlayer { get; }
    
    // Byttehandel med flere grunde:
    public List<OwnableSpace> OfferedProperties { get; } = new();
    public List<OwnableSpace> RequestedProperties { get; } = new();
    public int OfferedJailCards { get; }
    public int RequestedJailCards { get; }

    // Positiv: FromPlayer betaler Cash til ToPlayer. Negativ: ToPlayer betaler Cash til FromPlayer.
    public int CashAmount { get; }

    public TradeStatus Status { get; set; } = TradeStatus.Pending;

    // Bakudkompatible hjælpere
    public OwnableSpace? Property => RequestedProperties.FirstOrDefault();
    public int Price => CashAmount;
    public bool IsJailCardTrade => RequestedJailCards > 0 && RequestedProperties.Count == 0;

    public TradeOffer(
        int id,
        Player fromPlayer,
        Player toPlayer,
        IEnumerable<OwnableSpace>? offeredProperties,
        IEnumerable<OwnableSpace>? requestedProperties,
        int cashAmount,
        int offeredJailCards = 0,
        int requestedJailCards = 0)
    {
        Id = id;
        FromPlayer = fromPlayer;
        ToPlayer = toPlayer;
        if (offeredProperties != null) OfferedProperties.AddRange(offeredProperties);
        if (requestedProperties != null) RequestedProperties.AddRange(requestedProperties);
        CashAmount = cashAmount;
        OfferedJailCards = offeredJailCards;
        RequestedJailCards = requestedJailCards;
    }

    // Konstruktør til simpel 1-til-1 handel / kontantbud (bakudkompatibel)
    public TradeOffer(int id, Player fromPlayer, Player toPlayer, OwnableSpace? property, int price, bool isJailCardTrade = false)
        : this(id, fromPlayer, toPlayer,
               offeredProperties: null,
               requestedProperties: property != null ? new[] { property } : null,
               cashAmount: price,
               offeredJailCards: 0,
               requestedJailCards: isJailCardTrade ? 1 : 0)
    {
    }
}
