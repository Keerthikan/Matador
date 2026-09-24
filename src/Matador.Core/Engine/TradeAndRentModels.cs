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
    public OwnableSpace? Property { get; }
    public bool IsJailCardTrade { get; }
    public int Price { get; }
    public TradeStatus Status { get; set; } = TradeStatus.Pending;

    public TradeOffer(int id, Player fromPlayer, Player toPlayer, OwnableSpace? property, int price, bool isJailCardTrade = false)
    {
        Id = id;
        FromPlayer = fromPlayer;
        ToPlayer = toPlayer;
        Property = property;
        Price = price;
        IsJailCardTrade = isJailCardTrade;
    }
}
