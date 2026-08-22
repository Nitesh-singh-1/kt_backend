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

                // Find user by username
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username == request.Username && u.IsActive == true);

                if (user == null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Invalid username or password"
                    };
                }

                // Verify plain text password
                if (request.Password != user.Password)
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
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                if (existingUser != null)
                {
                    return new AuthResponse
                    {
                        Success = false,
                        Message = "Username already exists"
                    };
                }

                // Create new user with plain text password
                var newUser = new User
                {
                    Username = request.Username,
                    Password = request.Password,
                    FullName = request.FullName,
                    Mobile = request.Mobile,
                    Role = request.Role,
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
                        Role = newUser.Role ?? "User",
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

                var user = await _context.Users.FindAsync(int.Parse(userIdClaim));
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
                        Role = user.Role ?? "User",
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

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Secret);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role ?? "User"),
                new Claim("FullName", user.FullName ?? string.Empty)
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