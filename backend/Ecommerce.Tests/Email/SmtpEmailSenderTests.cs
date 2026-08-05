using Ecommerce.Email;
using Microsoft.Extensions.Options;

namespace Ecommerce.Tests.Email;

public class SmtpEmailSenderTests
{
    private static SmtpEmailSender CreateSender() => new(Microsoft.Extensions.Options.Options.Create(new SmtpOptions
    {
        Host = "sandbox.smtp.mailtrap.io",
        Port = 2525,
        Username = "test-user",
        Password = "test-pass",
        FromEmail = "no-reply@shopdemo.local",
        FromName = "ShopDemo Admin",
    }));

    [Fact]
    public void BuildMessage_sets_from_to_subject_and_html_body()
    {
        var sender = CreateSender();

        var message = sender.BuildMessage("someone@example.com", "Reset your password", "<p>Click here</p>");

        Assert.Equal("no-reply@shopdemo.local", message.From.Mailboxes.Single().Address);
        Assert.Equal("someone@example.com", message.To.Mailboxes.Single().Address);
        Assert.Equal("Reset your password", message.Subject);
        Assert.Contains("Click here", message.HtmlBody);
    }
}
