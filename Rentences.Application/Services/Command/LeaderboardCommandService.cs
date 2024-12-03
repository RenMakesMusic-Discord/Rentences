using Discord;
using Discord.WebSocket;
using Rentences.Application;
using System.Threading.Tasks;

public class LeaderboardCommandService : ICommandService
{
    private readonly IInterop _interop;

    public LeaderboardCommandService(IInterop interop)
    {
        _interop = interop;
    }

    public string CommandName => "-stats";

    public async Task ProcessCommandAsync(string[] args, SocketMessage message)
    {
        var leaderboard = await _interop.GetLeaderboard();

        var embed = new EmbedBuilder()
            .WithTitle("Leaderboard")
            .WithDescription(leaderboard)
            .WithColor(Color.Blue)
            .Build();

        await message.Thread.SendMessageAsync(embed: embed, allowedMentions: AllowedMentions.None);
    }
}
