using System.Net;

namespace KTransport.API.Services.Email
{
    /// <summary>
    /// Minimal, self-contained HTML email bodies. Kept inline (no template engine) deliberately —
    /// there are only two transactional emails today; revisit if the set grows.
    /// </summary>
    public static class EmailTemplates
    {
        public static (string Subject, string Html) PasswordResetOtp(string fullName, string code, int validMinutes)
        {
            var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "there" : fullName);
            var subject = "Your KTransport password reset code";
            var html = Wrap($@"
                <h2 style=""margin:0 0 16px;color:#1a1a1a;"">Password reset requested</h2>
                <p>Hi {name},</p>
                <p>Use the following verification code to reset your KTransport password. It is valid for {validMinutes} minutes.</p>
                <div style=""font-size:32px;font-weight:700;letter-spacing:6px;color:#0f766e;background:#f0fdfa;border-radius:8px;padding:16px;text-align:center;margin:20px 0;"">{WebUtility.HtmlEncode(code)}</div>
                <p style=""color:#666;font-size:13px;"">If you did not request this, you can safely ignore this email — your password will not change.</p>");
            return (subject, html);
        }

        public static (string Subject, string Html) Welcome(string fullName, string organizationName, string username)
        {
            var name = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(fullName) ? "there" : fullName);
            var org = WebUtility.HtmlEncode(organizationName);
            var user = WebUtility.HtmlEncode(username);
            var subject = $"Welcome to KTransport, {org}";
            var html = Wrap($@"
                <h2 style=""margin:0 0 16px;color:#1a1a1a;"">Welcome aboard, {name}!</h2>
                <p>Your organization <strong>{org}</strong> has been set up on KTransport.</p>
                <p>You can sign in with your username <strong>{user}</strong> and the password you chose during signup.</p>
                <p style=""color:#666;font-size:13px;"">For security, we recommend changing your password after your first sign-in from Settings.</p>");
            return (subject, html);
        }

        public static (string Subject, string Html) TeamInvite(string organizationName, string inviterName, string acceptUrl, int validDays)
        {
            var org = WebUtility.HtmlEncode(organizationName);
            var inviter = WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(inviterName) ? "Your administrator" : inviterName);
            var url = WebUtility.HtmlEncode(acceptUrl);
            var subject = $"You're invited to join {org} on KTransport";
            var html = Wrap($@"
                <h2 style=""margin:0 0 16px;color:#1a1a1a;"">You've been invited</h2>
                <p>{inviter} has invited you to join <strong>{org}</strong> on KTransport.</p>
                <p>Click below to set up your account. This invitation is valid for {validDays} days.</p>
                <p style=""margin:24px 0;""><a href=""{url}"" style=""background:#0f766e;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:8px;font-weight:700;display:inline-block;"">Accept Invitation</a></p>
                <p style=""color:#666;font-size:12px;word-break:break-all;"">Or paste this link into your browser:<br/>{url}</p>");
            return (subject, html);
        }

        private static string Wrap(string inner)
        {
            return $@"<!doctype html>
<html>
  <body style=""margin:0;padding:0;background:#f4f4f5;font-family:-apple-system,Segoe UI,Roboto,Helvetica,Arial,sans-serif;color:#1a1a1a;"">
    <div style=""max-width:560px;margin:24px auto;background:#ffffff;border-radius:12px;padding:32px;border:1px solid #e5e7eb;"">
      <div style=""font-size:20px;font-weight:700;color:#0f766e;margin-bottom:24px;"">KTransport</div>
      {inner}
      <hr style=""border:none;border-top:1px solid #e5e7eb;margin:24px 0;"" />
      <p style=""color:#9ca3af;font-size:12px;margin:0;"">This is an automated message from KTransport. Please do not reply.</p>
    </div>
  </body>
</html>";
        }
    }
}
