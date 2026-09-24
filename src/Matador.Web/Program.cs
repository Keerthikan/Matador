using Matador.Core.Domain;
using Matador.Core.Domain.Chance;
using Matador.Core.Engine;
using Matador.Web.Rooms;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
});

builder.Services.AddSingleton<RoomManager>();

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

// ==========================================
// RUM & MULTIPLAYER LOBBY ENDPOINTS
// ==========================================

app.MapPost("/api/rooms/create", (CreateRoomRequest req, RoomManager manager) =>
{
    if (string.IsNullOrWhiteSpace(req.PlayerName)) return Results.BadRequest("Angiv et navn.");

    var room = manager.CreateRoom(req.PlayerName.Trim(), req.City, out var hostSession);
    return Results.Ok(new
    {
        roomCode = room.Code,
        playerId = hostSession.PlayerId,
        token = hostSession.Token,
        isHost = true,
        city = room.City.ToString()
    });
});

app.MapPost("/api/rooms/join", (JoinRoomRequest req, RoomManager manager) =>
{
    if (string.IsNullOrWhiteSpace(req.PlayerName) || string.IsNullOrWhiteSpace(req.RoomCode))
    {
        return Results.BadRequest("Angiv både rumkode og dit spillernavn.");
    }

    var session = manager.JoinRoom(req.RoomCode.Trim().ToUpper(), req.PlayerName.Trim(), out var room);
    if (session == null || room == null)
    {
        return Results.BadRequest("Rummet findes ikke, er allerede startet, eller er fuldt.");
    }

    return Results.Ok(new
    {
        roomCode = room.Code,
        playerId = session.PlayerId,
        token = session.Token,
        isHost = session.IsHost
    });
});

app.MapPost("/api/rooms/{code}/close", (string code, TokenRequest req, RoomManager manager) =>
{
    bool deleted = manager.DeleteRoom(code, req.Token);
    if (deleted)
    {
        return Results.Ok();
    }
    return Results.BadRequest("Kunne ikke lukke rummet (ugyldig vært eller kode).");
});

app.MapPost("/api/rooms/{code}/start", (string code, TokenRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null || !session.IsHost)
    {
        return Results.BadRequest("Kun værten kan starte spillet.");
    }

    try
    {
        room.StartGame();
        return Results.Ok();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ex.Message);
    }
});

// ==========================================
// SPILLOGIK (Med asymmetrisk synlighed)
// ==========================================

