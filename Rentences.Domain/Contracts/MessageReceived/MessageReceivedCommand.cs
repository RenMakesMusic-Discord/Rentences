
namespace Rentences.Domain.Contracts; 
public record struct MessageReceivedCommand : IRequest<MessageReceivedResponse> {
    public SocketMessage message;
}

public class MessageReceivedCommandValidator : AbstractValidator<MessageReceivedCommand> {
    public MessageReceivedCommandValidator() {
        RuleFor(m => m.message).Must(isChatMessage);
    }
    public bool isChatMessage(SocketMessage message) {
        return !(message is not SocketUserMessage);
    }
}

