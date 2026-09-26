using System.Threading.Tasks;
using KTransport.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KTransport.API.Controllers
{
    /// <summary>
    /// Read-only tenant branding for ANY authenticated user of the tenant (not just super users).
    /// The full ConfigurationController is super-user-only, which meant regular users never received
    /// their organization's logo/company details and their printed reports fell back to app defaults.
    /// This exposes only the safe branding subset (company identity + GSTIN/PAN that already appear on
    /// printed documents) — never feature flags, subscription, or billing rates.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BrandingController : ControllerBase
    {
        private readonly ITenantConfigurationService _configService;
        private readonly ITenantContext _tenantContext;

        public BrandingController(ITenantConfigurationService configService, ITenantContext tenantContext)
        {
            _configService = configService;
            _tenantContext = tenantContext;
        }

        [HttpGet]
        public async Task<ActionResult<object>> GetBranding()
        {
            var cfg = await _configService.GetTenantConfigurationAsync(_tenantContext.CurrentTenantId);

            return Ok(new
            {
                tenantId = cfg.TenantId,
                general = cfg.General,
                billingAndTax = new
                {
                    gstin = cfg.BillingAndTax.Gstin,
                    panNumber = cfg.BillingAndTax.PanNumber
                }
            });
        }
    }
}