app.MapGet("/api/rooms/{code}/state", (string code, string? token, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == token);

    // Hvis spillet ikke er startet endnu -> returner lobby-info
    if (!room.IsStarted || room.Engine == null)
    {
        return Results.Ok(new
        {
            RoomCode = room.Code,
            IsStarted = false,
            MyPlayerId = session?.PlayerId,
            IsHost = session?.IsHost ?? false,
            LobbyPlayers = room.Sessions.Select(s => new { s.PlayerId, s.Name, s.IsHost }).ToList()
        });
    }

    var game = room.Engine;
    var myPlayer = session != null ? game.Players.FirstOrDefault(p => p.Id == session.PlayerId) : null;
    bool isMyTurn = myPlayer != null && game.CurrentPlayer == myPlayer;

    var spacesDto = game.Board.Spaces.Select(s => new
    {
        s.Index,
        s.Name,
        Type = s.Type.ToString(),
        Price = s is OwnableSpace own ? own.Price : 0,
        MortgageValue = s is OwnableSpace ownM ? ownM.MortgageValue : 0,
        IsMortgaged = s is OwnableSpace ownMort ? ownMort.IsMortgaged : false,
        OwnerName = s is OwnableSpace ownO && ownO.Owner != null ? ownO.Owner.Name : null,
        OwnerId = s is OwnableSpace ownO2 && ownO2.Owner != null ? ownO2.Owner.Id : null,
        Group = s is StreetSpace st ? st.Group.ToString() : (s is ShippingSpace ? "Shipping" : (s is BrewerySpace ? "Brewery" : null)),
        HouseCount = s is StreetSpace st2 ? st2.HouseCount : 0,
        HousePrice = s is StreetSpace st3 ? st3.HousePrice : 0,
        BaseRent = s is StreetSpace st4 ? st4.BaseRent : 0,
        CurrentRent = s is OwnableSpace ownR ? ownR.CalculateRent(game.Board, game.State.LastRoll.Die1 + game.State.LastRoll.Die2) : 0
    });

    var playersDto = game.Players.Select(p => new
    {
        p.Id,
        p.Name,
        p.Balance,
        p.Position,
        p.IsInJail,
        p.TurnsInJail,
        p.GetOutOfJailCards,
        p.IsBankrupt,
        NetWorth = p.CalculateTotalNetWorth(),
        OwnedProperties = p.OwnedProperties.Select(op => new { op.Index, op.Name, op.Price }).ToList(),
        OwnedCount = p.OwnedProperties.Count
    });

    // ASYMMETRISK LEJE-OPKRÆVNING:
    // Kun ejeren (kreditor) må modtage information og alarm om at opkræve leje!
    object? pendingRentDto = null;
    if (game.PendingRentClaim != null && session != null)
    {
        bool iAmCreditor = game.PendingRentClaim.Creditor.Id == session.PlayerId;
        if (iAmCreditor)
        {
            pendingRentDto = new
            {
                IAmCreditor = true,
                DebtorName = game.PendingRentClaim.Debtor.Name,
                PropertyName = game.PendingRentClaim.Property.Name,
                Amount = game.PendingRentClaim.Amount
            };
        }
    }

    // Handelsforespørgsler rettet til mig
    var myPendingTrades = game.TradeOffers.Where(t => t.Status == TradeStatus.Pending && session != null && t.ToPlayer.Id == session.PlayerId).Select(t => new
    {
        t.Id,
        FromPlayer = t.FromPlayer.Name,
        FromPlayerId = t.FromPlayer.Id,
        PropertyName = t.IsJailCardTrade ? "Frikort (Kom ud af Fængsel)" : t.Property?.Name,
        PropertyIndex = t.Property?.Index ?? -1,
        IsJailCardTrade = t.IsJailCardTrade,
        t.Price
    }).ToList();

    // Aktiv afstemning hvis en spiller har forladt spillet
    object? activeVoteDto = null;
    if (game.ActiveVoteSession != null && session != null)
    {
        activeVoteDto = new
        {
            game.ActiveVoteSession.LeavingPlayerName,
            HasVoted = game.ActiveVoteSession.HasVoted(session.PlayerId),
            TotalEligible = game.ActiveVoteSession.EligiblePlayerIds.Count,
            VotesCount = game.ActiveVoteSession.Votes.Count
        };
    }

    return Results.Ok(new
    {
        RoomCode = room.Code,
        City = room.City.ToString(),
        IsStarted = true,
        MyPlayerId = session?.PlayerId,
        IsMyTurn = isMyTurn,
        Phase = game.State.Phase.ToString(),
        CurrentPlayerIndex = game.State.CurrentPlayerIndex,
        CurrentPlayer = new
        {
            game.CurrentPlayer.Id,
            game.CurrentPlayer.Name,
            game.CurrentPlayer.Balance,
            game.CurrentPlayer.Position,
            game.CurrentPlayer.IsInJail
        },
        Die1 = game.State.LastRoll.Die1,
        Die2 = game.State.LastRoll.Die2,
        CanRollAgain = game.State.CanRollAgain,
        JackpotPool = game.JackpotPool,
        IsGameOver = game.IsGameOver,
        Winner = game.Winner?.Name,
        ActiveVote = activeVoteDto,
        PendingRent = pendingRentDto,
        PendingTrades = myPendingTrades,
        Players = playersDto,
        Spaces = spacesDto,
        Logs = room.Logs.TakeLast(30).ToList()
    });
});

app.MapPost("/api/rooms/{code}/vote", (string code, VoteRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null) return Results.BadRequest();

    bool success = room.Engine.VoteOnContinue(session.PlayerId, req.ContinueGame);
    return Results.Ok(new { success });
});

app.MapPost("/api/rooms/{code}/roll", (string code, TokenRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null || room.Engine.CurrentPlayer.Id != session.PlayerId)
    {
        return Results.BadRequest("Det er ikke din tur til at slå.");
    }

    if (room.Engine.State.Phase == TurnPhase.WaitingForRoll)
    {
        room.Engine.RollDiceAndMove();
    }
    return Results.Ok();
});

