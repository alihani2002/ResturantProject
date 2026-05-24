using Mailjet.Client;
using Mailjet.Client.Resources;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;

namespace Resturant.Services.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly IConfiguration _configuration;

        public EmailSender(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var apiKey = _configuration["MailJet:ApiKey"];
            var secretKey = _configuration["MailJet:SecretKey"];
            var senderEmail = _configuration["MailJet:SenderEmail"] ?? "info@resturant.com";

            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secretKey))
            {
                throw new InvalidOperationException("Mailjet API details are missing in configuration.");
            }

            MailjetClient client = new MailjetClient(apiKey, secretKey);

            MailjetRequest request = new MailjetRequest
            {
                Resource = Send.Resource,
            }
            .Property(Send.FromEmail, senderEmail)
            .Property(Send.FromName, "Resturant App")
            .Property(Send.Subject, subject)
            .Property(Send.HtmlPart, htmlMessage)
            .Property(Send.Recipients, new JArray {
                new JObject {
                    {"Email", email}
                }
            });

            await client.PostAsync(request);
        }
    }
}
