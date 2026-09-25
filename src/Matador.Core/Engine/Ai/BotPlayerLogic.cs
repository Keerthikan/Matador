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
            bool accept = EvaluateTradeOffer(engine, bot, pendingTrade);
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

    private static bool EvaluateTradeOffer(GameEngine engine, Player bot, TradeOffer trade)
    {
        // 1. Tjek om botten har råd til kontantkravet (hvis CashAmount er negativt, skal ToPlayer/bot betale penge)
        if (trade.CashAmount < 0 && bot.Balance < -trade.CashAmount)
        {
            return false;
        }

        // 2. Frikorthandel alene
        if (trade.IsJailCardTrade)
        {
            return trade.CashAmount >= 2000;
        }

        // 3. Sælg ALDRIG eller byt en grund væk, hvis botten derved bryder et eksisterende monopol
        foreach (var reqProp in trade.RequestedProperties.OfType<StreetSpace>())
        {
            var groupStreets = engine.Board.Spaces.OfType<StreetSpace>().Where(s => s.Group == reqProp.Group).ToList();
            bool hasMonopoly = groupStreets.All(s => s.Owner == bot);
            if (hasMonopoly) return false; // Beskyt eget monopol
        }

        // 4. Undgå at give modstanderen et nemt monopol (medmindre botten selv får et monopol eller massiv overpris)
        bool givesOpponentMonopoly = false;
        foreach (var reqProp in trade.RequestedProperties.OfType<StreetSpace>())
        {
            var groupStreets = engine.Board.Spaces.OfType<StreetSpace>().Where(s => s.Group == reqProp.Group).ToList();
            int opponentOwns = groupStreets.Count(s => s.Owner == trade.FromPlayer);
            if (opponentOwns == groupStreets.Count - 1)
            {
                givesOpponentMonopoly = true;
                break;
            }
        }

        // 5. Giver denne handel botten et nyt monopol?
        bool givesBotMonopoly = false;
        foreach (var offeredProp in trade.OfferedProperties.OfType<StreetSpace>())
        {
            var groupStreets = engine.Board.Spaces.OfType<StreetSpace>().Where(s => s.Group == offeredProp.Group).ToList();
            int botOwns = groupStreets.Count(s => s.Owner == bot);
            // Hvis botten mangler 1 grund i gruppen og modtager den her
            if (botOwns == groupStreets.Count - 1)
            {
                givesBotMonopoly = true;
                break;
            }
        }

        // Hvis botten opnår et monopol, er den meget villig til at bytte
        if (givesBotMonopoly && !givesOpponentMonopoly)
        {
            return true;
        }

        // Hvis handlen giver modstanderen monopol uden at botten selv får monopol, afvises det
        if (givesOpponentMonopoly && !givesBotMonopoly)
        {
            return false;
        }

        // 6. Værdi-evaluering: Hvad modtager botten vs hvad afgiver botten?
        // Botten modtager: OfferedProperties + OfferedJailCards (á 1500) + CashAmount (hvis positiv)
        int valueReceived = trade.OfferedProperties.Sum(p => p.Price) 
                          + (trade.OfferedJailCards * 1500) 
                          + (trade.CashAmount > 0 ? trade.CashAmount : 0);

        // Botten afgiver: RequestedProperties + RequestedJailCards (á 1500) + CashAmount (hvis negativ)
        int valueGiven = trade.RequestedProperties.Sum(p => p.Price) 
                       + (trade.RequestedJailCards * 1500) 
                       + (trade.CashAmount < 0 ? -trade.CashAmount : 0);

        // Botten accepterer hvis den modtagne værdi er mindst 125% af den afgivne værdi
        return valueReceived >= (int)(valueGiven * 1.25);
    }
}
