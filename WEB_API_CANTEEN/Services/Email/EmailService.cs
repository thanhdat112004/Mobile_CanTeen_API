using System.Threading;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Configuration;

namespace WEB_API_CANTEEN.Services
{
    public interface IEmailService
    {
        Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _cfg;
        public EmailService(IConfiguration cfg) => _cfg = cfg;

        public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                _cfg["Email:FromName"] ?? "Smart Canteen",
                _cfg["Email:FromEmail"]));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var smtp = new SmtpClient();
            var host = _cfg["Email:SmtpHost"]!;
            var port = int.Parse(_cfg["Email:SmtpPort"] ?? "587");
            var user = _cfg["Email:User"]!;
            var pass = _cfg["Email:Pass"]!;
            var useStartTls = bool.Parse(_cfg["Email:UseStartTls"] ?? "true");

            await smtp.ConnectAsync(host, port, useStartTls ? SecureSocketOptions.StartTls : SecureSocketOptions.Auto, ct);
            await smtp.AuthenticateAsync(user, pass, ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);
        }
    }
}
