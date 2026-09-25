using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KTransport.API.Services
{
    public class FleetService : IFleetService
    {
        private readonly KTransportDbContext _context;
        private readonly ILogger<FleetService> _logger;

        public FleetService(KTransportDbContext context, ILogger<FleetService> logger)
        {
            _context = context;
            _logger = logger;
        }

        // Vehicles
        public async Task<List<VehicleDto>> GetVehiclesAsync(string? search = null)
        {
            var query = _context.Vehicles.AsNoTracking().Where(v => v.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(v =>
                    v.VehicleNo.ToLower().Contains(s) ||
                    (v.VehicleType != null && v.VehicleType.ToLower().Contains(s)) ||
                    (v.OwnerType != null && v.OwnerType.ToLower().Contains(s))
                );
            }

            var vehicles = await query.OrderBy(v => v.VehicleNo).ToListAsync();
            return vehicles.Select(v => new VehicleDto
            {
                Id = v.Id,
                TenantId = v.TenantId,
                VehicleNo = v.VehicleNo,
                VehicleType = v.VehicleType,
                OwnerType = v.OwnerType,
                CapacityTons = v.CapacityTons,
                EngineNo = v.EngineNo,
                ChassisNo = v.ChassisNo,
                FitnessValidUntil = v.FitnessValidUntil,
                InsuranceValidUntil = v.InsuranceValidUntil,
                PermitValidUntil = v.PermitValidUntil,
                IsActive = v.IsActive,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            }).ToList();
        }

        public async Task<List<VehicleLookupDto>> GetVehicleLookupAsync(string? query = null)
        {
            var dbQuery = _context.Vehicles.AsNoTracking().Where(v => v.IsActive);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var s = query.Trim().ToLower();
                dbQuery = dbQuery.Where(v => v.VehicleNo.ToLower().Contains(s));
            }

            return await dbQuery
                .OrderBy(v => v.VehicleNo)
                .Take(50)
                .Select(v => new VehicleLookupDto
                {
                    Id = v.Id,
                    VehicleNo = v.VehicleNo,
                    VehicleType = v.VehicleType,
                    OwnerType = v.OwnerType,
                    CapacityTons = v.CapacityTons
                })
                .ToListAsync();
        }

        public async Task<VehicleDto?> GetVehicleByIdAsync(long id)
        {
            var v = await _context.Vehicles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (v == null) return null;

            return new VehicleDto
            {
                Id = v.Id,
                TenantId = v.TenantId,
                VehicleNo = v.VehicleNo,
                VehicleType = v.VehicleType,
                OwnerType = v.OwnerType,
                CapacityTons = v.CapacityTons,
                EngineNo = v.EngineNo,
                ChassisNo = v.ChassisNo,
                FitnessValidUntil = v.FitnessValidUntil,
                InsuranceValidUntil = v.InsuranceValidUntil,
                PermitValidUntil = v.PermitValidUntil,
                IsActive = v.IsActive,
                CreatedAt = v.CreatedAt,
                UpdatedAt = v.UpdatedAt
            };
        }

        public async Task<VehicleDto> CreateVehicleAsync(CreateVehicleRequest request)
        {
            var vehicle = new Vehicle
            {
                VehicleNo = request.VehicleNo.Trim().ToUpperInvariant(),
                VehicleType = request.VehicleType?.Trim(),
                OwnerType = request.OwnerType?.Trim(),
                CapacityTons = request.CapacityTons,
                EngineNo = request.EngineNo?.Trim().ToUpperInvariant(),
                ChassisNo = request.ChassisNo?.Trim().ToUpperInvariant(),
                FitnessValidUntil = request.FitnessValidUntil,
                InsuranceValidUntil = request.InsuranceValidUntil,
                PermitValidUntil = request.PermitValidUntil,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Vehicles.Add(vehicle);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created vehicle {VehicleNo} (ID: {Id})", vehicle.VehicleNo, vehicle.Id);
            return new VehicleDto
            {
                Id = vehicle.Id,
                TenantId = vehicle.TenantId,
                VehicleNo = vehicle.VehicleNo,
                VehicleType = vehicle.VehicleType,
                OwnerType = vehicle.OwnerType,
                CapacityTons = vehicle.CapacityTons,
                EngineNo = vehicle.EngineNo,
                ChassisNo = vehicle.ChassisNo,
                FitnessValidUntil = vehicle.FitnessValidUntil,
                InsuranceValidUntil = vehicle.InsuranceValidUntil,
                PermitValidUntil = vehicle.PermitValidUntil,
                IsActive = vehicle.IsActive,
                CreatedAt = vehicle.CreatedAt
            };
        }

        public async Task<VehicleDto?> UpdateVehicleAsync(long id, UpdateVehicleRequest request)
        {
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id);
            if (vehicle == null) return null;

            if (!string.IsNullOrWhiteSpace(request.VehicleNo))
                vehicle.VehicleNo = request.VehicleNo.Trim().ToUpperInvariant();
            if (request.VehicleType != null)
                vehicle.VehicleType = request.VehicleType.Trim();
            if (request.OwnerType != null)
                vehicle.OwnerType = request.OwnerType.Trim();
            if (request.CapacityTons.HasValue)
                vehicle.CapacityTons = request.CapacityTons.Value;
            if (request.EngineNo != null)
                vehicle.EngineNo = request.EngineNo.Trim().ToUpperInvariant();
            if (request.ChassisNo != null)
                vehicle.ChassisNo = request.ChassisNo.Trim().ToUpperInvariant();
            if (request.FitnessValidUntil.HasValue)
                vehicle.FitnessValidUntil = request.FitnessValidUntil;
            if (request.InsuranceValidUntil.HasValue)
                vehicle.InsuranceValidUntil = request.InsuranceValidUntil;
            if (request.PermitValidUntil.HasValue)
                vehicle.PermitValidUntil = request.PermitValidUntil;
            if (request.IsActive.HasValue)
                vehicle.IsActive = request.IsActive.Value;

            vehicle.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated vehicle {VehicleNo} (ID: {Id})", vehicle.VehicleNo, vehicle.Id);
            return new VehicleDto
            {
                Id = vehicle.Id,
                TenantId = vehicle.TenantId,
                VehicleNo = vehicle.VehicleNo,
                VehicleType = vehicle.VehicleType,
                OwnerType = vehicle.OwnerType,
                CapacityTons = vehicle.CapacityTons,
                EngineNo = vehicle.EngineNo,
                ChassisNo = vehicle.ChassisNo,
                FitnessValidUntil = vehicle.FitnessValidUntil,
                InsuranceValidUntil = vehicle.InsuranceValidUntil,
                PermitValidUntil = vehicle.PermitValidUntil,
                IsActive = vehicle.IsActive,
                CreatedAt = vehicle.CreatedAt,
                UpdatedAt = vehicle.UpdatedAt
            };
        }

        public async Task<bool> DeleteVehicleAsync(long id)
        {
            var vehicle = await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id);
            if (vehicle == null) return false;

            vehicle.IsActive = false;
            vehicle.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        // Drivers
        public async Task<List<DriverDto>> GetDriversAsync(string? search = null)
        {
            var query = _context.Drivers.AsNoTracking().Where(d => d.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(d =>
                    d.Name.ToLower().Contains(s) ||
                    (d.Mobile != null && d.Mobile.Contains(s)) ||
                    (d.LicenseNo != null && d.LicenseNo.ToLower().Contains(s))
                );
            }

            var drivers = await query.OrderBy(d => d.Name).ToListAsync();
            return drivers.Select(d => new DriverDto
            {
                Id = d.Id,
                TenantId = d.TenantId,
                Name = d.Name,
                Mobile = d.Mobile,
                LicenseNo = d.LicenseNo,
                LicenseValidUntil = d.LicenseValidUntil,
                AadharNo = d.AadharNo,
                Address = d.Address,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToList();
        }

        public async Task<List<DriverLookupDto>> GetDriverLookupAsync(string? query = null)
        {
            var dbQuery = _context.Drivers.AsNoTracking().Where(d => d.IsActive);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var s = query.Trim().ToLower();
                dbQuery = dbQuery.Where(d =>
                    d.Name.ToLower().Contains(s) ||
                    (d.Mobile != null && d.Mobile.Contains(s))
                );
            }

            return await dbQuery
                .OrderBy(d => d.Name)
                .Take(50)
                .Select(d => new DriverLookupDto
                {
                    Id = d.Id,
                    Name = d.Name,
                    Mobile = d.Mobile,
                    LicenseNo = d.LicenseNo
                })
                .ToListAsync();
        }

        public async Task<DriverDto?> GetDriverByIdAsync(long id)
        {
            var d = await _context.Drivers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (d == null) return null;

            return new DriverDto
            {
                Id = d.Id,
                TenantId = d.TenantId,
                Name = d.Name,
                Mobile = d.Mobile,
                LicenseNo = d.LicenseNo,
                LicenseValidUntil = d.LicenseValidUntil,
                AadharNo = d.AadharNo,
                Address = d.Address,
                IsActive = d.IsActive,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            };
        }

        public async Task<DriverDto> CreateDriverAsync(CreateDriverRequest request)
        {
            var driver = new Driver
            {
                Name = ToTitleCase(request.Name),
                Mobile = request.Mobile?.Trim(),
                LicenseNo = request.LicenseNo?.Trim().ToUpperInvariant(),
                LicenseValidUntil = request.LicenseValidUntil,
                AadharNo = request.AadharNo?.Trim(),
                Address = request.Address?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Drivers.Add(driver);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created driver {Name} (ID: {Id})", driver.Name, driver.Id);
            return new DriverDto
            {
                Id = driver.Id,
                TenantId = driver.TenantId,
                Name = driver.Name,
                Mobile = driver.Mobile,
                LicenseNo = driver.LicenseNo,
                LicenseValidUntil = driver.LicenseValidUntil,
                AadharNo = driver.AadharNo,
                Address = driver.Address,
                IsActive = driver.IsActive,
                CreatedAt = driver.CreatedAt
            };
        }

        public async Task<DriverDto?> UpdateDriverAsync(long id, UpdateDriverRequest request)
        {
            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == id);
            if (driver == null) return null;

            if (!string.IsNullOrWhiteSpace(request.Name))
                driver.Name = ToTitleCase(request.Name);
            if (request.Mobile != null)
                driver.Mobile = request.Mobile.Trim();
            if (request.LicenseNo != null)
                driver.LicenseNo = request.LicenseNo.Trim().ToUpperInvariant();
            if (request.LicenseValidUntil.HasValue)
                driver.LicenseValidUntil = request.LicenseValidUntil;
            if (request.AadharNo != null)
                driver.AadharNo = request.AadharNo.Trim();
            if (request.Address != null)
                driver.Address = request.Address.Trim();
            if (request.IsActive.HasValue)
                driver.IsActive = request.IsActive.Value;

            driver.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated driver {Name} (ID: {Id})", driver.Name, driver.Id);
            return new DriverDto
            {
                Id = driver.Id,
                TenantId = driver.TenantId,
                Name = driver.Name,
                Mobile = driver.Mobile,
                LicenseNo = driver.LicenseNo,
                LicenseValidUntil = driver.LicenseValidUntil,
                AadharNo = driver.AadharNo,
                Address = driver.Address,
                IsActive = driver.IsActive,
                CreatedAt = driver.CreatedAt,
                UpdatedAt = driver.UpdatedAt
            };
        }

        public async Task<bool> DeleteDriverAsync(long id)
        {
            var driver = await _context.Drivers.FirstOrDefaultAsync(d => d.Id == id);
            if (driver == null) return false;

            driver.IsActive = false;
            driver.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        // Locations
        public async Task<List<LocationDto>> GetLocationsAsync(string? search = null)
        {
            var query = _context.Locations.AsNoTracking().Where(l => l.IsActive);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(l =>
                    l.Name.ToLower().Contains(s) ||
                    l.Code.ToLower().Contains(s) ||
                    (l.City != null && l.City.ToLower().Contains(s)) ||
                    (l.State != null && l.State.ToLower().Contains(s))
                );
            }

            var locations = await query.OrderBy(l => l.Name).ToListAsync();
            return locations.Select(l => new LocationDto
            {
                Id = l.Id,
                TenantId = l.TenantId,
                Code = l.Code,
                Name = l.Name,
                City = l.City,
                State = l.State,
                Address = l.Address,
                Pincode = l.Pincode,
                IsActive = l.IsActive,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            }).ToList();
        }

        public async Task<List<LocationLookupDto>> GetLocationLookupAsync(string? query = null)
        {
            var dbQuery = _context.Locations.AsNoTracking().Where(l => l.IsActive);

            if (!string.IsNullOrWhiteSpace(query))
            {
                var s = query.Trim().ToLower();
                dbQuery = dbQuery.Where(l =>
                    l.Name.ToLower().Contains(s) ||
                    l.Code.ToLower().Contains(s) ||
                    (l.City != null && l.City.ToLower().Contains(s))
                );
            }

            return await dbQuery
                .OrderBy(l => l.Name)
                .Take(50)
                .Select(l => new LocationLookupDto
                {
                    Id = l.Id,
                    Code = l.Code,
                    Name = l.Name,
                    City = l.City,
                    State = l.State
                })
                .ToListAsync();
        }

        public async Task<LocationDto?> GetLocationByIdAsync(long id)
        {
            var l = await _context.Locations.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
            if (l == null) return null;

            return new LocationDto
            {
                Id = l.Id,
                TenantId = l.TenantId,
                Code = l.Code,
                Name = l.Name,
                City = l.City,
                State = l.State,
                Address = l.Address,
                Pincode = l.Pincode,
                IsActive = l.IsActive,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt
            };
        }

        public async Task<LocationDto> CreateLocationAsync(CreateLocationRequest request)
        {
            var code = request.Code?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                code = GenerateLocationCode(request.Name);
            }

            var loc = new Location
            {
                Code = code.ToUpperInvariant(),
                Name = ToTitleCase(request.Name),
                City = request.City != null ? ToTitleCase(request.City) : null,
                State = request.State != null ? ToTitleCase(request.State) : null,
                Address = request.Address?.Trim(),
                Pincode = request.Pincode?.Trim(),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            _context.Locations.Add(loc);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created location {Code} - {Name} (ID: {Id})", loc.Code, loc.Name, loc.Id);
            return new LocationDto
            {
                Id = loc.Id,
                TenantId = loc.TenantId,
                Code = loc.Code,
                Name = loc.Name,
                City = loc.City,
                State = loc.State,
                Address = loc.Address,
                Pincode = loc.Pincode,
                IsActive = loc.IsActive,
                CreatedAt = loc.CreatedAt
            };
        }

        public async Task<LocationDto?> UpdateLocationAsync(long id, UpdateLocationRequest request)
        {
            var loc = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
            if (loc == null) return null;

            if (!string.IsNullOrWhiteSpace(request.Code))
                loc.Code = request.Code.Trim().ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(request.Name))
                loc.Name = ToTitleCase(request.Name);
            if (request.City != null)
                loc.City = ToTitleCase(request.City);
            if (request.State != null)
                loc.State = ToTitleCase(request.State);
            if (request.Address != null)
                loc.Address = request.Address.Trim();
            if (request.Pincode != null)
                loc.Pincode = request.Pincode.Trim();
            if (request.IsActive.HasValue)
                loc.IsActive = request.IsActive.Value;

            loc.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated location {Code} - {Name} (ID: {Id})", loc.Code, loc.Name, loc.Id);
            return new LocationDto
            {
                Id = loc.Id,
                TenantId = loc.TenantId,
                Code = loc.Code,
                Name = loc.Name,
                City = loc.City,
                State = loc.State,
                Address = loc.Address,
                Pincode = loc.Pincode,
                IsActive = loc.IsActive,
                CreatedAt = loc.CreatedAt,
                UpdatedAt = loc.UpdatedAt
            };
        }

        private static string ToTitleCase(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(input.Trim().ToLowerInvariant());
        }

        private static string GenerateLocationCode(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "HUB-01";
            var clean = System.Text.RegularExpressions.Regex.Replace(name, @"[^a-zA-Z0-9\s]", "").Trim();
            var words = clean.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 1)
            {
                var w = words[0].ToUpperInvariant();
                return w.Length <= 4 ? w : w[..4];
            }
            if (words.Length == 2)
            {
                var w1 = words[0].ToUpperInvariant();
                var w2 = words[1].ToUpperInvariant();
                var p1 = w1.Length <= 3 ? w1 : w1[..3];
                var p2 = w2.Length <= 3 ? w2 : w2[..3];
                return $"{p1}-{p2}";
            }
            var initials = string.Concat(words.Take(3).Select(w => char.ToUpperInvariant(w[0])));
            return $"{initials}-HUB";
        }

        public async Task<bool> DeleteLocationAsync(long id)
        {
            var loc = await _context.Locations.FirstOrDefaultAsync(l => l.Id == id);
            if (loc == null) return false;

            loc.IsActive = false;
            loc.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
