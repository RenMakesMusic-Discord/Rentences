using Discord;
using Discord.WebSocket;
using Rentences.Application.Services.Game;
using Rentences.Domain.Definitions;
using Rentences.Application;
using System;
using System.Threading.Tasks;
using Rentences.Application.Services;

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

        try
        {
            // Force terminate any existing game and start the casual game
            await _gameService.StartGameWithForceTerminationAsync(Gamemodes.GAMEMODE_CASUAL, "Casual game command");
            
            var successEmbed = new EmbedBuilder()
                .WithTitle("🎮 Game Started")
                .WithDescription("Casual game has been started! Any existing game has been terminated and this new game is now active.")
                .WithColor(Color.Green)
                .Build();
                
            await message.Channel.SendMessageAsync(embed: successEmbed, allowedMentions: AllowedMentions.None);
        }
        catch (Exception ex)
        {
            var errorEmbed = new EmbedBuilder()
                .WithTitle("❌ Game Error")
                .WithDescription($"Failed to start casual game: {ex.Message}")
                .WithColor(Color.Red)
                .Build();
                
            await message.Channel.SendMessageAsync(embed: errorEmbed, allowedMentions: AllowedMentions.None);
        }
    }
}