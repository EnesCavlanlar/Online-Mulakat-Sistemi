using DenemeTest.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace DenemeTest.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class DenemeTestController : AbpControllerBase
{
    protected DenemeTestController()
    {
        LocalizationResource = typeof(DenemeTestResource);
    }
}
