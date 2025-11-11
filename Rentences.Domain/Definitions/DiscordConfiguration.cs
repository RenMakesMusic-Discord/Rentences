
namespace Rentences.Domain.Definitions;
public class DiscordConfiguration
{
    public string Token { get; set; }
    public string ServerId { get; set; }
    public string ChannelId { get; set; } = "1104584926520692836";
    public string Status { get; set; }
    public string StaffRoleId { get; set; }
    public Emote WinEmoji { get; set; }
    public Emote LoseEmoji { get; set; }
    public Emote CorrectEmoji { get; set; }
}

public class Emote
{
    public string Contents { get; set; }
    public bool IsEmoji { get; set; }
}
 