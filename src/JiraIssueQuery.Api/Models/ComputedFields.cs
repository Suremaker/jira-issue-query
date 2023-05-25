namespace JiraIssueQuery.Api.Models;

public class ComputedFields
{
    public static readonly ComputedField StatusCategory = new(nameof(StatusCategory), "status");
    public static readonly ComputedField Age = new(nameof(Age), "created");
    public static readonly ComputedField TimeInStatus = new(nameof(TimeInStatus), "[CHART] Time in Status");
    public static readonly ComputedField TimeInCategory = new(nameof(TimeInCategory), "[CHART] Time in Status");
    public static readonly ComputedField LeadTime = new(nameof(LeadTime), "resolutiondate", "created");
    public static readonly ComputedField CycleTime = new(nameof(CycleTime), "[CHART] Time in Status", "resolutiondate");
    public static readonly ComputedField CarriedOverSprint = new(nameof(CarriedOverSprint), "sprint", "resolutiondate");
    public static readonly ComputedField CompletedSprint = new(nameof(CompletedSprint), "sprint", "resolutiondate");
    public static readonly ComputedField CompletedSprintStartDate = new(nameof(CompletedSprintStartDate), "sprint", "resolutiondate");
    public static readonly ComputedField CompletedSprintEndDate = new(nameof(CompletedSprintEndDate), "sprint", "resolutiondate");
    public static readonly ComputedField TimeSinceStatusCategoryChange = new(nameof(TimeSinceStatusCategoryChange), "statuscategorychangedate");
    public static readonly ComputedField Url = new(nameof(Url));

    public static readonly IReadOnlyList<ComputedField> Fields = typeof(ComputedFields).GetFields()
        .Where(f => f.FieldType == typeof(ComputedField))
        .Select(f => (ComputedField)f.GetValue(null)!)
        .ToArray();
}