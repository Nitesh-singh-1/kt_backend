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
                EngineNo = request.EngineNo?.Trim(),
                ChassisNo = request.ChassisNo?.Trim(),
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
                Name = request.Name.Trim(),
                Mobile = request.Mobile?.Trim(),
                LicenseNo = request.LicenseNo?.Trim(),
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
            var loc = new Location
            {
                Code = request.Code.Trim().ToUpperInvariant(),
                Name = request.Name.Trim(),
                City = request.City?.Trim(),
                State = request.State?.Trim(),
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
