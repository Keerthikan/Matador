using Matador.Core.Domain;
using Matador.Core.Engine;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=================================================");
Console.WriteLine("          MATADOR - KLASSISK DANSK UDGAVE         ");
Console.WriteLine("=================================================\n");

var p1 = new Player("1", "Mads", 30000);
var p2 = new Player("2", "Lise", 30000);
var p3 = new Player("3", "Søren", 30000);

var engine = new GameEngine(new List<Player> { p1, p2, p3 });

engine.OnLog += msg => Console.WriteLine($"  {msg}");

int round = 1;
while (!engine.IsGameOver && round <= 15)
{
    Console.WriteLine($"\n--- RUNDE {round} ---");

    for (int i = 0; i < engine.Players.Count; i++)
    {
        if (engine.IsGameOver) break;

        var player = engine.CurrentPlayer;
        if (player.IsBankrupt)
        {
            engine.EndTurn();
            continue;
        }

        Console.WriteLine($"\n▶ Tur: {player.Name} (Saldo: kr. {player.Balance:N0} | Position: {player.Position})");

        // Slå terninger og flyt
        engine.RollDiceAndMove();

        // Hvis grunden er til salg, køber vi den hvis saldo er god
        if (engine.State.Phase == TurnPhase.PendingBuyOrPass)
        {
            var space = engine.Board[player.Position];
            if (space is OwnableSpace ownable && player.Balance >= ownable.Price + 2000)
            {
                engine.BuyProperty(player);
            }
            else
            {
                engine.PassBuyProperty();
            }
        }

        // Tjek om spilleren har råd til at bygge huse på sine fulde serier
        foreach (var prop in player.OwnedProperties.OfType<StreetSpace>().ToList())
        {
            if (player.Balance > 8000 && engine.BuyHouse(player, prop))
            {
                // Forsøgte at bygge
            }
        }

        engine.EndTurn();
    }

    round++;
}

Console.WriteLine("\n=================================================");
Console.WriteLine("                   STATUS EFTER SPIL             ");
Console.WriteLine("=================================================");
foreach (var p in engine.Players)
{
    string status = p.IsBankrupt ? "BANKEROT" : $"kr. {p.Balance:N0} (Nettoformue: kr. {p.CalculateTotalNetWorth():N0})";
    Console.WriteLine($"* {p.Name,-8}: {status} | Ejendomme: {p.OwnedProperties.Count}");
}

if (engine.Winner != null)
{
    Console.WriteLine($"\n🏆 Vinderen er: {engine.Winner.Name}!");
}
