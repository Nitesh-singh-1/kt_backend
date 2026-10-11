using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KTransport.API.Common;
using KTransport.API.Controllers;
using KTransport.API.Data;
using KTransport.API.DTOs;
using KTransport.API.Models;
using KTransport.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KTransport.API.Tests.Controllers
{
    public class MenuCatalogAdminControllerTests
    {
        private static KTransportDbContext CreateInMemoryDb()
        {
            var options = new DbContextOptionsBuilder<KTransportDbContext>()
                .UseInMemoryDatabase(databaseName: $"MenuCatalogAdminTests_{Guid.NewGuid():N}")
                .Options;

            var ctx = new KTransportDbContext(options);
            int id = 1;
            foreach (var r in MenuCatalogSeedData.Rows)
            {
                ctx.MenuItems.Add(new MenuItem
                {
                    Id = id++,
                    Key = r.Key,
                    ParentKey = r.ParentKey,
                    Title = r.Title,
                    Path = r.Path,
                    Icon = r.Icon,
                    PermissionKey = r.PermissionKey,
                    Badge = r.Badge,
                    DisplayOrder = r.DisplayOrder,
                    VisibilityRule = r.VisibilityRule,
                    IsActive = true
                });
            }
            ctx.SaveChanges();
            return ctx;
        }

        [Fact]
        public async Task GetAll_ReturnsAllCatalogItems()
        {
            using var db = CreateInMemoryDb();
            var service = new MenuCatalogService(db, NullLogger<MenuCatalogService>.Instance);
            var controller = new MenuCatalogAdminController(db, service);

            var actionResult = await controller.GetAll();
            var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
            var items = Assert.IsAssignableFrom<List<MenuItemDto>>(okResult.Value);

            Assert.Equal(MenuCatalogSeedData.Rows.Length, items.Count);
        }

        [Fact]
        public async Task UpdateStatus_TogglesSingleMenuItemActiveStatus()
        {
            using var db = CreateInMemoryDb();
            var service = new MenuCatalogService(db, NullLogger<MenuCatalogService>.Instance);
            var controller = new MenuCatalogAdminController(db, service);

            var target = await db.MenuItems.FirstAsync(m => m.Key == "master_data.tyres");
            Assert.True(target.IsActive);

            var result = await controller.UpdateStatus(target.Id, new UpdateMenuItemStatusRequest { IsActive = false });
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var dto = Assert.IsType<MenuItemDto>(ok.Value);

            Assert.False(dto.IsActive);

            var updatedInDb = await db.MenuItems.FindAsync(target.Id);
            Assert.False(updatedInDb!.IsActive);
        }

        [Fact]
        public async Task BatchUpdateStatus_UpdatesMultipleItemsByKeys()
        {
            using var db = CreateInMemoryDb();
            var service = new MenuCatalogService(db, NullLogger<MenuCatalogService>.Instance);
            var controller = new MenuCatalogAdminController(db, service);

            var keys = new List<string> { "master_data.spares", "master_data.loans" };
            var result = await controller.BatchUpdateStatus(new BatchUpdateMenuItemStatusRequest
            {
                Keys = keys,
                IsActive = false
            });

            Assert.IsType<OkObjectResult>(result);

            var updatedItems = await db.MenuItems.Where(m => keys.Contains(m.Key)).ToListAsync();
            Assert.All(updatedItems, m => Assert.False(m.IsActive));
        }

        [Fact]
        public async Task HideIncompleteMasterData_HidesIncompleteAndKeepsPartiesAndFleetActive()
        {
            using var db = CreateInMemoryDb();
            var service = new MenuCatalogService(db, NullLogger<MenuCatalogService>.Instance);
            var controller = new MenuCatalogAdminController(db, service);

            var result = await controller.HideIncompleteMasterData();
            Assert.IsType<OkObjectResult>(result);

            var masterItems = await db.MenuItems.Where(m => m.ParentKey == "master_data").ToListAsync();
            var activeItems = masterItems.Where(m => m.IsActive).Select(m => m.Key).ToList();
            var hiddenItems = masterItems.Where(m => !m.IsActive).Select(m => m.Key).ToList();

            Assert.Contains("master_data.parties", activeItems);
            Assert.Contains("master_data.fleet", activeItems);
            Assert.Equal(2, activeItems.Count);

            Assert.Equal(8, hiddenItems.Count);
            Assert.Contains("master_data.tyres", hiddenItems);
            Assert.Contains("master_data.spares", hiddenItems);
            Assert.Contains("master_data.driver_ledger", hiddenItems);
        }

        [Fact]
        public async Task ResetMasterData_ReactivatesAllMasterDataItems()
        {
            using var db = CreateInMemoryDb();
            var service = new MenuCatalogService(db, NullLogger<MenuCatalogService>.Instance);
            var controller = new MenuCatalogAdminController(db, service);

            // First hide incomplete
            await controller.HideIncompleteMasterData();

            // Then reset
            var result = await controller.ResetMasterData();
            Assert.IsType<OkObjectResult>(result);

            var masterItems = await db.MenuItems.Where(m => m.ParentKey == "master_data").ToListAsync();
            Assert.All(masterItems, m => Assert.True(m.IsActive));
        }
    }
}
