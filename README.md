# jira-issue-query
Web Api allowing to query Jira issues for further processing.

While Jira exposes Rest Api and makes it easy to query for the ticket details, working with the ticket models or aggregating data is difficult.

The Jira Issue Query API offers following capabilities:
* simplified, flattened response models, allowing easy graph creation
* response caching
* computation of additional ticket stats like:
  * `X-StatusCategory` - status category of the status field,
  * `X-Age` - ticket age in hours since it's creation,
  * `X-TimeInStatus` - array of Status-Time pairs where Time represent the total time in milliseconds that ticket spent in that status,
  * `X-TimeInCategory` - array of Status-Time pairs where Time represent the total time in milliseconds that ticket spent in that status,
  * `X-LeadTime` - total hours between created and resolved dates (present if ticket is resolved),
  * `X-CycleTime` - total hours that ticket spent in statuses configured in `Mappings:CycleTimeStatuses` setting (present if ticket is resolved),
  * `X-CarriedOverSprint` - array of sprint names that ticket was present in but not resolved,
  * `X-CompletedSprint` - sprint name of the sprint in which the ticket was resolved,
  * `X-CompletedSprintStartDate` - sprint start date in which the ticket was resolved,
  * `X-CompletedSprintEndDate` - sprint end date in which the ticket was resolved,
  * `X-TimeSinceStatusCategoryChange` - time in hours since last status category change on the ticket
* grouping and aggregation of the retrieved tickets against selected fields to produce results like average `X-CycleTime` of tickets for each `IssueType`, grouped by `Resolved` on the month basis.

_Note: Aggregate Api endpoints allows providing the Jira field names without spaces, i.e. `IssueType`_

# Before run

Update `Config:JiraUri` value in `appsettings.json` to point to your Jira instance.

# To run

Execute `\src\JiraIssueQuery.Api> dontent run`
Open `https://localhost:5001/swagger/index.html`
