using DenemeTest.Localization;
using Volo.Abp.AspNetCore.Components;

namespace DenemeTest.Blazor;

public abstract class DenemeTestComponentBase : AbpComponentBase
{
    protected DenemeTestComponentBase()
    {
        LocalizationResource = typeof(DenemeTestResource);
    }
}
