namespace Matador.Core.Engine;

public enum TurnPhase
{
    WaitingForRoll,
    PendingBuyOrPass,
    ActionResolved,
    TurnEnded
}

public class GameState
{
    public TurnPhase Phase { get; set; } = TurnPhase.WaitingForRoll;
    public int CurrentPlayerIndex { get; set; }
    public (int Die1, int Die2) LastRoll { get; set; }
    public int ConsecutiveDoubles { get; set; }
    public bool CanRollAgain => LastRoll.Die1 == LastRoll.Die2 && ConsecutiveDoubles > 0 && ConsecutiveDoubles < 3;
    public string LastEventMessage { get; set; } = string.Empty;
}
