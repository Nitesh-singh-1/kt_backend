using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using KTransport.API.Authorization;
using KTransport.API.Models;
using KTransport.API.Services;
using KTransport.API.Services.Email;

namespace KTransport.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("AuthPolicy")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly ILogger<AuthController> _logger;
        private readonly IFeatureAuthorizationService _authorizationService;
        private readonly IInvitationService _invitationService;
        private readonly IEmailSender _emailSender;

        public AuthController(IAuthService authService, ILogger<AuthController> logger, IFeatureAuthorizationService authorizationService, IInvitationService invitationService, IEmailSender emailSender)
        {
            _authService = authService;
            _logger = logger;
            _authorizationService = authorizationService;
            _invitationService = invitationService;
            _emailSender = emailSender;
        }

        /// <summary>Admin diagnostic: send a test email to verify SMTP configuration.</summary>
        [HttpPost("test-email")]
        [Authorize]
        [RequireSuperUser]
        public async Task<IActionResult> TestEmail([FromBody] TestEmailRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.To))
            {
                return BadRequest(new { success = false, message = "A recipient email ('to') is required." });
            }

            var sent = await _emailSender.SendAsync(
                request.To.Trim(),
                "KTransport SMTP test",
                "<p>This is a test email from KTransport. If you received it, your SMTP configuration is working.</p>");

            return Ok(new
            {
                success = sent,
                emailEnabled = _emailSender.IsEnabled,
                message = sent
                    ? "Test email handed off to the SMTP server successfully. Check the inbox (and spam)."
                    : (_emailSender.IsEnabled
                        ? "SMTP is enabled but the send failed — check the server logs for the SMTP error (bad credentials, unverified sender, etc.)."
                        : "Email is disabled (Email:Enabled=false) — the message was only logged, not sent.")
            });
        }

        /// <summary>Public: fetch invitation info (org, email, role) for the accept-invite page.</summary>
        [HttpGet("invite/{token}")]
        [AllowAnonymous]
        public async Task<ActionResult<KTransport.API.DTOs.InviteInfoDto>> GetInvite(string token)
        {
            var info = await _invitationService.GetInviteInfoByTokenAsync(token);
            return Ok(info);
        }

        /// <summary>Public: accept an invitation — the invitee sets their own username/password, then is logged in.</summary>
        [HttpPost("accept-invite")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> AcceptInvite([FromBody] KTransport.API.DTOs.AcceptInviteRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var response = await _invitationService.AcceptInviteAsync(request);
            if (!response.Success) return BadRequest(response);
            return Ok(response);
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.LoginAsync(request);

            if (!response.Success)
            {
                return Unauthorized(response);
            }

            return Ok(response);
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.RegisterAsync(request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        /// <summary>Exchange a valid refresh token for a new access token (rotates the refresh token).</summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request)
        {
            var response = await _authService.RefreshTokenAsync(request?.RefreshToken ?? string.Empty);
            if (!response.Success)
            {
                return Unauthorized(response);
            }
            return Ok(response);
        }

        /// <summary>Revoke a refresh token (sign-out). Idempotent.</summary>
        [HttpPost("logout")]
        [AllowAnonymous]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
        {
            await _authService.RevokeRefreshTokenAsync(request?.RefreshToken ?? string.Empty);
            return Ok(new { success = true, message = "Signed out." });
        }

        [HttpPost("validate")]
        [Authorize]
        public async Task<ActionResult<AuthResponse>> ValidateToken([FromBody] string token)
        {
            var response = await _authService.ValidateTokenAsync(token);

            if (!response.Success)
            {
                return Unauthorized(response);
            }

            return Ok(response);
        }

        [HttpPost("forgot-password/request-code")]
        [AllowAnonymous]
        public async Task<ActionResult<RequestResetCodeResponse>> RequestPasswordResetCode([FromBody] RequestResetCodeRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.RequestPasswordResetCodeAsync(request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("forgot-password/verify-and-reset")]
        [AllowAnonymous]
        public async Task<ActionResult<ResetPasswordResponse>> VerifyAndResetPassword([FromBody] VerifyAndResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.VerifyAndResetPasswordAsync(request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<ActionResult<ResetPasswordResponse>> ResetPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _authService.ResetPasswordAsync(request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpPost("change-password")]
        [Authorize]
        public async Task<ActionResult<ResetPasswordResponse>> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new ResetPasswordResponse { Success = false, Message = "Invalid user session." });
            }

            var response = await _authService.ChangePasswordAsync(userId, request);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<object>> GetCurrentUser()
        {
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid user session." });
            }

            var profile = await _authService.GetMyProfileAsync(userId);
            if (profile == null)
            {
                return NotFound(new { message = "User not found." });
            }

            profile.IsPlatformAdmin = _authorizationService.IsPlatformAdmin(User);
            return Ok(profile);
        }

        /// <summary>
        /// Update the currently authenticated user's own profile (full name, mobile, email).
        /// Any authenticated user may update their own profile; password changes use change-password.
        /// </summary>
        [HttpPut("profile")]
        [Authorize]
        public async Task<ActionResult<object>> UpdateMyProfile([FromBody] UpdateProfileRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { message = "Invalid user session." });
            }

            var (success, message, profile) = await _authService.UpdateMyProfileAsync(userId, request);
            if (!success)
            {
                return BadRequest(new { success = false, message });
            }

            return Ok(new { success = true, message, profile });
        }
    }
}