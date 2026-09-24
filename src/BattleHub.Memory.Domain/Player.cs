namespace BattleHub.Memory.Domain;

public class Player
{
    public string UserId { get; }
    public string DisplayName { get; }
    public int MatchedPairs { get; private set; }

    public Player(string userId, string displayName)
    {
        UserId = userId;
        DisplayName = displayName;
    }

    public void AddMatchedPair()
    {
        MatchedPairs++;
    }
}
