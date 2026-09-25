using Matador.Core.Domain;
using Matador.Core.Domain.Chance;

namespace Matador.Core.Engine;

public class GameEngine
{
    public Board Board { get; }
    public List<Player> Players { get; }
    public ChanceDeck ChanceDeck { get; }
    public IDice Dice { get; }
    public GameState State { get; }
    public RentClaim? PendingRentClaim { get; private set; }
    public List<TradeOffer> TradeOffers { get; } = new();
    public ContinueVoteSession? ActiveVoteSession { get; private set; }
    public int JackpotPool { get; private set; } = 1000; // Starter på kr. 1.000
    private int _nextTradeId = 1;

    public const int PassStartReward = 4000;
    public const int JailReleaseFee = 1000;

    public Player CurrentPlayer => Players[State.CurrentPlayerIndex];
    public bool IsGameOver => Players.Count(p => !p.IsBankrupt) <= 1;
    public Player? Winner => IsGameOver ? Players.FirstOrDefault(p => !p.IsBankrupt) : null;

    public event Action<string>? OnLog;

    public GameEngine(List<Player> players, Board? board = null, ChanceDeck? chanceDeck = null, IDice? dice = null)
    {
        if (players == null || players.Count < 2)
        {
            throw new ArgumentException("Der skal være mindst 2 spillere i Matador.", nameof(players));
        }

        Players = players;
        Board = board ?? new Board();
        ChanceDeck = chanceDeck ?? new ChanceDeck();
        Dice = dice ?? new StandardDice();
        State = new GameState();
    }

    private void Log(string message)
    {
        State.LastEventMessage = message;
        OnLog?.Invoke(message);
    }

    public void RollDiceAndMove()
    {
        // Glemte lejekrav: Hvis der var et uafhentet krav før terningerne kastes af en spiller
        CheckAndExpirePendingRent();

        if (IsGameOver)
        {
            Log("Spillet er slut!");
            return;
        }

        var player = CurrentPlayer;

        if (player.IsBankrupt)
        {
            EndTurn();
            return;
        }

        var (d1, d2) = Dice.Roll();
        State.LastRoll = (d1, d2);
        bool isDoubles = d1 == d2;

        Log($"{player.Name} slog {d1} og {d2} (Total: {d1 + d2})");

        // Fængselslogik
        if (player.IsInJail)
        {
            HandleJailTurn(player, d1, d2, isDoubles);
            return;
        }

        // Tjek for 3x to ens i træk -> direkte i fængsel
        if (isDoubles)
        {
            State.ConsecutiveDoubles++;
            if (State.ConsecutiveDoubles == 3)
            {
                Log($"{player.Name} slog to ens for 3. gang og ryger direkte i fængsel!");
                SendToJail(player);
                State.Phase = TurnPhase.ActionResolved;
                return;
            }
        }
        else
        {
            State.ConsecutiveDoubles = 0;
        }

        // Flyt spiller
        MovePlayer(player, d1 + d2);
    }

    private void CheckAndExpirePendingRent()
    {
        if (PendingRentClaim != null && !PendingRentClaim.IsClaimed)
        {
            Log($"⚠️ {PendingRentClaim.Creditor.Name} glemte at opkræve leje på {PendingRentClaim.Property.Name} (kr. {PendingRentClaim.Amount:N0}) fra {PendingRentClaim.Debtor.Name}!");
            PendingRentClaim = null;
        }
    }

    public bool ClaimRent()
    {
        if (PendingRentClaim == null || PendingRentClaim.IsClaimed) return false;

        var claim = PendingRentClaim;
        claim.IsClaimed = true;
        Log($"🔔 {claim.Creditor.Name} opkrævede leje på {claim.Property.Name}: kr. {claim.Amount:N0} fra {claim.Debtor.Name}!");
        PayMoney(claim.Debtor, claim.Amount, claim.Creditor);
        PendingRentClaim = null;
        return true;
    }

    public TradeOffer ProposeTrade(Player from, Player to, OwnableSpace property, int price)
    {
        var offer = new TradeOffer(_nextTradeId++, from, to, property, price);
        TradeOffers.Add(offer);
        Log($"💼 {from.Name} tilbyder {to.Name} at købe {property.Name} for kr. {price:N0}.");
        return offer;
    }

