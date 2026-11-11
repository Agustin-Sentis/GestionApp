namespace GastosBackend.Application.dtos
{
    /// <summary>A mail ready to send: the address it goes to and its subject, in plain text and in HTML.</summary>
    public record EmailMessage(string To, string Subject, string TextBody, string HtmlBody);
}
