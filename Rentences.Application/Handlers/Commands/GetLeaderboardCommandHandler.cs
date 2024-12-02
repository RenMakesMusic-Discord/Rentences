using MediatR;
using Microsoft.EntityFrameworkCore;
using Rentences.Persistence;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public class GetLeaderboardCommandHandler : IRequestHandler<GetLeaderboardCommand, string>
{
    private readonly AppDbContext _dbContext;
    private readonly IWordRepository _wordRepository;

    public GetLeaderboardCommandHandler(AppDbContext dbContext, IWordRepository wordRepo)
    {
        _dbContext = dbContext;
        _wordRepository = wordRepo;
    }

    public async Task<string> Handle(GetLeaderboardCommand request, CancellationToken cancellationToken)
    {
        var topUsers = await _dbContext.UserStatistics
            .OrderByDescending(u => u.TotalWordsAdded)
            .Take(10)
            .ToListAsync(cancellationToken);



        var sb = new StringBuilder();
        sb.AppendLine("🏆 **Top 10 Users by Words Contributed** 🏆");
        for (int i = 0; i < topUsers.Count; i++)
        {
            var user = topUsers[i];

            var topWord = _wordRepository.GetTopWordsByUser(user.UserId, 1).FirstOrDefault();
            if(topWord == null)
                sb.AppendLine($"{i + 1}. <@{user.UserId}> - {user.TotalWordsAdded} words");
            else
                sb.AppendLine($"{i + 1}. <@{user.UserId}> - {user.TotalWordsAdded} words | Top Word ({topWord.Value})");
        }

        return sb.ToString();
    }
}
