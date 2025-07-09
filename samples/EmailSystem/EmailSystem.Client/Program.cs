using EmailSystem.Application.Contracts.Dtos;

using ErrorOr;
using NATS.Client.Core;
using NATS.Net;

var opts = NatsOpts.Default with
{
    Url = "nats://localhost:4223"
};
var client = new NatsClient(opts);
const string SendEmailSubject = "emailsys.emails.send";
var payload = new SendEmailDto
{
    Subject = "Test-Email",
    To = "rain.hu@advantech.com",
    From = "noreply@advantech.com",
    HtmlContent = "<html><body><h1>Welcome!</h1><p>This is a test email from EmailSystem.</p></body></html>",
    TextContent = "Welcome!\n\nThis is a test email from EmailSystem.",
    Data = []
};
try
{
    var response = await client.RequestAsync<SendEmailDto, ErrorOr<EmailDto>>(SendEmailSubject, payload);
    System.Console.WriteLine(response.Subject);
}
catch (Exception ex)
{
    System.Console.WriteLine(ex.Message); 
}
