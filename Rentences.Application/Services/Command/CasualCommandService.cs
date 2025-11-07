public class CasualCommandService : ICommandService
{
    private readonly IGameService _gameService;
    private readonly IInterop _interop;

    public CasualCommandService(IGameService gameService, IInterop interop)
    {
        _gameService = gameService;
        _interop = interop;
    }

    public string CommandName => "-casual";

    public async Task ProcessCommandAsync(string[] args, SocketMessage message)
    {
        var socketGuildUser = message.Author as SocketGuildUser;
        if (socketGuildUser == null)
        {
            await message.Channel.SendMessageAsync("This command can only be used in a server.", allowedMentions: AllowedMentions.None);
            return;
        }

        // Start the casual game
        await _gameService.StartGame(Gamemodes.GAMEMODE_CASUAL);
        
        var successEmbed = new EmbedBuilder()
            .WithTitle("🎮 Game Started")
            .WithDescription("Casual game has been started! Everyone is welcome to join in the fun.")
            .WithColor(Color.Green)
            .Build();
            
        await message.Channel.SendMessageAsync(embed: successEmbed, allowedMentions: AllowedMentions.None);
    }
}