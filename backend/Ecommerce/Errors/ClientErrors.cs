namespace Ecommerce.Errors;

public static class ClientErrors
{
    public static readonly Error ClientNotFound = new("Client.NotFound", "No client was found with the given ID");
    public static readonly Error EmailAlreadyExists = new("Client.EmailAlreadyExists", "Another account already uses this email address");
    public static readonly Error UpdateFailed = new("Client.UpdateFailed", "The client account could not be updated");
}
