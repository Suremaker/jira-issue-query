using System;
using System.Linq;
using System.Text.Json;
using JiraIssueQuery.Api.Mappers;
using JiraIssueQuery.Api.Models;
using Moq;
using Shouldly;
using Xunit;

namespace JiraIssueQuery.Api.UnitTests
{
    public class JiraIssuesMapperTests
    {
        private readonly Mock<IReferenceDataMapper> _refDataMapper = new();
        private readonly JiraIssueMapper _mapper;
        private const string JiraServerUri = "https://some-jira/";

        public JiraIssuesMapperTests()
        {
            _refDataMapper.Setup(x => x.TryResolveFieldName(It.IsAny<string>())).Returns((string arg) => arg);
            _refDataMapper.Setup(x => x.GetCycleTimeStatuses())
                .Returns(Array.Empty<string>());

            _mapper = new JiraIssueMapper(_refDataMapper.Object);
        }

        [Fact]
        public void Map_calculates_Age_in_hours()
        {
            var createdDate = "2022-02-14T17:19:37.302+0000";
            var src = ToJsonElement("T-1", ("created", createdDate));
            var issue = _mapper.Map(src);
            var expected = (long)(DateTimeOffset.UtcNow - DateTimeOffset.Parse(createdDate)).TotalHours;

            ((long)(double)issue["X-Age"]).ShouldBe(expected);
        }

        [Fact]
        public void Map_calculates_TimeSinceStatusCategoryChange_in_hours()
        {
            var changedDate = "2022-02-14T17:19:37.302+0000";
            var src = ToJsonElement("T-1", ("statuscategorychangedate", changedDate));
            var issue = _mapper.Map(src);
            var expected = (long)(DateTimeOffset.UtcNow - DateTimeOffset.Parse(changedDate)).TotalHours;

            ((long)(double)issue["X-TimeSinceStatusCategoryChange"]).ShouldBe(expected);
        }

        [Fact]
        public void Map_calculates_LeadTime_in_hours()
        {
            var createdDate = "2022-02-14T17:19:37.302+0000";
            var resolvedDate = "2022-02-15T15:23:58.037+0000";
            var src = ToJsonElement("T-1", ("created", createdDate), ("resolutiondate", resolvedDate));
            var issue = _mapper.Map(src);
            var expected = (DateTimeOffset.Parse(resolvedDate) - DateTimeOffset.Parse(createdDate)).TotalHours;

            issue["X-LeadTime"].ShouldBe(expected);
        }

        [Fact]
        public void Map_calculates_CycleTime_in_hours()
        {
            _refDataMapper.Setup(x => x.GetCycleTimeStatuses())
                .Returns(new[] { "In Progress", "In Review" });

            _refDataMapper.Setup(x => x.TimeInStatusFieldKey)
                .Returns("time-in-status");
            _refDataMapper.Setup(x => x.GetStatusById("10008"))
                .Returns(new Status { Name = "In Progress" });
            _refDataMapper.Setup(x => x.GetStatusById("10009"))
                .Returns(new Status { Name = "In Review" });
            _refDataMapper.Setup(x => x.GetStatusById("10010"))
                .Returns(new Status { Name = "To Do" });
            _refDataMapper.Setup(x => x.GetStatusById("10014"))
                .Returns(new Status { Name = "Done" });

            var inProgressMs = 2607650;
            var inReviewMs = 4400000;

            var src = ToJsonElement("T-1", ("resolutiondate", "2022-02-15T15:23:58.037+0000"),
                ("time-in-status", $"10008_*:*_1_*:*_{inProgressMs}_*|10009_*:*_1_*:*_{inReviewMs}_*|10010_*:*_1_*:*_2600000_*|*_10014_*:*_1_*:*_0"));

            var issue = _mapper.Map(src);
            var expected = TimeSpan.FromMilliseconds(inProgressMs + inReviewMs).TotalHours;
            issue["X-CycleTime"].ShouldBe(expected);
        }

        [Fact]
        public void Map_calculates_StatusCategory()
        {
            var src = ToJsonElement("T-1", ("status", new { statusCategory = new { name = "To Do" } }));
            var issue = _mapper.Map(src);

            issue["X-StatusCategory"].ShouldBe("To Do");
        }

        [Fact]
        public void Map_handles_CompletedSprint_and_CarriedOverSprint_for_null_Sprints()
        {
            _refDataMapper.Setup(x => x.TryResolveFieldKey("sprint"))
                .Returns("customfield_1234");

            var src = ToJsonElement("T-1", ("resolutiondate", "2021-09-27T11:54:36.347+0100"),
                ("customfield_1234", null!));

            var issue = _mapper.Map(src);
            issue.Keys.ShouldNotContain("X-CompletedSprint");
            issue["X-CarriedOverSprint"].ShouldBe(Array.Empty<string>());
        }

