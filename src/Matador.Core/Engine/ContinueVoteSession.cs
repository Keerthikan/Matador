namespace Matador.Core.Engine;

public class ContinueVoteSession
{
    public string LeavingPlayerName { get; }
    public Dictionary<string, bool> Votes { get; } = new(); // PlayerId -> Continue (true) or End (false)
    public List<string> EligiblePlayerIds { get; }

    public ContinueVoteSession(string leavingPlayerName, List<string> eligiblePlayerIds)
    {
        LeavingPlayerName = leavingPlayerName;
        EligiblePlayerIds = eligiblePlayerIds;
    }

    public bool HasVoted(string playerId) => Votes.ContainsKey(playerId);

    public void RegisterVote(string playerId, bool continueGame)
    {
        if (EligiblePlayerIds.Contains(playerId))
        {
            Votes[playerId] = continueGame;
        }
    }

    public bool IsComplete => EligiblePlayerIds.All(id => Votes.ContainsKey(id));

    public bool ShouldContinue()
    {
        int continueVotes = Votes.Values.Count(v => v);
        int endVotes = Votes.Values.Count(v => !v);
        // Flertallet bestemmer. Ved lige stemmer fortsættes spillet
        return continueVotes >= endVotes;
    }
}