app.MapPost("/api/rooms/{code}/buy", (string code, TokenRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null || room.Engine.CurrentPlayer.Id != session.PlayerId)
    {
        return Results.BadRequest("Det er ikke din tur.");
    }

    if (room.Engine.State.Phase == TurnPhase.PendingBuyOrPass)
    {
        room.Engine.BuyProperty(room.Engine.CurrentPlayer);
    }
    return Results.Ok();
});

app.MapPost("/api/rooms/{code}/pass", (string code, TokenRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null || room.Engine.CurrentPlayer.Id != session.PlayerId)
    {
        return Results.BadRequest("Det er ikke din tur.");
    }

    if (room.Engine.State.Phase == TurnPhase.PendingBuyOrPass)
    {
        room.Engine.PassBuyProperty();
    }
    return Results.Ok();
});

app.MapPost("/api/rooms/{code}/claimrent", (string code, TokenRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null || room.Engine.PendingRentClaim == null || room.Engine.PendingRentClaim.Creditor.Id != session.PlayerId)
    {
        return Results.BadRequest("Du har intet krav at opkræve.");
    }

    bool success = room.Engine.ClaimRent();
    return Results.Ok(new { success });
});

app.MapPost("/api/rooms/{code}/leave", (string code, TokenRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.Ok();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session != null)
    {
        var player = room.Engine.Players.FirstOrDefault(p => p.Id == session.PlayerId);
        if (player != null)
        {
            room.Engine.PlayerForfeit(player);
        }
    }
    return Results.Ok();
});

app.MapPost("/api/rooms/{code}/endturn", (string code, TokenRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null || room.Engine.CurrentPlayer.Id != session.PlayerId)
    {
        return Results.BadRequest("Det er ikke din tur.");
    }

    if (room.Engine.State.Phase == TurnPhase.ActionResolved)
    {
        room.Engine.EndTurn();
    }
    return Results.Ok();
});

app.MapPost("/api/rooms/{code}/payjail", (string code, TokenRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null || room.Engine.CurrentPlayer.Id != session.PlayerId) return Results.BadRequest();

    room.Engine.PayOutOfJail(room.Engine.CurrentPlayer);
    return Results.Ok();
});

app.MapPost("/api/rooms/{code}/trade/propose", (string code, RoomTradeRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null) return Results.BadRequest();

    var from = room.Engine.Players.FirstOrDefault(p => p.Id == session.PlayerId);
    var to = room.Engine.Players.FirstOrDefault(p => p.Id == req.ToPlayerId);

    if (from != null && to != null)
    {
        if (req.IsJailCard)
        {
            var offer = room.Engine.ProposeJailCardTrade(from, to, req.Price);
            return Results.Ok(new { success = true, offerId = offer.Id });
        }

        var prop = room.Engine.Board[req.PropertyIndex] as OwnableSpace;
        if (prop != null)
        {
            var offer = room.Engine.ProposeTrade(from, to, prop, req.Price);
            return Results.Ok(new { success = true, offerId = offer.Id });
        }
    }
    return Results.BadRequest(new { error = "Ugyldige parametre til handel." });
});

app.MapPost("/api/rooms/{code}/trade/respond", (string code, RoomTradeRespondRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null) return Results.BadRequest();

    bool success = room.Engine.RespondToTrade(req.TradeId, req.Accept);
    return Results.Ok(new { success });
});

app.MapPost("/api/rooms/{code}/buyhouse/{spaceIndex:int}", (string code, int spaceIndex, TokenRequest req, RoomManager manager) =>
{
    var room = manager.GetRoom(code);
    if (room?.Engine == null) return Results.NotFound();

    var session = room.Sessions.FirstOrDefault(s => s.Token == req.Token);
    if (session == null) return Results.BadRequest();

    var player = room.Engine.Players.FirstOrDefault(p => p.Id == session.PlayerId);
    var space = room.Engine.Board[spaceIndex];
    if (player != null && space is StreetSpace street)
    {
        bool success = room.Engine.BuyHouse(player, street);
        return Results.Ok(new { success });
    }
    return Results.BadRequest();
});

app.Run();

public record CreateRoomRequest(string PlayerName, CityTheme City = CityTheme.Copenhagen);
public record JoinRoomRequest(string RoomCode, string PlayerName);
public record TokenRequest(string Token);
public record VoteRequest(string Token, bool ContinueGame);
public record RoomTradeRequest(string Token, string ToPlayerId, int PropertyIndex, int Price, bool IsJailCard = false);
public record RoomTradeRespondRequest(string Token, int TradeId, bool Accept);
