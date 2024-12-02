namespace Rentences.Domain;
public struct GameState
{
    public Guid GameId { get; set; }
    public GameStatus CurrentState { get; set; }
}
