using Microsoft.Extensions.Localization;
using DenemeTest.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace DenemeTest.Blazor;

[Dependency(ReplaceServices = true)]
public class DenemeTestBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<DenemeTestResource> _localizer;

    public DenemeTestBrandingProvider(IStringLocalizer<DenemeTestResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
