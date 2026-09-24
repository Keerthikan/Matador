using Matador.Core.Domain;
using Matador.Core.Engine;

namespace Matador.Web.Rooms;

public class PlayerSession
{
    public string PlayerId { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; }
    public bool IsHost { get; set; }

    public PlayerSession(string name, bool isHost = false)
    {
        Name = name;
        IsHost = isHost;
    }
}

public class GameRoom
{
    public string Code { get; }
    public CityTheme City { get; set; } = CityTheme.Copenhagen;
    public List<PlayerSession> Sessions { get; } = new();
    public GameEngine? Engine { get; private set; }
    public bool IsStarted => Engine != null;
    public List<string> Logs { get; } = new();

    public GameRoom(string code, CityTheme city = CityTheme.Copenhagen)
    {
        Code = code;
        City = city;
    }

    public void StartGame()
    {
        if (Sessions.Count < 2)
        {
            throw new InvalidOperationException("Der skal være mindst 2 spillere for at starte spillet.");
        }

        var players = Sessions.Select(s => new Player(s.PlayerId, s.Name, 30000)).ToList();
        var board = BoardFactory.CreateBoard(City);
        Engine = new GameEngine(players, board: board);
        Logs.Add($"Spillet '{Code}' ({City} udgave) er startet med {players.Count} spillere!");

        Engine.OnLog += msg =>
        {
            Logs.Add(msg);
            if (Logs.Count > 100) Logs.RemoveAt(0);
        };
    }
}

public class RoomManager
{
    private readonly Dictionary<string, GameRoom> _rooms = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();

    public GameRoom CreateRoom(string hostName, CityTheme city, out PlayerSession hostSession)
    {
        string code = GenerateRoomCode();
        var room = new GameRoom(code, city);
        hostSession = new PlayerSession(hostName, isHost: true);
        room.Sessions.Add(hostSession);
        _rooms[code] = room;
        return room;
    }

    public GameRoom? GetRoom(string code)
    {
        _rooms.TryGetValue(code, out var room);
        return room;
    }

    public PlayerSession? JoinRoom(string code, string playerName, out GameRoom? room)
    {
        room = GetRoom(code);
        if (room == null || room.IsStarted || room.Sessions.Count >= 6)
        {
            return null;
        }

        var session = new PlayerSession(playerName);
        room.Sessions.Add(session);
        return session;
    }

    public bool DeleteRoom(string code, string hostToken)
    {
        if (_rooms.TryGetValue(code, out var room))
        {
            var hostSession = room.Sessions.FirstOrDefault(s => s.IsHost);
            if (hostSession != null && hostSession.Token == hostToken)
            {
                _rooms.Remove(code);
                return true;
            }
        }
        return false;
    }

    private string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        string code;
        do
        {
            code = new string(Enumerable.Repeat(chars, 4).Select(s => s[_random.Next(s.Length)]).ToArray());
        } while (_rooms.ContainsKey(code));
        return code;
    }
}
