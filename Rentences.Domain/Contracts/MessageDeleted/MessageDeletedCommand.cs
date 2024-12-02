
using Discord;

namespace Rentences.Domain.Contracts; 
public record struct MessageDeletedCommand : IRequest<MessageDeletedResponse> {
    public ulong messageId;
}

public class MessageDeletedCommandValidator : AbstractValidator<MessageDeletedCommand> {
    public MessageDeletedCommandValidator() {
    }
    
}

