using Discord;
using Discord.WebSocket;
using Rentences.Application.Services.Game;
using Rentences.Domain.Definitions;
using Rentences.Application;
using System;
using System.Threading.Tasks;
using Rentences.Application.Services;

public class StaffReverseCommandService : ICommandService
{
    private readonly IGameService _gameService;
    private readonly IInterop _interop;

    public StaffReverseCommandService(IGameService gameService, IInterop interop)
    {
        _gameService = gameService;
        _interop = interop;
    }

    public string CommandName => "-reverse";

    public async Task ProcessCommandAsync(string[] args, SocketMessage message)
    {
        var socketGuildUser = message.Author as SocketGuildUser;
        if (socketGuildUser == null)
        {
            await message.Channel.SendMessageAsync("This command can only be used in a server.", allowedMentions: AllowedMentions.None);
            return;
        }

        var config = _interop.GetDiscordConfiguration();
        if (ulong.TryParse(config.StaffRoleId, out var staffRoleId))
        {
            var hasStaffRole = socketGuildUser.Roles.Any(role => role.Id == staffRoleId);
            
            if (!hasStaffRole)
            {
                var embed = new EmbedBuilder()
                    .WithTitle("❌ Access Denied")
                    .WithDescription("This command is restricted to staff members only.")
                    .WithColor(Color.Red)
                    .Build();
                
                await message.Channel.SendMessageAsync(embed: embed, allowedMentions: AllowedMentions.None);
                return;
            }

            // User has staff role, start the reverse sentence game
            await _gameService.StartGame(Gamemodes.GAMEMODE_REVERSE_SENTENCE);
            
            var successEmbed = new EmbedBuilder()
                .WithTitle("🎮 Game Started")
                .WithDescription("Reverse sentence game has been started by staff!")
                .WithColor(Color.Green)
                .Build();
                
            await message.Channel.SendMessageAsync(embed: successEmbed, allowedMentions: AllowedMentions.None);
        }
        else
        {
            await message.Channel.SendMessageAsync("Staff role ID is not properly configured.", allowedMentions: AllowedMentions.None);
        }
    }
}