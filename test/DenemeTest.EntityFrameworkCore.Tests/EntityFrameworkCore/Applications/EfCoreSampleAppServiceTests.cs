using DenemeTest.Samples;
using Xunit;

namespace DenemeTest.EntityFrameworkCore.Applications;

[Collection(DenemeTestTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<DenemeTestEntityFrameworkCoreTestModule>
{

}
