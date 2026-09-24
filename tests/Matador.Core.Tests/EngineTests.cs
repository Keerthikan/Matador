using Matador.Core.Domain;
using Matador.Core.Engine;
using Xunit;

namespace Matador.Core.Tests;

public class TestDice : IDice
{
    private readonly Queue<(int, int)> _rolls = new();

    public void EnqueueRoll(int d1, int d2) => _rolls.Enqueue((d1, d2));

    public (int Die1, int Die2) Roll()
    {
        return _rolls.Count > 0 ? _rolls.Dequeue() : (1, 2);
    }
}

public class GameEngineTests
{
    [Fact]
    public void PassingStart_Grants4000Kr()
    {
        var p1 = new Player("1", "Mads", 30000) { Position = 38 };
        var p2 = new Player("2", "Lise", 30000);
        var dice = new TestDice();
        dice.EnqueueRoll(1, 1); // 38 + 2 = 40 % 40 = 0 (Lander direkte på START)

        var engine = new GameEngine(new List<Player> { p1, p2 }, dice: dice);
        engine.RollDiceAndMove();

        Assert.Equal(0, p1.Position);
        Assert.Equal(34000, p1.Balance); // 30.000 + 4.000
    }

    [Fact]
    public void LandingOnUnownedProperty_AllowsBuying()
    {
        var p1 = new Player("1", "Mads", 30000) { Position = 0 };
        var p2 = new Player("2", "Lise", 30000);
        var dice = new TestDice();
        dice.EnqueueRoll(1, 2); // Land på felt 3 (Hvidovrevej, pris 1200)

        var engine = new GameEngine(new List<Player> { p1, p2 }, dice: dice);
        engine.RollDiceAndMove();

        Assert.Equal(TurnPhase.PendingBuyOrPass, engine.State.Phase);
        bool bought = engine.BuyProperty(p1);

        Assert.True(bought);
        Assert.Equal(30000 - 1200, p1.Balance);
        var street = (StreetSpace)engine.Board[3];
        Assert.Equal(p1, street.Owner);
        Assert.Contains(street, p1.OwnedProperties);
    }

    [Fact]
    public void LandingOnOwnedProperty_PaysRentToOwner()
    {
        var p1 = new Player("1", "Mads", 30000);
        var p2 = new Player("2", "Lise", 30000);
        var board = new Board();
        
        var street = (StreetSpace)board[1]; // Rødovrevej (BaseRent = 50)
        street.Owner = p2;
        p2.OwnedProperties.Add(street);

        var dice = new TestDice();
        dice.EnqueueRoll(0, 1); // p1 lander på 1

        var engine = new GameEngine(new List<Player> { p1, p2 }, board: board, dice: dice);
        engine.RollDiceAndMove();

        Assert.NotNull(engine.PendingRentClaim);
        engine.ClaimRent();

        Assert.Equal(30000 - 50, p1.Balance);
        Assert.Equal(30000 + 50, p2.Balance);
    }

    [Fact]
    public void FullColorGroup_DoublesBaseRent()
    {
        var p1 = new Player("1", "Mads", 30000);
        var p2 = new Player("2", "Lise", 30000);
        var board = new Board();

        // P2 ejer både Rødovrevej (1) og Hvidovrevej (3)
        var s1 = (StreetSpace)board[1];
        var s2 = (StreetSpace)board[3];
        s1.Owner = p2;
        s2.Owner = p2;
        p2.OwnedProperties.Add(s1);
        p2.OwnedProperties.Add(s2);

        var dice = new TestDice();
        dice.EnqueueRoll(0, 1); // p1 lander på Rødovrevej

        var engine = new GameEngine(new List<Player> { p1, p2 }, board: board, dice: dice);
        engine.RollDiceAndMove();

        engine.ClaimRent();

        // 50 * 2 = 100 i monopol-leje
        Assert.Equal(30000 - 100, p1.Balance);
        Assert.Equal(30000 + 100, p2.Balance);
    }

