using DenemeTest.Samples;
using Xunit;

namespace DenemeTest.EntityFrameworkCore.Domains;

[Collection(DenemeTestTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<DenemeTestEntityFrameworkCoreTestModule>
{

}
