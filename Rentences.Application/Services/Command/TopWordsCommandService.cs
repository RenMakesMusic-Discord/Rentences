using Discord;
using Discord.WebSocket;
// ReSharper disable once CheckNamespace
using Rentences.Application;
using Rentences.Persistence.Repositories;
using System.Linq;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace

public class TopWordsCommandService : ICommandService
{
    private readonly IInterop _interop;
    private readonly IWordRepository _wordRepository;

    public TopWordsCommandService(IWordRepository wordRepository)
    {
        _wordRepository = wordRepository;
    }

    public string CommandName => "-topwords";

    public async Task ProcessCommandAsync(string[] args, SocketMessage message)
    {
        
        var user = message.MentionedUsers.Any() ? message.MentionedUsers.First() : message.Author;
        var topWords = _wordRepository.GetTopWordsByUser(user.Id, 10).ToList();

        var topWordmessage = "";
        for (int i = 0; i < topWords.Count; i++)
        {
            var word = topWords[i];
            var rating = i + 1;
            topWordmessage += $"{rating}. {word.Value}\n";
        }

        var embed = new EmbedBuilder()
            .WithTitle($"Top 10 Words by {user.Username}")
            .WithDescription(topWordmessage)
            .WithColor(Color.Green)
            .Build();

        await message.Channel.SendMessageAsync(embed: embed, allowedMentions: AllowedMentions.None);
    }
}
