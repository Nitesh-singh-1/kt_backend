using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;

namespace KTransport.API.Services
{
    public class UserService : IUserService
    {
        private readonly KTransportDbContext _context;
        private readonly IFeatureAuthorizationService _authService;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public UserService(KTransportDbContext context, IFeatureAuthorizationService authService)
        {
            _context = context;
            _authService = authService;
        }

        public async Task<List<SubUserDetailsDto>> GetTenantUsersAsync(Guid tenantId)
        {
            var users = await _context.Users
                .Where(u => u.TenantId == tenantId)
                .OrderBy(u => u.FullName)
                .ToListAsync();

            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            var userOverrides = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (setting != null && !string.IsNullOrWhiteSpace(setting.MenuEntitlementsJson))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<TenantMenuEntitlementsDto>(setting.MenuEntitlementsJson, JsonOptions);
                    if (parsed != null && !string.IsNullOrWhiteSpace(parsed.UserOverridesJson))
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(parsed.UserOverridesJson, JsonOptions);
                        if (dict != null) userOverrides = new Dictionary<string, List<string>>(dict, StringComparer.OrdinalIgnoreCase);
                    }
                }
                catch { }
            }

            var subscribedFeatures = await _authService.GetSubscribedFeaturesAsync(tenantId);
            var result = new List<SubUserDetailsDto>();

            foreach (var u in users)
            {
                var assigned = new List<string>();
                var key = u.Username;
                var idKey = u.Id.ToString();

                if (userOverrides.TryGetValue(idKey, out var valById)) assigned = valById;
                else if (userOverrides.TryGetValue(key, out var valByName)) assigned = valByName;

                var effective = await _authService.GetEffectiveFeaturesForUserAsync(tenantId, u.Role ?? "SUB_USER", u.Username);

                result.Add(new SubUserDetailsDto
                {
                    Id = u.Id,
                    TenantId = u.TenantId,
                    Username = u.Username,
                    FullName = u.FullName ?? string.Empty,
                    Role = u.Role ?? "SUB_USER",
                    Mobile = u.Mobile,
                    IsActive = u.IsActive ?? true,
                    CreatedAt = u.CreatedAt,
                    AssignedFeatures = assigned ?? new List<string>(),
                    EffectiveFeatures = effective.ToList()
                });
            }

            return result;
        }

        public async Task<SubUserDetailsDto?> GetUserByIdAsync(Guid tenantId, int userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null) return null;

            var effective = await _authService.GetEffectiveFeaturesForUserAsync(tenantId, user.Role ?? "SUB_USER", user.Username);

            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            var assigned = new List<string>();
            if (setting != null && !string.IsNullOrWhiteSpace(setting.MenuEntitlementsJson))
            {
                try
                {
                    var parsed = JsonSerializer.Deserialize<TenantMenuEntitlementsDto>(setting.MenuEntitlementsJson, JsonOptions);
                    if (parsed != null && !string.IsNullOrWhiteSpace(parsed.UserOverridesJson))
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(parsed.UserOverridesJson, JsonOptions);
                        if (dict != null)
                        {
                            if (dict.TryGetValue(user.Id.ToString(), out var byId)) assigned = byId;
                            else if (dict.TryGetValue(user.Username, out var byName)) assigned = byName;
                        }
                    }
                }
                catch { }
            }

            return new SubUserDetailsDto
            {
                Id = user.Id,
                TenantId = user.TenantId,
                Username = user.Username,
                FullName = user.FullName ?? string.Empty,
                Role = user.Role ?? "SUB_USER",
                Mobile = user.Mobile,
                IsActive = user.IsActive ?? true,
                CreatedAt = user.CreatedAt,
                AssignedFeatures = assigned,
                EffectiveFeatures = effective.ToList()
            };
        }

        public async Task<(bool Success, string Message, SubUserDetailsDto? Data)> CreateSubUserAsync(Guid tenantId, CreateSubUserRequest request)
        {
            // 1. Check if username already exists
            var existing = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.Trim().ToLower());

            if (existing != null)
            {
                return (false, $"Username '{request.Username}' is already taken.", null);
            }

            // 2. Validate assigned features against organization's active subscription
            var subscribedFeatures = await _authService.GetSubscribedFeaturesAsync(tenantId);
            var validAssigned = new List<string>();

            if (request.AssignedFeatures != null && request.AssignedFeatures.Count > 0)
            {
                foreach (var feat in request.AssignedFeatures)
                {
                    var canon = FeatureConstants.Normalize(feat);
                    if (subscribedFeatures.Contains(feat) || subscribedFeatures.Contains(canon))
                    {
                        validAssigned.Add(feat);
                    }
                    else
                    {
                        return (false, $"Cannot assign feature '{feat}' because the organization has not subscribed to it.", null);
                    }
                }
            }

            // 3. Create User entity
            var newUser = new User
            {
                TenantId = tenantId,
                Username = request.Username.Trim(),
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                FullName = request.FullName.Trim(),
                Mobile = request.Mobile?.Trim(),
                Role = string.IsNullOrWhiteSpace(request.Role) ? "SUB_USER" : request.Role,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            // 4. Save assigned feature overrides in TenantSettings
            await SaveUserFeatureAssignmentsAsync(tenantId, newUser.Id, newUser.Username, validAssigned);

            var effective = await _authService.GetEffectiveFeaturesForUserAsync(tenantId, newUser.Role, newUser.Username);

            var result = new SubUserDetailsDto
            {
                Id = newUser.Id,
                TenantId = newUser.TenantId,
                Username = newUser.Username,
                FullName = newUser.FullName ?? string.Empty,
                Role = newUser.Role ?? "SUB_USER",
                Mobile = newUser.Mobile,
                IsActive = newUser.IsActive ?? true,
                CreatedAt = newUser.CreatedAt,
                AssignedFeatures = validAssigned,
                EffectiveFeatures = effective.ToList()
            };

            return (true, "Sub-user created successfully.", result);
        }

        public async Task<(bool Success, string Message, SubUserDetailsDto? Data)> UpdateSubUserAsync(Guid tenantId, int userId, UpdateSubUserRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null)
            {
                return (false, "User not found.", null);
            }

            if (!string.IsNullOrWhiteSpace(request.FullName)) user.FullName = request.FullName.Trim();
            if (request.Mobile != null) user.Mobile = request.Mobile.Trim();
            if (!string.IsNullOrWhiteSpace(request.Role)) user.Role = request.Role.Trim();
            if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

            await _context.SaveChangesAsync();

            return (true, "User updated successfully.", await GetUserByIdAsync(tenantId, userId));
        }

        public async Task<(bool Success, string Message)> UpdateUserPermissionsAsync(Guid tenantId, int userId, UpdateUserPermissionsRequest request)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null)
            {
                return (false, "User not found.");
            }

            // Validate against organization subscription
            var subscribedFeatures = await _authService.GetSubscribedFeaturesAsync(tenantId);
            var validAssigned = new List<string>();

            if (request.AssignedFeatures != null)
            {
                foreach (var feat in request.AssignedFeatures)
                {
                    var canon = FeatureConstants.Normalize(feat);
                    if (subscribedFeatures.Contains(feat) || subscribedFeatures.Contains(canon))
                    {
                        validAssigned.Add(feat);
                    }
                    else
                    {
                        return (false, $"Cannot assign feature '{feat}' because the organization has not subscribed to it.");
                    }
                }
            }

            await SaveUserFeatureAssignmentsAsync(tenantId, user.Id, user.Username, validAssigned);
            return (true, "User permissions updated successfully.");
        }

        public async Task<(bool Success, string Message)> ToggleUserStatusAsync(Guid tenantId, int userId, bool isActive)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null) return (false, "User not found.");

            user.IsActive = isActive;
            await _context.SaveChangesAsync();

            return (true, $"User {(isActive ? "activated" : "deactivated")} successfully.");
        }

        public async Task<(bool Success, string Message)> DeleteUserAsync(Guid tenantId, int userId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId);

            if (user == null) return (false, "User not found.");

            // Soft-deactivate user
            user.IsActive = false;
            await _context.SaveChangesAsync();

            return (true, "User deactivated successfully.");
        }

        private async Task SaveUserFeatureAssignmentsAsync(Guid tenantId, int userId, string username, List<string> features)
        {
            var setting = await _context.TenantSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId);

            TenantMenuEntitlementsDto dto;
            if (setting != null && !string.IsNullOrWhiteSpace(setting.MenuEntitlementsJson) && setting.MenuEntitlementsJson != "{}")
            {
                try
                {
                    dto = JsonSerializer.Deserialize<TenantMenuEntitlementsDto>(setting.MenuEntitlementsJson, JsonOptions) 
                          ?? new TenantMenuEntitlementsDto { TenantId = tenantId };
                }
                catch
                {
                    dto = new TenantMenuEntitlementsDto { TenantId = tenantId };
                }
            }
            else
            {
                dto = new TenantMenuEntitlementsDto { TenantId = tenantId };
            }

            var overrides = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrWhiteSpace(dto.UserOverridesJson))
            {
                try
                {
                    var existing = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(dto.UserOverridesJson, JsonOptions);
                    if (existing != null) overrides = new Dictionary<string, List<string>>(existing, StringComparer.OrdinalIgnoreCase);
                }
                catch { }
            }

            // Save under both username and ID
            overrides[username] = features;
            overrides[userId.ToString()] = features;

            dto.UserOverridesJson = JsonSerializer.Serialize(overrides, JsonOptions);

            var json = JsonSerializer.Serialize(dto, JsonOptions);

            if (setting == null)
            {
                setting = new TenantSetting
                {
                    TenantId = tenantId,
                    MenuEntitlementsJson = json,
                    CreatedAt = DateTime.UtcNow
                };
                _context.TenantSettings.Add(setting);
            }
            else
            {
                setting.MenuEntitlementsJson = json;
                setting.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
        }
    }
}