    public TradeOffer ProposeJailCardTrade(Player from, Player to, int price)
    {
        var offer = new TradeOffer(_nextTradeId++, from, to, null, price, isJailCardTrade: true);
        TradeOffers.Add(offer);
        Log($"💼 {from.Name} tilbyder {to.Name} at købe et Fængsels-Frikort for kr. {price:N0}.");
        return offer;
    }

    public bool RespondToTrade(int tradeId, bool accept)
    {
        var offer = TradeOffers.FirstOrDefault(t => t.Id == tradeId && t.Status == TradeStatus.Pending);
        if (offer == null) return false;

        string itemName = offer.IsJailCardTrade ? "Frikort til fængsel" : offer.Property?.Name ?? "Ejendom";

        if (!accept)
        {
            offer.Status = TradeStatus.Rejected;
            Log($"❌ {offer.ToPlayer.Name} afviste tilbuddet fra {offer.FromPlayer.Name} om {itemName}.");
            return true;
        }

        // Tjek om køberen har pengene
        if (offer.FromPlayer.Balance < offer.Price)
        {
            Log($"Handel kunne ikke gennemføres: {offer.FromPlayer.Name} har ikke råd.");
            return false;
        }

        if (offer.IsJailCardTrade)
        {
            if (offer.ToPlayer.GetOutOfJailCards <= 0)
            {
                Log($"Handel kunne ikke gennemføres: {offer.ToPlayer.Name} har ikke noget frikort længere.");
                return false;
            }

            // Gennemfør frikort-handel
            offer.FromPlayer.DeductMoney(offer.Price);
            offer.ToPlayer.AddMoney(offer.Price);
            offer.ToPlayer.GetOutOfJailCards--;
            offer.FromPlayer.GetOutOfJailCards++;
            offer.Status = TradeStatus.Accepted;

            Log($"🤝 HANDEL GENNEMFØRT! {offer.FromPlayer.Name} købte et Fængsels-Frikort af {offer.ToPlayer.Name} for kr. {offer.Price:N0}!");
            return true;
        }

        if (offer.Property == null || offer.Property.Owner != offer.ToPlayer)
        {
            Log($"Handel kunne ikke gennemføres: {offer.ToPlayer.Name} ejer ikke længere {itemName}.");
            return false;
        }

        // Gennemfør grund-handel
        offer.FromPlayer.DeductMoney(offer.Price);
        offer.ToPlayer.AddMoney(offer.Price);
        offer.ToPlayer.OwnedProperties.Remove(offer.Property);
        offer.Property.Owner = offer.FromPlayer;
        offer.FromPlayer.OwnedProperties.Add(offer.Property);
        offer.Status = TradeStatus.Accepted;

        Log($"🤝 HANDEL GENNEMFØRT! {offer.FromPlayer.Name} købte {offer.Property.Name} af {offer.ToPlayer.Name} for kr. {offer.Price:N0}!");
        return true;
    }

    private void HandleJailTurn(Player player, int d1, int d2, bool isDoubles)
    {
        player.TurnsInJail++;

        if (isDoubles)
        {
            Log($"{player.Name} slog to ens ({d1}-{d2}) og kommer ud af fængslet!");
            player.IsInJail = false;
            player.TurnsInJail = 0;
            State.ConsecutiveDoubles = 0; // må ikke slå igen ved løsladelse via to ens
            MovePlayer(player, d1 + d2);
            return;
        }

        if (player.TurnsInJail >= 3)
        {
            Log($"{player.Name} har siddet i fængsel i 3 omgange og skal betale kr. {JailReleaseFee:N0} for at komme ud.");
            PayMoney(player, JailReleaseFee, null);
            player.IsInJail = false;
            player.TurnsInJail = 0;
            MovePlayer(player, d1 + d2);
            return;
        }

        Log($"{player.Name} forbliver i fængsel (omgang {player.TurnsInJail} af 3).");
        State.Phase = TurnPhase.ActionResolved;
    }

