using System.Linq;
using JiraIssueQuery.Api.Aggregators;
using JiraIssueQuery.Api.Mappers;
using JiraIssueQuery.Api.Models;
using Moq;
using Shouldly;
using Xunit;

namespace JiraIssueQuery.Api.UnitTests
{
    public class IssueAggregatorTests
    {
        private readonly IssueAggregator _aggregator;

        public IssueAggregatorTests()
        {
            var refDataMapper = new Mock<IReferenceDataMapper>();
            refDataMapper.Setup(x => x.TryResolveFieldName(It.IsAny<string>())).Returns((string arg) => arg);
            _aggregator = new IssueAggregator(refDataMapper.Object);
        }

        [Fact]
        public void Aggregate_should_group_issues_by_field()
        {
            var issues = new[]
            {
                new JiraIssue { { "grp", "a" },{ "sg", "Done" } },
                new JiraIssue { { "grp", "a" },{ "sg", "Done" } },
                new JiraIssue { { "grp", "b" },{ "sg", "Done" } },
                new JiraIssue { { "grp", "b" },{ "sg", "In Progress" } },
                new JiraIssue { { "grp", "c" },{ "sg", "To Do" } },
            };
            var results = _aggregator.Aggregate(issues,
                    new FieldGrouping("grp"),
                    new FieldGrouping("sg"),
                    new FieldAggregation("sg"))
                .ToArray();

            AssertResults(
                results,
                "grp",
                ToResult(("grp", "a"), ("Done", 2), ("In Progress", 0), ("To Do", 0), ("all", 2)),
                ToResult(("grp", "b"), ("Done", 1), ("In Progress", 1), ("To Do", 0), ("all", 2)),
                ToResult(("grp", "c"), ("Done", 0), ("In Progress", 0), ("To Do", 1), ("all", 1)));
        }

        [Fact]
        public void Aggregate_should_group_issues_by_date()
        {
            var issues = new[]
            {
                new JiraIssue { { "grp", "2020-01-02 15:27:00" },{ "sg", "Done" } },
                new JiraIssue { { "grp", "2020-01-02 15:17:00" },{ "sg", "Done" } },
                new JiraIssue { { "grp", "2020-01-03 15:27:00" },{ "sg", "Done" } },
                new JiraIssue { { "grp", "2020-01-03 21:27:00" },{ "sg", "In Progress" } },
                new JiraIssue { { "grp", "2020-02-04 15:27:00" },{ "sg", "In Progress" } },
            };

            AssertResults(
                _aggregator.Aggregate(issues,
                    new FieldGrouping("grp", AggregateFieldType.Date, "yyyy-MM-dd"),
                    new FieldGrouping("sg"),
                    new FieldAggregation("sg", AggregateOperation.Count)).ToArray(),
                "grp",
                ToResult(("grp", "2020-01-02"), ("Done", 2), ("In Progress", 0), ("all", 2)),
                ToResult(("grp", "2020-01-03"), ("Done", 1), ("In Progress", 1), ("all", 2)),
                ToResult(("grp", "2020-02-04"), ("Done", 0), ("In Progress", 1), ("all", 1)));

            AssertResults(
                _aggregator.Aggregate(issues,
                    new FieldGrouping("grp", AggregateFieldType.Date, "yyyy-MM"),
                    new FieldGrouping("sg"),
                    new FieldAggregation("sg", AggregateOperation.Count)).ToArray(),
                "grp",
                ToResult(("grp", "2020-01"), ("Done", 3), ("In Progress", 1), ("all", 4)),
                ToResult(("grp", "2020-02"), ("Done", 0), ("In Progress", 1), ("all", 1)));
        }

