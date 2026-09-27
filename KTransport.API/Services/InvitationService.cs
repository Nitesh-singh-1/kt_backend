using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace KTransport.API.Services
{
    public class InvitationService : IInvitationService
    {
        private const int InviteValidDays = 7;

        private readonly KTransportDbContext _context;
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly IFeatureAuthorizationService _featureAuth;
        private readonly IEmailSender _emailSender;
        private readonly IAuditLogService _auditLogService;
        private readonly string _frontendBaseUrl;
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        public InvitationService(
            KTransportDbContext context,
            IUserService userService,
            IAuthService authService,
            IFeatureAuthorizationService featureAuth,
            IEmailSender emailSender,
            IAuditLogService auditLogService,
            IConfiguration configuration)
        {
            _context = context;
            _userService = userService;
            _authService = authService;
            _featureAuth = featureAuth;
            _emailSender = emailSender;
            _auditLogService = auditLogService;

            _frontendBaseUrl = (configuration["App:FrontendBaseUrl"]
                ?? configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()?.FirstOrDefault()
                ?? "http://localhost:3000").TrimEnd('/');
        }

        public async Task<(bool Success, string Message, InviteDto? Data)> CreateInviteAsync(Guid tenantId, int? invitedByUserId, CreateInviteRequest request)
        {
            var email = request.Email.Trim().ToLowerInvariant();

            // Don't invite someone who is already a user of this tenant.
            var existingUser = await _context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.TenantId == tenantId && u.Email != null && u.Email.ToLower() == email);
            if (existingUser)
            {
                return (false, "A user with this email already exists in your organization.", null);
            }

            // Only allow assigning features the organization is actually subscribed to.
            var subscribed = await _featureAuth.GetSubscribedFeaturesAsync(tenantId);
            var validAssigned = new List<string>();
            foreach (var feat in request.AssignedFeatures ?? new List<string>())
            {
                var canon = FeatureConstants.Normalize(feat);
                if (subscribed.Contains(feat) || subscribed.Contains(canon))
                {
                    validAssigned.Add(feat);
                }
                else
                {
                    return (false, $"Cannot assign feature '{feat}' — your organization has not subscribed to it.", null);
                }
            }

            // Constrain role to admin/standard.
            var role = string.Equals(request.Role, "admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "SUB_USER";

            // Supersede any existing pending invite for the same email.
            var priorPending = await _context.Invitations
                .Where(i => i.TenantId == tenantId && i.Email == email && i.Status == "Pending")
                .ToListAsync();
            foreach (var p in priorPending) p.Status = "Revoked";

            var invite = new Invitation
            {
                TenantId = tenantId,
                Email = email,
                Role = role,
                AssignedFeaturesJson = JsonSerializer.Serialize(validAssigned),
                Token = GenerateToken(),
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddDays(InviteValidDays),
                InvitedByUserId = invitedByUserId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Invitations.Add(invite);
            await _context.SaveChangesAsync();

            var acceptUrl = $"{_frontendBaseUrl}/accept-invite?token={invite.Token}";

            // Send the invite email (best effort).
            var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId);
            var inviterName = invitedByUserId.HasValue
                ? (await _context.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == invitedByUserId.Value))?.FullName ?? ""
                : "";
            var brandName = tenant?.Name ?? "your organization";
            var (subject, html) = EmailTemplates.TeamInvite(brandName, inviterName ?? "", acceptUrl, InviteValidDays);
            var emailSent = await _emailSender.SendAsync(email, subject, html, brandName);

            await _auditLogService.LogAsync("UserInvited", tenantId: tenantId, userId: invitedByUserId, entityType: "Invitation", entityId: invite.Id.ToString(), details: $"Invited {email} as {role}");

            return (true, emailSent ? "Invitation sent." : "Invitation created (email delivery disabled — share the link manually).", new InviteDto
            {
                Id = invite.Id,
                Email = invite.Email,
                Role = invite.Role,
                AssignedFeatures = validAssigned,
                Status = invite.Status,
                ExpiresAt = invite.ExpiresAt,
                CreatedAt = invite.CreatedAt,
                EmailSent = emailSent,
                AcceptUrl = (_emailSender.IsEnabled && emailSent) ? null : acceptUrl
            });
        }

        public async Task<List<InviteDto>> GetInvitesAsync(Guid tenantId)
        {
            var invites = await _context.Invitations
                .Where(i => i.TenantId == tenantId)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return invites.Select(i => new InviteDto
            {
                Id = i.Id,
                Email = i.Email,
                Role = i.Role,
                AssignedFeatures = DeserializeFeatures(i.AssignedFeaturesJson),
                Status = i.ExpiresAt < DateTime.UtcNow && i.Status == "Pending" ? "Expired" : i.Status,
                ExpiresAt = i.ExpiresAt,
                CreatedAt = i.CreatedAt,
                EmailSent = true
            }).ToList();
        }

        public async Task<(bool Success, string Message)> RevokeInviteAsync(Guid tenantId, long inviteId)
        {
            var invite = await _context.Invitations.FirstOrDefaultAsync(i => i.TenantId == tenantId && i.Id == inviteId);
            if (invite == null) return (false, "Invitation not found.");
            if (invite.Status == "Accepted") return (false, "This invitation has already been accepted.");

            invite.Status = "Revoked";
            await _context.SaveChangesAsync();
            return (true, "Invitation revoked.");
        }

        public async Task<InviteInfoDto> GetInviteInfoByTokenAsync(string token)
        {
            var invite = await _context.Invitations.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Token == token);
            if (invite == null)
            {
                return new InviteInfoDto { Valid = false, Message = "This invitation link is invalid." };
            }
            if (invite.Status == "Revoked")
            {
                return new InviteInfoDto { Valid = false, Message = "This invitation has been revoked." };
            }
            if (invite.Status == "Accepted")
            {
                return new InviteInfoDto { Valid = false, Message = "This invitation has already been used." };
            }
            if (invite.ExpiresAt < DateTime.UtcNow)
            {
                return new InviteInfoDto { Valid = false, Message = "This invitation has expired. Ask your administrator to send a new one." };
            }

            var tenant = await _context.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == invite.TenantId);
            return new InviteInfoDto
            {
                Valid = true,
                Email = invite.Email,
                OrganizationName = tenant?.Name,
                Role = invite.Role
            };
        }

        public async Task<AuthResponse> AcceptInviteAsync(AcceptInviteRequest request)
        {
            var invite = await _context.Invitations.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.Token == request.Token);

            if (invite == null || invite.Status == "Revoked")
                return new AuthResponse { Success = false, Message = "This invitation is invalid or has been revoked." };
            if (invite.Status == "Accepted")
                return new AuthResponse { Success = false, Message = "This invitation has already been used." };
            if (invite.ExpiresAt < DateTime.UtcNow)
                return new AuthResponse { Success = false, Message = "This invitation has expired." };

            var createResult = await _userService.CreateSubUserAsync(invite.TenantId, new CreateSubUserRequest
            {
                Username = request.Username.Trim(),
                Password = request.Password,
                FullName = request.FullName.Trim(),
                Mobile = request.Mobile?.Trim(),
                Email = invite.Email,
                Role = invite.Role,
                AssignedFeatures = DeserializeFeatures(invite.AssignedFeaturesJson)
            });

            if (!createResult.Success)
            {
                return new AuthResponse { Success = false, Message = createResult.Message };
            }

            invite.Status = "Accepted";
            invite.AcceptedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await _auditLogService.LogAsync("InviteAccepted", tenantId: invite.TenantId, username: request.Username.Trim(), entityType: "Invitation", entityId: invite.Id.ToString());

            // Auto-login the new user by reusing the standard login path.
            return await _authService.LoginAsync(new LoginRequest { Username = request.Username.Trim(), Password = request.Password });
        }

        private static List<string> DeserializeFeatures(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<string>();
            try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>(); }
            catch { return new List<string>(); }
        }

        private static string GenerateToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        }
    }
}