    public bool PayOutOfJail(Player player)
    {
        if (!player.IsInJail) return false;

        if (player.GetOutOfJailCards > 0)
        {
            player.GetOutOfJailCards--;
            player.IsInJail = false;
            player.TurnsInJail = 0;
            Log($"{player.Name} brugte et 'Prøv Lykken' frikort til at komme ud af fængslet.");
            return true;
        }

        if (player.Balance >= JailReleaseFee)
        {
            player.DeductMoney(JailReleaseFee);
            player.IsInJail = false;
            player.TurnsInJail = 0;
            Log($"{player.Name} betalte kr. {JailReleaseFee:N0} og er nu fri fra fængslet.");
            return true;
        }

        return false;
    }

    public void MovePlayer(Player player, int steps, bool canCollectStart = true)
    {
        int oldPosition = player.Position;
        int newPosition = (oldPosition + steps) % 40;
        if (newPosition < 0) newPosition += 40;

        // Passerede START hvis newPosition < oldPosition (ved fremadgående bevægelse)
        if (canCollectStart && steps > 0 && newPosition < oldPosition)
        {
            player.AddMoney(PassStartReward);
            Log($"{player.Name} passerede START og modtog kr. {PassStartReward:N0}!");
        }

        player.Position = newPosition;
        var space = Board[newPosition];
        Log($"{player.Name} lander på felt {space.Index}: {space.Name}");

        ResolveSpaceLanding(player, space, steps);
    }

    public void MovePlayerToSpace(Player player, int targetIndex, bool canCollectStart = true)
    {
        int oldPosition = player.Position;
        if (canCollectStart && targetIndex < oldPosition && targetIndex != 10) // ikke hvis fængsel
        {
            player.AddMoney(PassStartReward);
            Log($"{player.Name} passerede START og modtog kr. {PassStartReward:N0}!");
        }

        player.Position = targetIndex;
        var space = Board[targetIndex];
        Log($"{player.Name} rykker til felt {space.Index}: {space.Name}");

        ResolveSpaceLanding(player, space, 0);
    }

    private void ResolveSpaceLanding(Player player, Space space, int diceTotal)
    {
        switch (space)
        {
            case OwnableSpace ownable:
                HandleOwnableLanding(player, ownable, diceTotal);
                break;

            case TaxSpace tax:
                int taxAmount = tax.Amount;
                // Hvis skat tillader 10% af formue: vælger den laveste
                if (tax.Percentage.HasValue)
                {
                    int netWorthTax = (int)(player.CalculateTotalNetWorth() * tax.Percentage.Value);
                    taxAmount = Math.Min(tax.Amount, netWorthTax);
                }
                Log($"🏛️ {player.Name} betaler skat: kr. {taxAmount:N0} (lægges i Puljen!)");
                PayMoney(player, taxAmount, null);
                JackpotPool += taxAmount;
                State.Phase = TurnPhase.ActionResolved;
                break;

            case ChanceSpace:
                DrawAndResolveChance(player);
                break;

            case ActionSpace action:
                if (action.Type == SpaceType.GoToJail)
                {
                    Log($"{player.Name} er sendt i Fængsel!");
                    SendToJail(player);
                }
                else if (action.Type == SpaceType.FreeParking)
                {
                    // DEN KLASSISKE HUSREGEL: VIND SKATTE-/PARKERINGSPULJEN!
                    if (JackpotPool > 0)
                    {
                        int winAmount = JackpotPool;
                        player.AddMoney(winAmount);
                        JackpotPool = 1000; // Nulstilles til grundbeløb
                        Log($"💰💰 JACKPOT! {player.Name} landede på Parkering og vandt hele puljen på kr. {winAmount:N0}!");
                    }
                    else
                    {
                        Log($"{player.Name} tager en pause på Parkering. Puljen er tom.");
                    }
                }
                State.Phase = TurnPhase.ActionResolved;
                break;

            default:
                State.Phase = TurnPhase.ActionResolved;
                break;
        }
    }

