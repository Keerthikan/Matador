using Matador.Core.Domain;
using Matador.Core.Engine;
using Xunit;

namespace Matador.Core.Tests;

public class ForfeitTests
{
    [Fact]
    public void PlayerForfeit_With3Players_GameContinuesBetweenRemainingTwo()
    {
        var p1 = new Player("1", "Mads", 30000);
        var p2 = new Player("2", "Lise", 30000);
        var p3 = new Player("3", "Søren", 30000);

        var engine = new GameEngine(new List<Player> { p1, p2, p3 });

        // Mads forlader spillet -> Starter afstemning
        engine.PlayerForfeit(p1);

        Assert.True(p1.IsBankrupt);
        Assert.False(engine.IsGameOver);
        Assert.NotNull(engine.ActiveVoteSession);

        // De to tilbageværende stemmer for at fortsætte
        engine.VoteOnContinue(p2.Id, true);
        engine.VoteOnContinue(p3.Id, true);

        Assert.False(engine.IsGameOver); // Spillet fortsætter da Lise og Søren er tilbage!
        Assert.Null(engine.Winner);
        Assert.Equal(p2, engine.CurrentPlayer); // Turen går videre til næste aktive spiller (Lise)
    }

    [Fact]
    public void PlayerForfeit_With2Players_EndsGameAndDeclaresRemainingAsWinner()
    {
        var p1 = new Player("1", "Mads", 30000);
        var p2 = new Player("2", "Lise", 30000);

        var engine = new GameEngine(new List<Player> { p1, p2 });

        // Mads forlader spillet
        engine.PlayerForfeit(p1);

        Assert.True(p1.IsBankrupt);
        Assert.True(engine.IsGameOver); // Kun Lise tilbage -> Spillet er slut!
        Assert.Equal(p2, engine.Winner);
    }
}