    [Fact]
    public void ForgettingToClaimRent_ExpiresRentWhenNextPlayerRolls()
    {
        var p1 = new Player("1", "Mads", 30000);
        var p2 = new Player("2", "Lise", 30000);
        var board = new Board();

        var s1 = (StreetSpace)board[1];
        s1.Owner = p2;
        p2.OwnedProperties.Add(s1);

        var dice = new TestDice();
        dice.EnqueueRoll(0, 1); // Mads lander på 1
        dice.EnqueueRoll(1, 2); // Lise kaster i næste tur UDEN at opkræve leje!

        var engine = new GameEngine(new List<Player> { p1, p2 }, board: board, dice: dice);
        engine.RollDiceAndMove();

        Assert.NotNull(engine.PendingRentClaim);
        engine.EndTurn();

        // Lise slår nu terningerne i sin tur
        engine.RollDiceAndMove();

        // Kravet skal være bortfaldet!
        Assert.Null(engine.PendingRentClaim);
        Assert.Equal(30000, p1.Balance); // Mads beholdt sine penge!
    }

    [Fact]
    public void LandingOnGoToJail_MovesPlayerToJailField()
    {
        var p1 = new Player("1", "Mads", 30000) { Position = 28 };
        var p2 = new Player("2", "Lise", 30000);
        var dice = new TestDice();
        dice.EnqueueRoll(1, 1); // 28 + 2 = 30 (Gå i fængsel)

        var engine = new GameEngine(new List<Player> { p1, p2 }, dice: dice);
        engine.RollDiceAndMove();

        Assert.Equal(10, p1.Position);
        Assert.True(p1.IsInJail);
    }

    [Fact]
    public void ThreeConsecutiveDoubles_SendsPlayerToJail()
    {
        var p1 = new Player("1", "Mads", 30000);
        var p2 = new Player("2", "Lise", 30000);
        var dice = new TestDice();
        dice.EnqueueRoll(1, 1); // 1. gang ens
        dice.EnqueueRoll(2, 2); // 2. gang ens
        dice.EnqueueRoll(3, 3); // 3. gang ens -> Fængsel!

        var engine = new GameEngine(new List<Player> { p1, p2 }, dice: dice);

        engine.RollDiceAndMove();
        Assert.False(p1.IsInJail);
        engine.EndTurn(); // forbereder næste tur for p1 pga. ens

        engine.RollDiceAndMove();
        Assert.False(p1.IsInJail);
        engine.EndTurn();

        engine.RollDiceAndMove();
        Assert.True(p1.IsInJail);
        Assert.Equal(10, p1.Position);
    }

    [Fact]
    public void ShippingRent_ScalesWithOwnedCount()
    {
        var p1 = new Player("1", "Mads", 30000);
        var p2 = new Player("2", "Lise", 30000);
        var board = new Board();

        var ship1 = (ShippingSpace)board[5];  // Ø.K.
        var ship2 = (ShippingSpace)board[15]; // D.F.D.S.
        ship1.Owner = p2;
        ship2.Owner = p2;
        p2.OwnedProperties.Add(ship1);
        p2.OwnedProperties.Add(ship2);

        var dice = new TestDice();
        dice.EnqueueRoll(2, 3); // Felt 5

        var engine = new GameEngine(new List<Player> { p1, p2 }, board: board, dice: dice);
        engine.RollDiceAndMove();

        engine.ClaimRent();

        // 2 rederier = 1.000 kr.
        Assert.Equal(30000 - 1000, p1.Balance);
        Assert.Equal(30000 + 1000, p2.Balance);
    }

    [Fact]
    public void Bankruptcy_TransfersAssetsToCreditor()
    {
        var p1 = new Player("1", "Mads", 500); // Lille formue
        var p2 = new Player("2", "Lise", 30000);
        var board = new Board();

        var street = (StreetSpace)board[39]; // Rådhuspladsen
        street.Owner = p2;
        street.HouseCount = 2; // Leje = 12.000 kr.
        p2.OwnedProperties.Add(street);

        var dice = new TestDice();
        dice.EnqueueRoll(19, 20); // Lander på 39

        var engine = new GameEngine(new List<Player> { p1, p2 }, board: board, dice: dice);
        engine.RollDiceAndMove();

        engine.ClaimRent();

        Assert.True(p1.IsBankrupt);
        Assert.True(engine.IsGameOver);
        Assert.Equal(p2, engine.Winner);
    }
}