    private void HandleOwnableLanding(Player player, OwnableSpace ownable, int diceTotal)
    {
        if (ownable.Owner == null)
        {
            if (player.Balance >= ownable.Price)
            {
                // Grunden er ledig og spilleren har råd til at købe den
                State.Phase = TurnPhase.PendingBuyOrPass;
                Log($"{ownable.Name} er ledig! Pris: kr. {ownable.Price:N0}. Vælg om du vil købe.");
            }
            else
            {
                // Spilleren har ikke råd til at købe grunden
                State.Phase = TurnPhase.ActionResolved;
                Log($"{ownable.Name} er ledig (kr. {ownable.Price:N0}), men {player.Name} har ikke råd (saldo: kr. {player.Balance:N0}).");
            }
        }
        else if (ownable.Owner != player)
        {
            // Grunden ejes af en anden -> Opret lejekrav (ejeren skal selv opkræve før næste kast!)
            int rent = ownable.CalculateRent(Board, diceTotal > 0 ? diceTotal : (State.LastRoll.Die1 + State.LastRoll.Die2));
            PendingRentClaim = new RentClaim(ownable.Owner, player, ownable, rent);
            Log($"👀 {player.Name} landede på {ownable.Name}! {ownable.Owner.Name} har ret til kr. {rent:N0} i leje (Husk at opkræve inden næste kast!)");
            State.Phase = TurnPhase.ActionResolved;
        }
        else
        {
            Log($"{player.Name} ejer allerede {ownable.Name}.");
            State.Phase = TurnPhase.ActionResolved;
        }
    }

    public bool BuyProperty(Player player)
    {
        if (State.Phase != TurnPhase.PendingBuyOrPass) return false;

        var space = Board[player.Position];
        if (space is not OwnableSpace ownable || ownable.Owner != null) return false;

        if (player.Balance < ownable.Price)
        {
            Log($"{player.Name} har ikke råd til at købe {ownable.Name} (koster kr. {ownable.Price:N0}, saldo: kr. {player.Balance:N0}).");
            return false;
        }

        player.DeductMoney(ownable.Price);
        ownable.Owner = player;
        player.OwnedProperties.Add(ownable);

        Log($"{player.Name} har købt {ownable.Name} for kr. {ownable.Price:N0}!");
        State.Phase = TurnPhase.ActionResolved;
        return true;
    }

    public void PassBuyProperty()
    {
        if (State.Phase != TurnPhase.PendingBuyOrPass) return;
        Log($"{CurrentPlayer.Name} valgte ikke at købe grunden.");
        State.Phase = TurnPhase.ActionResolved;
    }

    private void DrawAndResolveChance(Player player)
    {
        var card = ChanceDeck.DrawCard();
        Log($"[Prøv Lykken] {card.Text}");

        switch (card.ActionType)
        {
            case ChanceActionType.ReceiveMoney:
                player.AddMoney(card.Amount);
                break;

            case ChanceActionType.PayMoney:
                PayMoney(player, card.Amount, null);
                JackpotPool += card.Amount;
                break;

            case ChanceActionType.MoveToSpace:
                MovePlayerToSpace(player, card.TargetSpaceIndex);
                return;

            case ChanceActionType.MoveSteps:
                MovePlayer(player, card.Amount, canCollectStart: false);
                return;

            case ChanceActionType.GoToJail:
                SendToJail(player);
                break;

            case ChanceActionType.GetOutOfJailCard:
                player.GetOutOfJailCards++;
                Log($"🎉 {player.Name} gemmer frikortet (Antal frikort: {player.GetOutOfJailCards}). Kan bruges ved fængsel eller sælges til en medspiller!");
                break;

            case ChanceActionType.MatadorGrant:
                int netWorth = player.CalculateTotalNetWorth();
                if (netWorth <= 15000)
                {
                    player.AddMoney(card.Amount);
                    Log($"💰 HELDIG! {player.Name}s formue er kr. {netWorth:N0} (<= 15.000) og MODTAGER Matador-legatet på kr. 40.000!");
                }
                else
                {
                    Log($"❌ ÆRGERLIGT! {player.Name}s formue er kr. {netWorth:N0}, hvilket overstiger grænsen på kr. 15.000. Intet legat!");
                }
                break;

            case ChanceActionType.ReceiveFromAllPlayers:
                foreach (var other in Players.Where(p => p != player && !p.IsBankrupt))
                {
                    PayMoney(other, card.Amount, player);
                }
                break;

            case ChanceActionType.PayPerHouseAndHotel:
                int totalCost = 0;
                foreach (var street in player.OwnedProperties.OfType<StreetSpace>())
                {
                    if (street.HasHotel) totalCost += card.HotelCost;
                    else totalCost += street.HouseCount * card.HouseCost;
                }
                if (totalCost > 0)
                {
                    Log($"{player.Name} betaler i alt kr. {totalCost:N0} for vedligeholdelse af ejendomme.");
                    PayMoney(player, totalCost, null);
                }
                break;
        }

        State.Phase = TurnPhase.ActionResolved;
    }

