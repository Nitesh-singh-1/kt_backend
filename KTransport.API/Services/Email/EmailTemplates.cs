using System.Net;

namespace KTransport.API.Services.Email
{
    /// <summary>
    /// Minimal, self-contained HTML email bodies. Every template is brand-aware: the caller passes the
    /// recipient's ORGANIZATION name (multi-tenant white-label) so emails never hardcode a product name.
    /// Where no tenant brand exists, callers pass the configured platform name (App:Name).
    /// </summary>
    public static class EmailTemplates
    {
        private static string Brand(string? brand) =>
            WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(brand) ? "your account" : brand.Trim());

        public static (string Subject, string Html) PasswordResetOtp(string brandName, string fullName, string code, int validMinutes)
        {
            var brand = Brand(brandName);
            var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "there" : fullName);
            var subject = $"Your {brand} password reset code";
            var html = Wrap($@"
                <h2 style=""margin:0 0 16px;color:#1a1a1a;"">Password reset requested</h2>
                <p>Hi {name},</p>
                <p>Use the following verification code to reset your password. It is valid for {validMinutes} minutes.</p>
                <div style=""font-size:32px;font-weight:700;letter-spacing:6px;color:#0f766e;background:#f0fdfa;border-radius:8px;padding:16px;text-align:center;margin:20px 0;"">{WebUtility.HtmlEncode(code)}</div>
                <p style=""color:#666;font-size:13px;"">If you did not request this, you can safely ignore this email — your password will not change.</p>", brand);
            return (subject, html);
        }

        public static (string Subject, string Html) Welcome(string brandName, string fullName, string username)
        {
            var brand = Brand(brandName);
            var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "there" : fullName);
            var user = WebUtility.HtmlEncode(username);
            var subject = $"Welcome to {brand}";
            var html = Wrap($@"
                <h2 style=""margin:0 0 16px;color:#1a1a1a;"">Welcome aboard, {name}!</h2>
                <p>Your organization <strong>{brand}</strong> has been set up and is ready to use.</p>
                <p>You can sign in with your username <strong>{user}</strong> and the password you chose during signup.</p>
                <p style=""color:#666;font-size:13px;"">For security, we recommend changing your password after your first sign-in from Settings.</p>", brand);
            return (subject, html);
        }

        public static (string Subject, string Html) TeamInvite(string brandName, string inviterName, string acceptUrl, int validDays)
        {
            var brand = Brand(brandName);
            var inviter = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(inviterName) ? "Your administrator" : inviterName);
            var url = WebUtility.HtmlEncode(acceptUrl);
            var subject = $"You're invited to join {brand}";
            var html = Wrap($@"
                <h2 style=""margin:0 0 16px;color:#1a1a1a;"">You've been invited</h2>
                <p>{inviter} has invited you to join <strong>{brand}</strong>.</p>
                <p>Click below to set up your account. This invitation is valid for {validDays} days.</p>
                <p style=""margin:24px 0;""><a href=""{url}"" style=""background:#0f766e;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:700;display:inline-block;"">Accept Invitation</a></p>
                <p style=""color:#666;font-size:12px;word-break:break-all;"">Or paste this link into your browser:<br/>{url}</p>", brand);
            return (subject, html);
        }

        private static string Wrap(string inner, string brand)
        {
            return $@"<!doctype html>
<html>
  <body style=""margin:0;padding:0;background:#f4f4f5;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1a1a1a;"">
    <div style=""max-width:560px;margin:24px auto;background:#ffffff;border-radius:12px;padding:32px;border:1px solid #e5e7eb;"">
      <div style=""font-size:20px;font-weight:700;color:#0f766e;margin-bottom:24px;"">{brand}</div>
      {inner}
      <hr style=""border:none;border-top:1px solid #e5e7eb;margin:24px 0;"" />
      <p style=""color:#9ca3af;font-size:12px;margin:0;"">This is an automated message from {brand}. Please do not reply.</p>
    </div>
  </body>
</html>";
        }
    }
}