        [Fact]
        public void Map_calculates_CompletedSprint_and_CarriedOverSprint()
        {
            _refDataMapper.Setup(x => x.TryResolveFieldKey("sprint"))
                .Returns("customfield_1234");

            var src = ToJsonElement("T-1", ("resolutiondate", "2021-09-27T11:54:36.347+0100"),
                ("customfield_1234", new[]
                {
                    new {name="RT14", startDate="2021-07-05T08:00:00.000Z", endDate="2021-08-13T16:00:00.000Z", completeDate="2021-08-13T13:18:47.553Z"},
                    new {name="RT15", startDate="2021-08-16T10:57:50.620Z", endDate="2021-09-24T17:00:00.000Z", completeDate="2021-09-29T14:15:25.786Z"}
                }));
            var issue = _mapper.Map(src);

            issue["X-CompletedSprint"].ShouldBe("RT15");
            issue["X-CompletedSprintStartDate"].ShouldBe(DateTimeOffset.Parse("2021-08-16T10:57:50.620Z"));
            issue["X-CompletedSprintEndDate"].ShouldBe(DateTimeOffset.Parse("2021-09-29T14:15:25.786Z"));
            issue["X-CarriedOverSprint"].ShouldBe(new[] { "RT14" });
        }

        [Fact]
        public void Map_calculates_CompletedSprint_and_CarriedOverSprint_for_unresolved_tickets()
        {
            _refDataMapper.Setup(x => x.TryResolveFieldKey("sprint"))
                .Returns("customfield_1234");

            var src = ToJsonElement("T-1",
                ("customfield_1234", new[]
                {
                    new {name="RT14", startDate="2021-07-05T08:00:00.000Z", endDate="2021-08-13T16:00:00.000Z", completeDate=(string?)"2021-08-13T13:18:47.553Z"},
                    new {name="RT15", startDate="2021-08-16T10:57:50.620Z", endDate="2021-09-24T17:00:00.000Z", completeDate=(string?)"2021-09-29T14:15:25.786Z"},
                    new {name="RTCurrent", startDate="2021-08-16T10:57:50.620Z", endDate=DateTimeOffset.UtcNow.AddDays(1).ToString("O"), completeDate=(string?)null},
                }));
            var issue = _mapper.Map(src);

            issue.ContainsKey("X-CompletedSprint").ShouldBe(false);
            issue.ContainsKey("X-CompletedSprintStartDate").ShouldBe(false);
            issue.ContainsKey("X-CompletedSprintEndDate").ShouldBe(false);
            issue["X-CarriedOverSprint"].ShouldBe(new[] { "RT14", "RT15" });
        }

        [Fact]
        public void Map_calculates_CompletedSprint_dates_for_unfinished_sprints()
        {
            _refDataMapper.Setup(x => x.TryResolveFieldKey("sprint"))
                .Returns("customfield_1234");

            var src = ToJsonElement("T-1", ("resolutiondate", "2021-09-21T11:54:36.347+0100"),
                ("customfield_1234", new[]
                {
                    new {name="RT14", startDate="2021-07-05T08:00:00.000Z", endDate="2021-08-13T16:00:00.000Z", completeDate=(string?)"2021-08-13T13:18:47.553Z"},
                    new {name="RT15", startDate="2021-08-16T10:57:50.620Z", endDate="2021-09-24T17:00:00.000Z", completeDate=(string?)null},
                }));
            var issue = _mapper.Map(src);

            issue["X-CompletedSprint"].ShouldBe("RT15");
            issue["X-CompletedSprintStartDate"].ShouldBe(DateTimeOffset.Parse("2021-08-16T10:57:50.620Z"));
            issue["X-CompletedSprintEndDate"].ShouldBe(DateTimeOffset.Parse("2021-09-24T17:00:00.000Z"));
            issue["X-CarriedOverSprint"].ShouldBe(new[] { "RT14" });
        }

        [Fact]
        public void Map_calculates_CompletedSprint_and_CarriedOverSprint_for_unresolved_tickets_and_unspecified_sprints()
        {
            _refDataMapper.Setup(x => x.TryResolveFieldKey("sprint"))
                .Returns("customfield_1234");

            var src = ToJsonElement("T-1",
                ("customfield_1234", new[]
                {
                    new {name="RT14", startDate="2021-07-05T08:00:00.000Z", endDate="2021-08-13T16:00:00.000Z", completeDate=(string?)"2021-08-13T13:18:47.553Z"},
                    new {name="RT15", startDate="2021-08-16T10:57:50.620Z", endDate="2021-09-24T17:00:00.000Z", completeDate=(string?)"2021-09-29T14:15:25.786Z"},
                    new {name="RTCurrent", startDate=(string?)null, endDate=(string?)null, completeDate=(string?)null}
                }));
            var issue = _mapper.Map(src);

            issue.ContainsKey("X-CompletedSprint").ShouldBe(false);
            issue.ContainsKey("X-CompletedSprintStartDate").ShouldBe(false);
            issue.ContainsKey("X-CompletedSprintEndDate").ShouldBe(false);
            issue["X-CarriedOverSprint"].ShouldBe(new[] { "RT14", "RT15" });
        }

        [Fact]
        public void Map_extracts_key_and_url()
        {
            _refDataMapper.Setup(x => x.TryResolveFieldKey("sprint"))
                .Returns("customfield_1234");

            var src = ToJsonElement("T-1");
            var issue = _mapper.Map(src);

            issue["Key"].ShouldBe("T-1");
            issue["X-Url"].ShouldBe($"{JiraServerUri}browse/T-1");
        }

        private JsonElement ToJsonElement(string ticketKey, params (string key, object value)[] fields)
        {
            var obj = new
            {
                key = ticketKey,
                self = $"{JiraServerUri}rest/api/3/issue/{Random.Shared.Next(0, 100000)}",
                fields = fields.ToDictionary(f => f.key, f => f.value)
            };
            var json = JsonSerializer.Serialize(obj);
            return JsonDocument.Parse(json).RootElement;
        }
    }
}