    public void SendToJail(Player player)
    {
        player.Position = 10;
        player.IsInJail = true;
        player.TurnsInJail = 0;
        State.ConsecutiveDoubles = 0;
    }

    public bool BuyHouse(Player player, StreetSpace street)
    {
        if (street.Owner != player) return false;
        if (!Board.PlayerOwnsAllInGroup(player, street.Group)) return false;
        if (street.HouseCount >= 5) return false; // Maks hotel

        // Reglen om jævn bebyggelse: må ikke have mere end 1 hus over andre i gruppen
        var groupStreets = Board.Spaces.OfType<StreetSpace>().Where(s => s.Group == street.Group).ToList();
        int minHousesInGroup = groupStreets.Min(s => s.HouseCount);
        if (street.HouseCount > minHousesInGroup) return false;

        if (player.Balance < street.HousePrice) return false;

        player.DeductMoney(street.HousePrice);
        street.HouseCount++;

        string unit = street.HouseCount == 5 ? "Hotel" : $"{street.HouseCount}. hus";
        Log($"{player.Name} byggede et {unit} på {street.Name} for kr. {street.HousePrice:N0}.");
        return true;
    }

    public bool MortgageProperty(Player player, OwnableSpace property)
    {
        if (property.Owner != player || property.IsMortgaged) return false;

        if (property is StreetSpace street && street.HouseCount > 0)
        {
            return false; // Skal sælge huse først
        }

        property.IsMortgaged = true;
        player.AddMoney(property.MortgageValue);
        Log($"{player.Name} har pantsat {property.Name} og modtaget kr. {property.MortgageValue:N0}.");
        return true;
    }

    public bool UnmortgageProperty(Player player, OwnableSpace property)
    {
        if (property.Owner != player || !property.IsMortgaged) return false;

        int cost = (int)(property.MortgageValue * 1.10); // 10% rente
        if (player.Balance < cost) return false;

        player.DeductMoney(cost);
        property.IsMortgaged = false;
        Log($"{player.Name} har indløst pantsætningen på {property.Name} for kr. {cost:N0}.");
        return true;
    }

    public void PayMoney(Player debtor, int amount, Player? creditor)
    {
        if (debtor.Balance >= amount)
        {
            debtor.DeductMoney(amount);
            creditor?.AddMoney(amount);
            return;
        }

        // Ikke nok likvide midler - tjek om pantsætning kan dække
        int deficit = amount - debtor.Balance;
        Log($"[Advarsel] {debtor.Name} mangler kr. {deficit:N0}!");

        // Prøv automatisk at pantsætte ubehæftede ejendomme for at redde spilleren
        foreach (var prop in debtor.OwnedProperties.Where(p => !p.IsMortgaged).ToList())
        {
            if (prop is StreetSpace st && st.HouseCount > 0) continue;
            MortgageProperty(debtor, prop);
            if (debtor.Balance >= amount) break;
        }

        if (debtor.Balance >= amount)
        {
            debtor.DeductMoney(amount);
            creditor?.AddMoney(amount);
            return;
        }

        // Spilleren går bankerot!
        HandleBankruptcy(debtor, creditor);
    }