        [Theory]
        [InlineData(AggregateOperation.Count, 3, 2, 5)]
        [InlineData(AggregateOperation.CountRatio, 3 / 5.0, 2 / 5.0, 1)]
        [InlineData(AggregateOperation.Avg, 6 / 3.0, 9 / 2.0, 15 / 5.0)]
        [InlineData(AggregateOperation.Sum, 6, 9, 15)]
        [InlineData(AggregateOperation.SumRatio, 6 / 15.0, 9 / 15.0, 1)]
        [InlineData(AggregateOperation.Min, 1, 4, 1)]
        [InlineData(AggregateOperation.Max, 3, 5, 5)]
        public void AggregateOperation_should_properly_calculate_values(AggregateOperation operation,
            double done, double todo, double all)
        {
            var issues = new[]
            {
                new JiraIssue { { "grp", "a" },{ "sg", "Done" },{"v",1.0} },
                new JiraIssue { { "grp", "a" },{ "sg", "Done" },{"v",2.0} },
                new JiraIssue { { "grp", "a" },{ "sg", "Done" },{"v",3.0} },
                new JiraIssue { { "grp", "a" },{ "sg", "Todo" },{"v",4.0} },
                new JiraIssue { { "grp", "a" },{ "sg", "Todo" },{"v",5.0} }
            };
            var results = _aggregator.Aggregate(issues,
                new FieldGrouping("grp"),
                new FieldGrouping("sg"),
                new FieldAggregation("v", operation)).ToArray();

            AssertResults(results, "grp",
                ToResult(("grp", "a"), ("Done", done), ("Todo", todo), ("all", all)));
        }

        [Theory]
        [InlineData(AggregateOperation.Count, 1, 1)]
        [InlineData(AggregateOperation.CountRatio, 1, 1)]
        [InlineData(AggregateOperation.Avg, 0, 0)]
        [InlineData(AggregateOperation.Sum, 0, 0)]
        [InlineData(AggregateOperation.SumRatio, 0, 0)]
        [InlineData(AggregateOperation.Min, 0, 0)]
        [InlineData(AggregateOperation.Max, 0, 0)]
        public void AggregateOperation_should_properly_calculate_values_for_empty_collections(AggregateOperation operation,
            double unset, double all)
        {
            var issues = new[]
            {
                new JiraIssue { { "grp", "a" }}
            };
            var results = _aggregator.Aggregate(issues,
                new FieldGrouping("grp"),
                new FieldGrouping("sg"),
                new FieldAggregation("v", operation)).ToArray();

            AssertResults(results, "grp", ToResult(("grp", "a"), ("all", all), ("unset", unset)));
        }

        [Fact]
        public void Compare_should_compare_values_from_two_aggregates()
        {
            var baseline = new[]
            {
                ToResult(("key", "A"), ("V1", 5.0), ("V2", 10.0)),
                ToResult(("key", "B"), ("V1", 50.0), ("V2", 100.0)),
                ToResult(("key", "C"), ("V1", 500.0), ("V2", 1000.0))
            };
            var compare = new[]
            {
                ToResult(("key2", "A"), ("V1", 5.0), ("V2", 1.0), ("Ignored",2.0)),
                ToResult(("key2", "B"), ("V1", 0.0), ("V2", 110.0), ("Ignored",2.0)),
                ToResult(("key2", "ignored"), ("V1", 500.0), ("V2", 10.0)),
            };

            var result = _aggregator.Compare(baseline, compare, "key", "key2").ToArray();
            AssertResults(result, "key",
                ToResult(("key", "A"), ("V1", 1.0), ("V2", 0.1)),
                ToResult(("key", "B"), ("V1", 0.0), ("V2", 1.1)),
                ToResult(("key", "C"), ("V1", 0.0), ("V2", 0.0))
                );
        }

        private void AssertResults(JiraIssueAggregate[] actual, string groupField, params JiraIssueAggregate[] expected)
        {
            var a = actual.OrderBy(x => x[groupField]).ToArray();
            var e = expected.OrderBy(y => y[groupField]).ToArray();
            a.Length.ShouldBe(e.Length);
            for (int i = 0; i < e.Length; ++i)
                a[i].OrderBy(x => x.Key).ShouldBe(e[i].OrderBy(y => y.Key));
        }

        private JiraIssueAggregate ToResult((string key, object value) group, params (string key, double value)[] aggregates)
        {
            var r = new JiraIssueAggregate
            {
                [group.key] = group.value
            };
            foreach (var (key, value) in aggregates)
                r[key] = value;
            return r;
        }
    }
}