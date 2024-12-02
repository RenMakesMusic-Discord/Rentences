
namespace Rentences.Domain;

public static class CustomErrorValues
{
    public static CustomError WordValidationTitle = new() { Title = "Word.Validation", Description = "FluentValidation checks failed." };

}
public struct CustomError
{
    public string Title { get; set; }
    public string Description { get; set; }
}
