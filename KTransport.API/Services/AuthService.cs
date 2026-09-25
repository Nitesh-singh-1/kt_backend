using KTransport.API.Data;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace KTransport.API.Services
{
    public class AuthService : IAuthService
    {
        private readonly ILogger<AuthService> _logger;
        private readonly KTransportDbContext _context;
        private readonly JwtSettings _jwtSettings;

        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (string Code, DateTime Expiry)> _resetCodes = new(StringComparer.OrdinalIgnoreCase);

        public AuthService(ILogger<AuthService> logger, KTransportDbContext context, IOptions<JwtSettings> jwtSettings)
        {
            _logger = logger;
            _context = context;
            _jwtSettings = jwtSettings.Value;
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
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    };
                }

                // Generate JWT token
                var token = GenerateJwtToken(user);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Login successful",
                    Token = token,
                    User = new UserDto
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FullName ?? string.Empty,
                        Role = user.Role ?? "User",
                        Mobile = user.Mobile
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
                    Role = assignedRole,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Users.Add(newUser);
                await _context.SaveChangesAsync();

                // Generate JWT token
                var token = GenerateJwtToken(newUser);

                return new AuthResponse
                {
                    Success = true,
                    Message = "Registration successful",
                    Token = token,
                    User = new UserDto
                    {
                        Id = newUser.Id,
                        Username = newUser.Username,
                        FullName = newUser.FullName ?? string.Empty,
                        Role = newUser.Role ?? "SUB_USER",
                        Mobile = newUser.Mobile
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
                        Mobile = user.Mobile
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

                // Generate secure 6-digit OTP code
                var verificationCode = Random.Shared.Next(100000, 999999).ToString();
                var expiry = DateTime.UtcNow.AddMinutes(10);

                _resetCodes[cleanUsername] = (verificationCode, expiry);

                _logger.LogInformation("Generated verification code for {Username}: {Code} (Valid for 10 mins)", cleanUsername, verificationCode);

                return new RequestResetCodeResponse
                {
                    Success = true,
                    Message = $"Verification code generated successfully. Valid for 10 minutes.",
                    VerificationCode = verificationCode,
                    ExpiresInSeconds = 600
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
                var cleanCode = request.VerificationCode.Trim();

                if (!_resetCodes.TryGetValue(cleanUsername, out var stored) || stored.Expiry < DateTime.UtcNow)
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Verification code has expired or was not requested. Please request a new code."
                    };
                }

                if (stored.Code != cleanCode)
                {
                    return new ResetPasswordResponse
                    {
                        Success = false,
                        Message = "Invalid verification code. Please check and try again."
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

                // Consume verification code
                _resetCodes.TryRemove(cleanUsername, out _);

                _logger.LogInformation("Password reset successfully verified for user: {Username}", user.Username);

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