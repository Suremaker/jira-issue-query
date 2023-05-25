using JiraIssueQuery.Api.Clients;
using JiraIssueQuery.Api.Mappers;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;
using Xunit;

namespace JiraIssueQuery.Api.UnitTests;

public class ReferenceDataMapperTests
{
    private readonly ReferenceDataMapper _mapper = new(Mock.Of<IJiraClient>(),Mock.Of<IOptions<MappingConfig>>());

    [Fact]
    public void GetStatusById_returns_unknown_status_if_id_is_not_known()
    {
        var status = _mapper.GetStatusById("abc");
        status.Category.ShouldBe("Unknown");
        status.Name.ShouldBe("Unknown (abc)");
        status.Id.ShouldBe("abc");
    }
}