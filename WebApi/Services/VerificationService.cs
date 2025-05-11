using Azure;
using Azure.Communication.Email;
using Microsoft.AspNetCore.Mvc.Formatters.Xml;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Diagnostics;
using WebApi.Models;

namespace WebApi.Services;

public interface IVerificationService
{
    Task<VerificationServiceResult> SendVerificationCodeAsync(SendVerificationRequest request);
    void SaveVerificationCode(SaveVerificationCodeRequest request);
    VerificationServiceResult VerifyVerificationCode(VerifyVerificationCodeRequest request);
}

public class VerificationService(IConfiguration configuration, EmailClient emailClient, IMemoryCache memoryCache) : IVerificationService
{

    private readonly IConfiguration _configuration = configuration;
    private readonly EmailClient _emailClient = emailClient;
    private readonly IMemoryCache _cache = memoryCache;
    private static readonly Random _random = new();

    public async Task<VerificationServiceResult> SendVerificationCodeAsync(SendVerificationRequest request)
    {
        try
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
                return new VerificationServiceResult { Succeeded = false, Error = "Email address is required" };

            var verificationCode = _random.Next(100000, 999999).ToString();
            var subject = $"Your verification code is {verificationCode}";
            var plaintTextContent = @$"
            Verify your Email Address

            Hello,

            To complete your verification, please enter the following code:

            {verificationCode}

            Or use the following link:
            https://ventixe.com/verify?email={request.Email}&token=
            
            If you did not initiate this request, please ignore this email.
            Thank you,


            Privacy Policy
            https://ventixe.com/privacy-policy


            © ventixe.com 2025. All rights reserved.
            ";
            var htmlContent = $@"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
              <meta charset='UTF-8'>
              <title>Your verification code</title>
            </head>
            <body style='margin:0; padding:32px; font-family: Inter, sans-serif; background-color:#F7F7F7; color:#1E1E20;'>
              <div style='max-width:600px; margin:32px auto; background:#FFFFFF; border-radius:16px; padding:32px;'>

                <h1 style='font-size:32px; font-weight:600; color:#37437D; margin-bottom:16px; text-align:center;'>
                  Verify Your Email Address
                </h1>

                <p style='font-size:16px; color:#1E1E20; margin-bottom:16px;'>Hello,</p>

                <p style='font-size:16px; color:#1E1E20; margin-bottom:24px;'>
                  To complete your verification, please enter the code below or click the button to open a new page.
                </p>

                <div style='display:flex; justify-content:center; align-items:center; padding:16px; background-color:#FCD3FE; color:#1C2346; font-size:32px; letter-spacing:0.5rem; border-radius:12px; font-weight:600; margin-bottom:24px;'>
                  {verificationCode}
                </div>

                <div style='text-align:center; margin-bottom:32px;'>
                  <a href='https://domain.com/verify?email={request.Email}&token=' style='background-color:#F26CF9; color:#FFFFFF; padding:12px 24px; border-radius:25px; font-size:16px; text-decoration:none; display:inline-block;'>
                    Open Verification Page
                  </a>
                </div>

                <p style='font-size:12px; color:#777779; text-align:center; margin-top:24px;'>
                  If you did not initiate this request, you can safely disregard this email.
                  <br><br>
                  We take your privacy seriously. No further action is required if you did not initiate this request.
                  For more information about how we process personal data, please see our 
                  <a href='https://domain.com/privacy-policy' style='color:#F26CF9; text-decoration:none;'>Privacy Policy</a>.
                </p>

                <div style='font-size:12px; color:#777779; text-align:center; margin-top:24px;'>
                  © ventixe.com. All rights reserved.
                </div>

              </div>
            </body>
            </html>

        ";

            var emailMessage = new EmailMessage(
                senderAddress: _configuration["ACS:SenderAddress"],
                recipients: new EmailRecipients([new(request.Email)]),
                content: new EmailContent(subject)
                {
                    PlainText = plaintTextContent,
                    Html = htmlContent
                });

            var emailSendOperation = await _emailClient.SendAsync(WaitUntil.Started, emailMessage);
            SaveVerificationCode(new SaveVerificationCodeRequest
            {
                Email = request.Email,
                Code = verificationCode,
                ValidFor = TimeSpan.FromMinutes(5)
            });

            return new VerificationServiceResult { Succeeded = true, Message = "Verification email sent successfully" };
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new VerificationServiceResult { Succeeded = false, Error = ex.Message };

        }


    }

    public void SaveVerificationCode(SaveVerificationCodeRequest request)
    {
        _cache.Set(request.Email.ToLowerInvariant(), request.Code, request.ValidFor);
    }

    public VerificationServiceResult VerifyVerificationCode(VerifyVerificationCodeRequest request)
    {
        var key = request.Email.ToLowerInvariant();

        if (_cache.TryGetValue(key, out string? storedCode))
        {
            if (storedCode == request.Code)
            {
                _cache.Remove(key);
                return new VerificationServiceResult { Succeeded = true, Message = "Verification successful." };
            }

        }
        return new VerificationServiceResult { Succeeded = false, Error = "Invalid or expired verification code." };

    }
};
