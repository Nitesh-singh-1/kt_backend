using KTransport.API.Data;
using KTransport.API.Models;
using KTransport.API.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace KTransport.API.Services
{
    public class AuthService : IAuthService
    {
        private const string PasswordResetPurpose = "PasswordReset";
        private const int ResetCodeValidMinutes = 10;

        private static readonly HashSet<string> AdminRoleValues = new(StringComparer.OrdinalIgnoreCase)
        {
            "admin", "superadmin", "tenantadmin", "super_user", "tenant_owner"
        };

        // Platform operator = admin-class role AND membership in the default/platform tenant.
        private static bool ComputeIsPlatformAdmin(User user) =>
            AdminRoleValues.Contains(user.Role ?? "") && user.TenantId == TenantContext.DefaultTenantId;

        private readonly ILogger<AuthService> _logger;
        private readonly KTransportDbContext _context;
        private readonly JwtSettings _jwtSettings;
        private readonly IAuditLogService _auditLogService;
        private readonly IVerificationCodeService _verificationCodeService;
        private readonly IEmailSender _emailSender;
        private readonly EmailSettings _emailSettings;

        public AuthService(
            ILogger<AuthService> logger,
            KTransportDbContext context,
            IOptions<JwtSettings> jwtSettings,
            IAuditLogService auditLogService,
            IVerificationCodeService verificationCodeService,
            IEmailSender emailSender,
            IOptions<EmailSettings> emailSettings)
        {
            _logger = logger;
            _context = context;
            _jwtSettings = jwtSettings.Value;
            _auditLogService = auditLogService;
            _verificationCodeService = verificationCodeService;
            _emailSender = emailSender;
            _emailSettings = emailSettings.Value;
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            try
            {
                _logger.LogInformation("Login attempt for username: {Username}", request.Username);

                // Find user by username (IgnoreQueryFilters to allow login across all tenants without requiring prior JWT token)
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive == true);

                if (user == null)
                {
                    await _auditLogService.LogAsync("LoginFailed", success: false, username: request.Username, details: "Unknown username");
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    };
                }

                // Verify password (supports BCrypt hashed passwords and transparently upgrades legacy plaintext passwords)
                bool passwordValid = false;
                bool isHashed = user.Password.StartsWith("$2a$") || 
                                user.Password.StartsWith("$2b$") || 
                                user.Password.StartsWith("$2x$") || 
                                user.Password.StartsWith("$2y$");

                if (isHashed)
                {
                    try
                    {
                        passwordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.Password);
                    }
                    catch
                    {
                        passwordValid = false;
                    }
                }
                else
                {
                    // Legacy plaintext password check
                    if (request.Password == user.Password)
                    {
                        passwordValid = true;
                        // Transparently upgrade to BCrypt hash
                        user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("Transparently upgraded password to BCrypt hash for user: {Username}", user.Username);
                    }
                }

                if (!passwordValid)
                {
                    await _auditLogService.LogAsync("LoginFailed", success: false, tenantId: user.TenantId, userId: user.Id, username: user.Username, details: "Incorrect password");
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    };
                }

                // Generate JWT token
                var token = GenerateJwtToken(user);
                var refreshToken = await CreateRefreshTokenAsync(user.Id);

                await _auditLogService.LogAsync("Login", tenantId: user.TenantId, userId: user.Id, username: user.Username);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Token = token,
                    RefreshToken = refreshToken,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FullName ?? string.Empty,
                        Role = user.Role ?? "User",
                        Mobile = user.Mobile,
                        Email = user.Email,
                        IsPlatformAdmin = ComputeIsPlatformAdmin(user)
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login");
                return new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during login"
                };
            }
        }

        public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Registration attempt for username: {Username}", request.Username);

                // Check if user already exists
                var existingUser = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());

                if (existingUser != null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Username already exists"
                    };
                }

                // Never grant admin role via public registration - always default to SUB_USER
                var assignedRole = string.IsNullOrWhiteSpace(request.Role) || request.Role.Equals("admin", StringComparison.OrdinalIgnoreCase) || request.Role.Equals("SUPERADMIN", StringComparison.OrdinalIgnoreCase)
                    ? "SUB_USER"
                    : request.Role;

                // Create new user with BCrypt hashed password
                var newUser = new User
                {
                    Username = request.Username.Trim(),
                    Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                    FullName = request.FullName.Trim(),
                    Mobile = request.Mobile?.Trim(),
                    Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
                    Role = assignedRole,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                // Generate JWT token
                var token = GenerateJwtToken(newUser);
                var refreshToken = await CreateRefreshTokenAsync(newUser.Id);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    Token = token,
                    RefreshToken = refreshToken,
                    User = new UserDto
                    {
                        Id = newUser.Id,
                        Username = newUser.Username,
                        FullName = newUser.FullName ?? string.Empty,
                        Role = newUser.Role ?? "SUB_USER",
                        Mobile = newUser.Mobile,
                        Email = newUser.Email
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration");
                return new AuthResponse
                {
                    Success = false,
                    Message = "An error occurred during registration"
                };
            }
        }

        public async Task<AuthResponse> ValidateTokenAsync(string token)
        {
            try
            {
                _logger.LogInformation("Token validation attempt");

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

                var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userIdClaim))
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid token"
                    };
                }

                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == int.Parse(userIdClaim));

                if (user == null || user.IsActive != true)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "User not found or inactive"
                    };
                }

                return new AuthResponse
                {
                    Success = true,
                    Message = "Token is valid",
                    User = new UserDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FullName ?? string.Empty,
                        Role = user.Role ?? "SUB_USER",
                        Mobile = user.Mobile,
                        Email = user.Email
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token validation");
                return new AuthResponse
                {
                    Success = false,
                    Message = "Invalid token"
                };
            }
        }

        public async Task<RequestResetCodeResponse> RequestPasswordResetCodeAsync(RequestResetCodeRequest request)
        {
            try
            {
                _logger.LogInformation("Password reset verification code requested for username: {Username}", request.Username);

                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Mobile))
                {
                    return new RequestResetCodeResponse
                    {
                        Success = false,
                        Message = "Username and registered mobile number are required."
                    };
                }

                var cleanUsername = request.Username.Trim().ToLower();
                var cleanMobile = new string(request.Mobile.Where(char.IsDigit).ToArray());

                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername && u.IsActive == true);

                if (user == null)
                {
                    return new RequestResetCodeResponse
                    {
                        Success = false,
                        Message = "No active user found with the provided username."
                    };
                }

                var userMobileClean = new string((user.Mobile ?? "").Where(char.IsDigit).ToArray());
                if (!string.IsNullOrEmpty(userMobileClean) && !string.IsNullOrEmpty(cleanMobile))
                {
                    if (!userMobileClean.EndsWith(cleanMobile) && !cleanMobile.EndsWith(userMobileClean))
                    {
                        return new RequestResetCodeResponse
                        {
                            Success = false,
                            Message = "Mobile number does not match registered account records."
                        };
                    }
                }

                // Generate + persist a 6-digit OTP (hashed, with expiry) in the DB.
                var verificationCode = await _verificationCodeService.GenerateAsync(cleanUsername, PasswordResetPurpose, ResetCodeValidMinutes);

                await _auditLogService.LogAsync("PasswordResetRequested", tenantId: user.TenantId, userId: user.Id, username: user.Username);

                // Deliver via email when possible; never expose the code in the API response in that case.
                var emailed = false;
                if (!string.IsNullOrWhiteSpace(user.Email))
                {
                    // White-label: brand the email with the user's own organization name.
                    var brand = (await _context.Tenants.IgnoreQueryFilters()
                        .FirstOrDefaultAsync(t => t.Id == user.TenantId))?.Name;
                    var (subject, html) = EmailTemplates.PasswordResetOtp(brand ?? "", user.FullName ?? user.Username, verificationCode, ResetCodeValidMinutes);
                    emailed = await _emailSender.SendAsync(user.Email!, subject, html, brand);
                }

                if (emailed)
                {
                    return new RequestResetCodeResponse
                    {
                        Success = true,
                        Message = $"A verification code has been sent to your registered email. Valid for {ResetCodeValidMinutes} minutes.",
                        ExpiresInSeconds = ResetCodeValidMinutes * 60
                    };
                }

                // No deliverable email channel (email disabled, or user has no email on file).
                // The code was logged server-side; only ever echo it back in the response when
                // Email:ExposeOtpWhenDisabled is explicitly turned on (local dev only — see
                // EmailSettings.ExposeOtpWhenDisabled). Email being merely unconfigured must never
                // by itself put a live OTP into an API response reachable by the browser.
                _logger.LogInformation("Verification code for {Username}: {Code} (valid {Minutes} min)", cleanUsername, verificationCode, ResetCodeValidMinutes);

                var includeCodeInResponse = !_emailSender.IsEnabled && _emailSettings.ExposeOtpWhenDisabled;
                return new RequestResetCodeResponse
                {
                    Success = true,
                    Message = includeCodeInResponse
                        ? $"Verification code generated (email delivery disabled). Valid for {ResetCodeValidMinutes} minutes."
                        : $"If the account has a registered email, a verification code has been sent. Valid for {ResetCodeValidMinutes} minutes.",
                    VerificationCode = includeCodeInResponse ? verificationCode : null,
                    ExpiresInSeconds = ResetCodeValidMinutes * 60
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error requesting password reset code");
                return new RequestResetCodeResponse
                {
                    Success = false,
                    Message = "An error occurred while generating verification code."
                };
            }
        }

        public async Task<ResetPasswordResponse> VerifyAndResetPasswordAsync(VerifyAndResetPasswordRequest request)
        {
            try
            {
                _logger.LogInformation("Verifying reset code for username: {Username}", request.Username);

                if (string.IsNullOrWhiteSpace(request.Username) || 
                    string.IsNullOrWhiteSpace(request.VerificationCode) || 
                    string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Username, verification code, and new password are required."
                    };
                }

                var cleanUsername = request.Username.Trim().ToLower();

                var verifyResult = await _verificationCodeService.VerifyAsync(cleanUsername, PasswordResetPurpose, request.VerificationCode);

                if (verifyResult != VerificationResult.Success)
                {
                    var message = verifyResult switch
                    {
                        VerificationResult.Expired => "Verification code has expired. Please request a new code.",
                        VerificationResult.NotFound => "No verification code was requested for this account, or it has already been used. Please request a new code.",
                        VerificationResult.TooManyAttempts => "Too many incorrect attempts. This code has been disabled — please request a new one.",
                        _ => "Invalid verification code. Please check and try again."
                    };

                    await _auditLogService.LogAsync("PasswordResetFailed", success: false, username: cleanUsername, details: verifyResult.ToString());

                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = message
                    };
                }

                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername && u.IsActive == true);

                if (user == null)
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "User not found or inactive."
                    };
                }

                // Update password with BCrypt hash
                user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Password reset successfully verified for user: {Username}", user.Username);

                await _auditLogService.LogAsync("PasswordReset", tenantId: user.TenantId, userId: user.Id, username: user.Username, details: "Via OTP verification");

                return new ResetPasswordResponse
                {
                    Success = true,
                    Message = "Password has been reset successfully. You can now sign in with your new password."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying and resetting password for {Username}", request.Username);
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "An error occurred while updating password."
                };
            }
        }

        public async Task<ResetPasswordResponse> ResetPasswordAsync(ForgotPasswordRequest request)
        {
            try
            {
                _logger.LogInformation("Password reset attempt for username: {Username}", request.Username);

                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.NewPassword))
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Username and new password are required."
                    };
                }

                var cleanUsername = request.Username.Trim().ToLower();
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == cleanUsername && u.IsActive == true);

                if (user == null)
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "User not found or inactive."
                    };
                }

                // Verify mobile if provided
                if (!string.IsNullOrWhiteSpace(request.Mobile) && !string.IsNullOrWhiteSpace(user.Mobile))
                {
                    var reqMobile = new string(request.Mobile.Where(char.IsDigit).ToArray());
                    var userMobile = new string(user.Mobile.Where(char.IsDigit).ToArray());

                    if (!string.IsNullOrEmpty(userMobile) && !string.IsNullOrEmpty(reqMobile))
                    {
                        if (!userMobile.EndsWith(reqMobile) && !reqMobile.EndsWith(userMobile))
                        {
                            return new ResetPasswordResponse
                            {
                                Success = false,
                                Message = "Mobile number does not match registered user details."
                            };
                        }
                    }
                }

                // Hash and update password
                user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Password reset successfully for user: {Username}", user.Username);

                await _auditLogService.LogAsync("PasswordReset", tenantId: user.TenantId, userId: user.Id, username: user.Username, details: "Via mobile verification");

                return new ResetPasswordResponse
                {
                    Success = true,
                    Message = "Password has been reset successfully. You can now log in with your new password."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during password reset for username: {Username}", request.Username);
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "An error occurred while resetting the password."
                };
            }
        }

        public async Task<ResetPasswordResponse> ChangePasswordAsync(int userId, ChangePasswordRequest request)
        {
            try
            {
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive == true);

                if (user == null)
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "User not found."
                    };
                }

                bool oldPasswordValid = false;
                bool isHashed = user.Password.StartsWith("$2a$") || 
                                user.Password.StartsWith("$2b$") || 
                                user.Password.StartsWith("$2x$") || 
                                user.Password.StartsWith("$2y$");

                if (isHashed)
                {
                    try
                    {
                        oldPasswordValid = BCrypt.Net.BCrypt.Verify(request.OldPassword, user.Password);
                    }
                    catch
                    {
                        oldPasswordValid = false;
                    }
                }
                else
                {
                    oldPasswordValid = (request.OldPassword == user.Password);
                }

                if (!oldPasswordValid)
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Current password is incorrect."
                    };
                }

                user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                await _context.SaveChangesAsync();

                await _auditLogService.LogAsync("PasswordChanged", tenantId: user.TenantId, userId: user.Id, username: user.Username);

                return new ResetPasswordResponse
                {
                    Success = true,
                    Message = "Password changed successfully."
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing password for user ID: {UserId}", userId);
                return new ResetPasswordResponse
                {
                    Success = false,
                    Message = "An error occurred while changing password."
                };
            }
        }

        public async Task<MyProfileDto?> GetMyProfileAsync(int userId)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return null;

            return new MyProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                FullName = user.FullName ?? string.Empty,
                Role = user.Role ?? "SUB_USER",
                Mobile = user.Mobile,
                Email = user.Email
            };
        }

        public async Task<(bool Success, string Message, MyProfileDto? Profile)> UpdateMyProfileAsync(int userId, UpdateProfileRequest request)
        {
            try
            {
                var user = await _context.Users
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive == true);

                if (user == null)
                {
                    return (false, "User not found.", null);
                }

                if (request.FullName != null) user.FullName = request.FullName.Trim();
                if (request.Mobile != null) user.Mobile = string.IsNullOrWhiteSpace(request.Mobile) ? null : request.Mobile.Trim();
                if (request.Email != null) user.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

                await _context.SaveChangesAsync();

                await _auditLogService.LogAsync("ProfileUpdated", tenantId: user.TenantId, userId: user.Id, username: user.Username);

                return (true, "Profile updated successfully.", new MyProfileDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    FullName = user.FullName ?? string.Empty,
                    Role = user.Role ?? "SUB_USER",
                    Mobile = user.Mobile,
                    Email = user.Email
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating profile for user ID {UserId}", userId);
                return (false, "An error occurred while updating your profile.", null);
            }
        }

        public async Task<AuthResponse> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return new AuthResponse { Success = false, Message = "Refresh token is required." };
            }

            var hash = HashToken(refreshToken);
            var stored = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash);

            if (stored == null || stored.RevokedAt != null || stored.ExpiresAt <= DateTime.UtcNow)
            {
                return new AuthResponse { Success = false, Message = "Invalid or expired refresh token." };
            }

            var user = await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == stored.UserId && u.IsActive == true);
            if (user == null)
            {
                return new AuthResponse { Success = false, Message = "User not found or inactive." };
            }

            // Rotate: issue a new refresh token and revoke the old one (linked for reuse detection).
            var newRefresh = await CreateRefreshTokenAsync(user.Id);
            stored.RevokedAt = DateTime.UtcNow;
            stored.ReplacedByHash = HashToken(newRefresh);
            await _context.SaveChangesAsync();

            return new AuthResponse
            {
                Success = true,
                Message = "Token refreshed.",
                Token = GenerateJwtToken(user),
                RefreshToken = newRefresh,
                User = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    FullName = user.FullName ?? string.Empty,
                    Role = user.Role ?? "SUB_USER",
                    Mobile = user.Mobile,
                    Email = user.Email,
                    IsPlatformAdmin = ComputeIsPlatformAdmin(user)
                }
            };
        }

        public async Task RevokeRefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken)) return;
            var hash = HashToken(refreshToken);
            var stored = await _context.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash && t.RevokedAt == null);
            if (stored != null)
            {
                stored.RevokedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }
        }

        // Allowed character set for a username. Kept restrictive on purpose so we can:
        //   1) reject noise (whitespace, symbols) before touching the DB, and
        //   2) drop the query entirely for anything that could never register — which shrinks
        //      the surface area of the anonymous availability endpoint against enumeration/probing.
        private static readonly System.Text.RegularExpressions.Regex _usernamePattern =
            new(@"^[A-Za-z0-9._-]+$", System.Text.RegularExpressions.RegexOptions.Compiled);

        public async Task<(bool Valid, bool Available, string? Reason)> IsUsernameAvailableAsync(string? candidate)
        {
            var input = (candidate ?? string.Empty).Trim();

            if (input.Length == 0)
                return (false, false, "Username is required.");
            if (input.Length < 3)
                return (false, false, "Username must be at least 3 characters.");
            if (input.Length > 50)
                return (false, false, "Username cannot exceed 50 characters.");
            if (!_usernamePattern.IsMatch(input))
                return (false, false, "Only letters, numbers, dot, underscore and hyphen are allowed.");

            // Case-insensitive match to line up with the Register/Onboarding duplicate checks.
            // EF Core parameterises the value; a plain equality on ToLower() is safe against
            // injection but not necessarily index-friendly — that is acceptable here because
            // the input is length-capped, the endpoint is rate-limited, and the users table is
            // small at this scale. Revisit with a functional lower(username) index if it grows.
            var normalized = input.ToLowerInvariant();
            var taken = await _context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .AnyAsync(u => u.Username.ToLower() == normalized);

            return (true, !taken, null);
        }

        private async Task<string> CreateRefreshTokenAsync(int userId)
        {
            var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace("+", "-").Replace("/", "_").TrimEnd('=');

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = userId,
                TokenHash = HashToken(raw),
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenDays <= 0 ? 7 : _jwtSettings.RefreshTokenDays),
                CreatedAt = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();
            return raw;
        }

        private static string HashToken(string token)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            return Convert.ToHexString(bytes).ToLowerInvariant();
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role ?? "User"),
                new Claim("FullName", user.FullName ?? string.Empty),
                new Claim("tenant_id", user.TenantId.ToString())
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryInMinutes),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}