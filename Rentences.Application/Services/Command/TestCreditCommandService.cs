using Discord;
using Discord.WebSocket;
using Rentences.Application;
using System.Threading.Tasks;

public class TestCreditCommandService : ICommandService
{
    private readonly IInterop _interop;

    public TestCreditCommandService(IInterop interop)
    {
        _interop = interop;
    }

    public string CommandName => "-testcredits";

    public async Task ProcessCommandAsync(string[] args, SocketMessage message)
    {
        // List of user IDs without duplicates
        List<string> uniqueUserIds = new List<string>
        {
            "1090696445142778017",
            "1056128084811722852",
            "1138013423058296892",
            "442355743015043073",
            "749061126830161961",
            "948819617697112064",
            "555781206482878465",
            "165651528772747265",
            "712464157546381352",
            "332550207919357956",
            "348174104769200139",
            "1094720853951909888",
            "879217765439901737",
            "159758585428049922",
            "1087504659498938378",
            "406181581766656000",
            "630438167928897566",
            "722164590162739251",
            "781980996463230996"
        };

        // Sending gratitude message with Discord tags
        var gratitudeEmbed = new EmbedBuilder()
            .WithTitle("Testing credits")
            .WithDescription("Thank you to the following users for their contributions:\n" +
                string.Join("\n", uniqueUserIds.Select(id => $"<@{id}>")))
            .WithColor(Color.Green)
            .WithFooter("💗 This new version of Rentences wasn't possible without your testing & suggestions! 💗")
            .Build();

        await message.Thread.SendMessageAsync(embed: gratitudeEmbed, allowedMentions: AllowedMentions.None);
    }
}
