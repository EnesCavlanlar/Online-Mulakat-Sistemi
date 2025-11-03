using DenemeTest.Localization;
using Volo.Abp.Application.Services;

namespace DenemeTest;

/* Inherit your application services from this class.
 */
public abstract class DenemeTestAppService : ApplicationService
{
    protected DenemeTestAppService()
    {
        LocalizationResource = typeof(DenemeTestResource);
    }
}