    private void HandleBankruptcy(Player bankrupt, Player? creditor)
    {
        bankrupt.IsBankrupt = true;
        Log($"🚨 {bankrupt.Name} er gået BANKEROT!");

        if (creditor != null)
        {
            creditor.AddMoney(Math.Max(0, bankrupt.Balance));
            foreach (var prop in bankrupt.OwnedProperties)
            {
                prop.Owner = creditor;
                creditor.OwnedProperties.Add(prop);
            }
            creditor.GetOutOfJailCards += bankrupt.GetOutOfJailCards;
            Log($"Alle værdier tilhørende {bankrupt.Name} er overdraget til {creditor.Name}.");
        }
        else
        {
            // Banken tager ejendommene (bliver sat fri)
            foreach (var prop in bankrupt.OwnedProperties)
            {
                prop.Owner = null;
                prop.IsMortgaged = false;
                if (prop is StreetSpace street) street.HouseCount = 0;
            }
        }

        bankrupt.OwnedProperties.Clear();
    }

    public void EndTurn()
    {
        if (State.CanRollAgain && !CurrentPlayer.IsBankrupt && !CurrentPlayer.IsInJail)
        {
            Log($"{CurrentPlayer.Name} slog to ens og har en ekstra tur!");
            State.Phase = TurnPhase.WaitingForRoll;
            return;
        }

        State.ConsecutiveDoubles = 0;
        State.Phase = TurnPhase.WaitingForRoll;

        // Næste ikke-bankerotte spiller
        do
        {
            State.CurrentPlayerIndex = (State.CurrentPlayerIndex + 1) % Players.Count;
        } while (CurrentPlayer.IsBankrupt && !IsGameOver);

        Log($"--- Næste tur: {CurrentPlayer.Name} (Saldo: kr. {CurrentPlayer.Balance:N0}) ---");
    }

    public void PlayerForfeit(Player player)
    {
        if (player.IsBankrupt) return;

        player.IsBankrupt = true;
        Log($"🏳️ {player.Name} har forladt spillet og opgivet!");

        // Frigiv spillerens ejendomme
        foreach (var prop in player.OwnedProperties)
        {
            prop.Owner = null;
            prop.IsMortgaged = false;
            if (prop is StreetSpace street) street.HouseCount = 0;
        }
        player.OwnedProperties.Clear();

        var activePlayers = Players.Where(p => !p.IsBankrupt).ToList();

        if (activePlayers.Count <= 1)
        {
            // Kun 1 tilbage -> automatisk vinder
            if (Winner != null)
            {
                Log($"🏆 SPILLET ER SLUT! {Winner.Name} har vundet spillet!");
            }
        }
        else
        {
            // Flere spillere tilbage -> Start afstemning blandt de resterende!
            ActiveVoteSession = new ContinueVoteSession(player.Name, activePlayers.Select(p => p.Id).ToList());
            Log($"🗳️ AFSTEMNING: {player.Name} forlod spillet. De resterende {activePlayers.Count} spillere skal stemme om spillet skal fortsætte eller stoppe!");
        }
    }

    public bool VoteOnContinue(string playerId, bool continueGame)
    {
        if (ActiveVoteSession == null) return false;

        ActiveVoteSession.RegisterVote(playerId, continueGame);
        var player = Players.FirstOrDefault(p => p.Id == playerId);
        string choice = continueGame ? "Fortsætte" : "Stoppe";
        Log($"🗳️ {player?.Name ?? playerId} stemte for at {choice} spillet.");

        if (ActiveVoteSession.IsComplete)
        {
            bool shouldContinue = ActiveVoteSession.ShouldContinue();
            if (shouldContinue)
            {
                Log("✅ FLERTALLET HAR TALT: Spillet fortsætter!");
                ActiveVoteSession = null;

                if (CurrentPlayer.IsBankrupt)
                {
                    EndTurn();
                }
            }
            else
            {
                Log("🛑 FLERTALLET HAR TALT: Spillet stoppes!");
                // Find spilleren med højest nettoformue som vinder
                var topPlayer = Players.Where(p => !p.IsBankrupt).OrderByDescending(p => p.CalculateTotalNetWorth()).FirstOrDefault();
                foreach (var p in Players.Where(p => p != topPlayer))
                {
                    p.IsBankrupt = true;
                }
                ActiveVoteSession = null;
                Log($"🏆 Vinderen efter flertalsbeslutning er: {topPlayer?.Name} med formue på kr. {topPlayer?.CalculateTotalNetWorth():N0}!");
            }
        }

        return true;
    }
}
