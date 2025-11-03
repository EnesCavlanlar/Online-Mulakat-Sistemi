using Xunit;

namespace DenemeTest.EntityFrameworkCore;

[CollectionDefinition(DenemeTestTestConsts.CollectionDefinitionName)]
public class DenemeTestEntityFrameworkCoreCollection : ICollectionFixture<DenemeTestEntityFrameworkCoreFixture>
{

}
