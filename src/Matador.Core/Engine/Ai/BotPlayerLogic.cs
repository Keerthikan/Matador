using Matador.Core.Domain;
using Matador.Core.Domain.Chance;

namespace Matador.Core.Engine.Ai;

public static class BotPlayerLogic
{
    private static readonly Random _rnd = new();

    /// <summary>
    /// Udfører et enkelt trin i en bots tur eller reaktion.
    /// Returnerer true hvis der blev udført en handling, eller false hvis botten ikke kan gøre mere lige nu.
    /// </summary>
    public static bool ExecuteStep(GameEngine engine, Player bot)
    {
        if (bot.IsBankrupt || engine.IsGameOver) return false;

        // 1. Tjek om botten er kreditor i et udestående lejekrav
        if (engine.PendingRentClaim != null && engine.PendingRentClaim.Creditor.Id == bot.Id && !engine.PendingRentClaim.IsClaimed)
        {
            // En bot opdager og opkræver sin leje 90% af tiden (giver lidt menneskelig fejlmargin!)
            if (_rnd.Next(100) < 90)
            {
                engine.ClaimRent();
                return true;
            }
        }

        // 2. Tjek om botten har modtaget et handelstilbud
        var pendingTrade = engine.TradeOffers.FirstOrDefault(t => t.Status == TradeStatus.Pending && t.ToPlayer.Id == bot.Id);
        if (pendingTrade != null)
        {
            // Simpel evaluering: Hvis køberen betaler 130% eller mere af grundens værdi, eller mindst kr. 2.000 for et frikort
            bool accept = false;
            if (pendingTrade.IsJailCardTrade)
            {
                accept = pendingTrade.Price >= 2000;
            }
            else if (pendingTrade.Property != null)
            {
                if (pendingTrade.Property is StreetSpace streetProp)
                {
                    // Hvis botten ikke selv er tæt på monopol i gruppen
                    var groupStreets = engine.Board.Spaces.OfType<StreetSpace>().Where(s => s.Group == streetProp.Group).ToList();
                    int ownedInGroup = groupStreets.Count(s => s.Owner == bot);
                    bool hasMonopoly = ownedInGroup == groupStreets.Count;

                    // Sælg aldrig et monopol, men sælg gerne isolerede grunde hvis prisen er god
                    if (!hasMonopoly && pendingTrade.Price >= (int)(streetProp.Price * 1.35))
                    {
                        accept = true;
                    }
                }
                else
                {
                    // Færger eller bryggerier
                    if (pendingTrade.Price >= (int)(pendingTrade.Property.Price * 1.30))
                    {
                        accept = true;
                    }
                }
            }

            engine.RespondToTrade(pendingTrade.Id, accept);
            return true;
        }

        // 3. Tjek om der er en aktiv afstemning (en spiller forlod spillet)
        if (engine.ActiveVoteSession != null && !engine.ActiveVoteSession.HasVoted(bot.Id))
        {
            // Bot stemmer altid for at fortsætte spillet
            engine.VoteOnContinue(bot.Id, true);
            return true;
        }

        // 4. Hvis det IKKE er denne bots tur, er der ikke mere den kan gøre
        if (engine.CurrentPlayer.Id != bot.Id) return false;

        // 5. Botten er i fængsel
        if (bot.IsInJail && engine.State.Phase == TurnPhase.WaitingForRoll)
        {
            // Hvis botten har frikort eller god saldo, kom ud
            if (bot.GetOutOfJailCards > 0 || bot.Balance >= 5000)
            {
                engine.PayOutOfJail(bot);
                return true;
            }
        }

        // 6. Bot skal kaste terninger
        if (engine.State.Phase == TurnPhase.WaitingForRoll)
        {
            engine.RollDiceAndMove();
            return true;
        }

        // 7. Bot skal beslutte om den køber grund
        if (engine.State.Phase == TurnPhase.PendingBuyOrPass)
        {
            var space = engine.Board[bot.Position] as OwnableSpace;
            if (space != null && space.Owner == null)
            {
                // Køb hvis botten har råd og beholder en sikkerhedsbuffer på mindst 1.500 kr.
                if (bot.Balance >= space.Price + 1500)
                {
                    engine.BuyProperty(bot);
                }
                else
                {
                    engine.PassBuyProperty();
                }
            }
            else
            {
                engine.PassBuyProperty();
            }
            return true;
        }

        // 8. Tjek for husbyggeri hvis botten ejer hele grupper
        if (engine.State.Phase == TurnPhase.ActionResolved)
        {
            foreach (var street in bot.OwnedProperties.OfType<StreetSpace>().ToList())
            {
                if (street.HouseCount < 5 && bot.Balance >= street.HousePrice + 3000)
                {
                    if (engine.BuyHouse(bot, street))
                    {
                        return true;
                    }
                }
            }

            // 9. Afslut turen
            engine.EndTurn();
            return true;
        }

        return false;
    }
}
