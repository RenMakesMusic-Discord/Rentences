namespace Rentences.Domain.Definitions; 
public class Message {
    public required string Text;
    public required string Author;
}
public class MessageValidator : AbstractValidator<Message> {
    public MessageValidator() {
        
    }
}

