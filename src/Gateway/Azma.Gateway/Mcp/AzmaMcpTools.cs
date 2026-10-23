using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace Azma.Gateway.Mcp;

[McpServerToolType]
[Description("Tools for operating the Azma test management platform through the local Gateway API.")]
public sealed class AzmaMcpTools
{
    private readonly AzmaApiClient _api;

    public AzmaMcpTools(AzmaApiClient api) => _api = api;

    [McpServerTool]
    [Description("Use to inspect the authenticated MCP user's current Azma workspace. No side effects. Returns the current user/workspace context.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_whoami(CancellationToken cancellationToken = default) =>
        _api.GetAsync("/auth/me", "Loaded current Azma MCP identity.", cancellationToken);

    [McpServerTool]
    [Description("Use to list Azma projects in the current workspace. No side effects. Supports search, archived inclusion, and pagination.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_list_projects(
        [Description("Optional project name/code search text.")] string? search = null,
        [Description("Include archived projects when true.")] bool includeArchived = false,
        [Description("1-based page number.")] int page = 1,
        [Description("Page size. Default 25, maximum 100.")] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync(
            "/projects",
            "Listed Azma projects.",
            cancellationToken,
            Q("search", search),
            Q("includeArchived", includeArchived),
            Q("page", Page(page)),
            Q("pageSize", PageSize(pageSize)));

    [McpServerTool]
    [Description("Use to fetch one Azma project by ID. No side effects. Returns project details.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_project(
        [Description("Project ID.")] Guid projectId,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync($"/projects/{projectId}", "Loaded Azma project.", cancellationToken);

    [McpServerTool]
    [Description("Use to create a project in the current workspace. Side effect: creates a project. Code must be unique in the workspace.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_create_project(
        [Description("Project display name.")] string name,
        [Description("Short unique project code.")] string code,
        [Description("Optional project description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync(
            "/projects",
            new { name, code, description },
            "Created Azma project.",
            cancellationToken);

    [McpServerTool]
    [Description("Use to update a project. Side effect: updates project name/description. Requires projectId.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_update_project(
        [Description("Project ID.")] Guid projectId,
        [Description("New project display name.")] string name,
        [Description("Optional project description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _api.PutAsync(
            $"/projects/{projectId}",
            new { name, description },
            "Updated Azma project.",
            cancellationToken);

    [McpServerTool]
    [Description("Use to archive a project. Side effect: hides/disables a project. Requires confirm=true.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_archive_project(
        [Description("Project ID.")] Guid projectId,
        [Description("Must be true to confirm archiving this project.")] bool confirm = false,
        CancellationToken cancellationToken = default) =>
        confirm
            ? _api.PostAsync($"/projects/{projectId}/archive", null, "Archived Azma project.", cancellationToken)
            : Task.FromResult(ConfirmRequired("archive_project", "Call azma_archive_project again with confirm=true."));

    [McpServerTool]
    [Description("Use to restore an archived project. Side effect: makes a project active again. Requires projectId.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_restore_project(
        [Description("Project ID.")] Guid projectId,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/projects/{projectId}/restore", null, "Restored Azma project.", cancellationToken);

    [McpServerTool]
    [Description("Use to list suites for a project. No side effects. Returns suite summaries.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_list_suites(
        [Description("Project ID.")] Guid projectId,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync($"/projects/{projectId}/suites", "Listed project suites.", cancellationToken);

    [McpServerTool]
    [Description("Use to fetch a suite tree, including nested sections. No side effects. Requires suiteId.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_suite_tree(
        [Description("Suite ID.")] Guid suiteId,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync($"/suites/{suiteId}/tree", "Loaded suite tree.", cancellationToken);

    [McpServerTool]
    [Description("Use to create a suite under a project. Side effect: creates a suite. Requires projectId.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_create_suite(
        [Description("Project ID.")] Guid projectId,
        [Description("Suite name.")] string name,
        [Description("Optional suite description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/projects/{projectId}/suites", new { name, description }, "Created suite.", cancellationToken);

    [McpServerTool]
    [Description("Use to update a suite. Side effect: updates suite name/description. Requires suiteId.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_update_suite(
        [Description("Suite ID.")] Guid suiteId,
        [Description("Suite name.")] string name,
        [Description("Optional suite description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _api.PutAsync($"/suites/{suiteId}", new { name, description }, "Updated suite.", cancellationToken);

    [McpServerTool]
    [Description("Use to create a section inside a suite. Side effect: creates a section. parentId may be null for root.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_create_section(
        [Description("Suite ID.")] Guid suiteId,
        [Description("Section name.")] string name,
        [Description("Optional parent section ID. Null creates a root section.")] Guid? parentId = null,
        [Description("Optional section description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/suites/{suiteId}/sections", new { parentId, name, description }, "Created section.", cancellationToken);

    [McpServerTool]
    [Description("Use to update a section. Side effect: updates section name/description. Requires sectionId.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_update_section(
        [Description("Section ID.")] Guid sectionId,
        [Description("Section name.")] string name,
        [Description("Optional section description.")] string? description = null,
        CancellationToken cancellationToken = default) =>
        _api.PutAsync($"/sections/{sectionId}", new { name, description }, "Updated section.", cancellationToken);

    [McpServerTool]
    [Description("Use to move/reorder a section. Side effect: changes section parent/order. newParentId may be null for root.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_move_section(
        [Description("Section ID.")] Guid sectionId,
        [Description("New 0-based position under the target parent.")] int newPosition,
        [Description("Optional new parent section ID. Null moves to suite root.")] Guid? newParentId = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/sections/{sectionId}/move", new { newParentId, newPosition }, "Moved section.", cancellationToken);

    [McpServerTool]
    [Description("Use to list test cases. No side effects. Supports project/suite/section filters, search, tag, status, priority, executionState, staleDays, deleted inclusion, and pagination.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_list_test_cases(
        [Description("Optional project ID filter.")] Guid? projectId = null,
        [Description("Optional suite ID filter.")] Guid? suiteId = null,
        [Description("Optional section ID filter.")] Guid? sectionId = null,
        [Description("Optional text search.")] string? search = null,
        [Description("Optional tag filter.")] string? tag = null,
        [Description("Optional status: Draft, Ready, Deprecated.")] string? status = null,
        [Description("Optional priority: Low, Medium, High, Critical.")] string? priority = null,
        [Description("Optional execution state filter.")] string? executionState = null,
        [Description("Days used by stale execution filter.")] int staleDays = 14,
        [Description("Include soft-deleted test cases when true.")] bool includeDeleted = false,
        [Description("1-based page number.")] int page = 1,
        [Description("Page size. Default 25, maximum 100.")] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync(
            "/testcases",
            "Listed test cases.",
            cancellationToken,
            Q("projectId", projectId),
            Q("suiteId", suiteId),
            Q("sectionId", sectionId),
            Q("search", search),
            Q("tag", tag),
            Q("status", status),
            Q("priority", priority),
            Q("executionState", executionState),
            Q("staleDays", staleDays),
            Q("includeDeleted", includeDeleted),
            Q("page", Page(page)),
            Q("pageSize", PageSize(pageSize)));

    [McpServerTool]
    [Description("Use to fetch one test case by ID. No side effects. Returns full test case details including steps and custom fields.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_test_case(
        [Description("Test case ID.")] Guid testCaseId,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync($"/testcases/{testCaseId}", "Loaded test case.", cancellationToken);

    [McpServerTool]
    [Description("Use to create a test case. Side effect: creates a test case. type: Functional/Regression/Performance/Security/Usability; priority: Low/Medium/High/Critical; status: Draft/Ready/Deprecated.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_create_test_case(
        [Description("Project ID.")] Guid projectId,
        [Description("Test case title.")] string title,
        [Description("Optional suite ID.")] Guid? suiteId = null,
        [Description("Optional section ID.")] Guid? sectionId = null,
        [Description("Optional preconditions text.")] string? preconditions = null,
        [Description("Type. Default Functional.")] string? type = "Functional",
        [Description("Priority. Default Medium.")] string? priority = "Medium",
        [Description("Status. Default handled by API when null.")] string? status = null,
        [Description("Optional tags.")] string[]? tags = null,
        [Description("Optional ordered test steps.")] AzmaStepInput[]? steps = null,
        [Description("Optional external automation scenario path.")] string? automationScenarioPath = null,
        [Description("Optional custom field values keyed by field key.")] Dictionary<string, JsonElement>? customFields = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync(
            "/testcases",
            new
            {
                projectId,
                suiteId,
                sectionId,
                title,
                preconditions,
                type,
                priority,
                status,
                tags,
                steps,
                automationScenarioPath,
                customFields
            },
            "Created test case.",
            cancellationToken);

    [McpServerTool]
    [Description("Use to update a test case. Side effect: updates the test case and may create a new version. Requires all main enum fields.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_update_test_case(
        [Description("Test case ID.")] Guid testCaseId,
        [Description("Test case title.")] string title,
        [Description("Type: Functional, Regression, Performance, Security, or Usability.")] string type,
        [Description("Priority: Low, Medium, High, or Critical.")] string priority,
        [Description("Status: Draft, Ready, or Deprecated.")] string status,
        [Description("Optional suite ID.")] Guid? suiteId = null,
        [Description("Optional section ID.")] Guid? sectionId = null,
        [Description("Optional preconditions text.")] string? preconditions = null,
        [Description("Optional change reason.")] string? changeReason = null,
        [Description("Optional external automation scenario path.")] string? automationScenarioPath = null,
        [Description("Optional custom field values keyed by field key.")] Dictionary<string, JsonElement>? customFields = null,
        CancellationToken cancellationToken = default) =>
        _api.PutAsync(
            $"/testcases/{testCaseId}",
            new
            {
                suiteId,
                sectionId,
                title,
                preconditions,
                type,
                priority,
                status,
                changeReason,
                automationScenarioPath,
                customFields
            },
            "Updated test case.",
            cancellationToken);

    [McpServerTool]
    [Description("Use to delete/soft-delete a test case. Side effect: removes the test case from normal lists. Requires confirm=true.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_delete_test_case(
        [Description("Test case ID.")] Guid testCaseId,
        [Description("Must be true to confirm deleting this test case.")] bool confirm = false,
        CancellationToken cancellationToken = default) =>
        confirm
            ? _api.DeleteAsync($"/testcases/{testCaseId}", "Deleted test case.", cancellationToken)
            : Task.FromResult(ConfirmRequired("delete_test_case", "Call azma_delete_test_case again with confirm=true."));

    [McpServerTool]
    [Description("Use to replace all steps for a test case. Side effect: overwrites the full ordered step list.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_set_test_steps(
        [Description("Test case ID.")] Guid testCaseId,
        [Description("Full ordered list of replacement steps.")] AzmaStepInput[] steps,
        CancellationToken cancellationToken = default) =>
        _api.PutAsync($"/testcases/{testCaseId}/steps", steps, "Replaced test case steps.", cancellationToken);

    [McpServerTool]
    [Description("Use to replace all tags for a test case. Side effect: overwrites the full tag list.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_set_test_tags(
        [Description("Test case ID.")] Guid testCaseId,
        [Description("Full replacement tag list.")] string[] tags,
        CancellationToken cancellationToken = default) =>
        _api.PutAsync($"/testcases/{testCaseId}/tags", tags, "Replaced test case tags.", cancellationToken);

    [McpServerTool]
    [Description("Use to list versions for a test case. No side effects. Requires testCaseId.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_list_test_case_versions(
        [Description("Test case ID.")] Guid testCaseId,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync($"/testcases/{testCaseId}/versions", "Listed test case versions.", cancellationToken);

    [McpServerTool]
    [Description("Use to list test runs. No side effects. Supports project, milestone, status Open/Closed, search, deleted inclusion, and pagination.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_list_runs(
        [Description("Optional project ID filter.")] Guid? projectId = null,
        [Description("Optional milestone ID filter.")] Guid? milestoneId = null,
        [Description("Optional status: Open or Closed.")] string? status = null,
        [Description("Optional run name search text.")] string? search = null,
        [Description("Include deleted runs when true.")] bool includeDeleted = false,
        [Description("1-based page number.")] int page = 1,
        [Description("Page size. Default 25, maximum 100.")] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync(
            "/runs",
            "Listed runs.",
            cancellationToken,
            Q("projectId", projectId),
            Q("milestoneId", milestoneId),
            Q("status", status),
            Q("search", search),
            Q("includeDeleted", includeDeleted),
            Q("page", Page(page)),
            Q("pageSize", PageSize(pageSize)));

    [McpServerTool]
    [Description("Use to fetch one run by ID. No side effects. Returns run details.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_run(
        [Description("Run ID.")] Guid runId,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync($"/runs/{runId}", "Loaded run.", cancellationToken);

    [McpServerTool]
    [Description("Use to create a run. Side effect: creates a test run and snapshots selected test cases.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_create_run(
        [Description("Project ID.")] Guid projectId,
        [Description("Run name.")] string name,
        [Description("Test case IDs to include. Empty creates a run without cases if API permits.")] Guid[]? testCaseIds = null,
        [Description("Optional run description.")] string? description = null,
        [Description("Optional milestone ID.")] Guid? milestoneId = null,
        [Description("Optional environment label.")] string? environment = null,
        [Description("Optional due date as ISO 8601 UTC string.")] string? dueDateUtc = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync(
            "/runs",
            new { projectId, milestoneId, name, description, testCaseIds, environment, dueDateUtc },
            "Created run.",
            cancellationToken);

    [McpServerTool]
    [Description("Use to update run metadata. Side effect: updates name, description, environment, or due date.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_update_run(
        [Description("Run ID.")] Guid runId,
        [Description("Optional new run name.")] string? name = null,
        [Description("Optional new description.")] string? description = null,
        [Description("Optional new environment label.")] string? environment = null,
        [Description("Optional due date as ISO 8601 UTC string.")] string? dueDateUtc = null,
        CancellationToken cancellationToken = default) =>
        _api.PatchAsync($"/runs/{runId}", new { name, description, environment, dueDateUtc }, "Updated run.", cancellationToken);

    [McpServerTool]
    [Description("Use to duplicate a run. Side effect: creates a new run copied from the source run.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_duplicate_run(
        [Description("Source run ID.")] Guid runId,
        [Description("Optional name for duplicated run.")] string? name = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/runs/{runId}/duplicate", new { name }, "Duplicated run.", cancellationToken);

    [McpServerTool]
    [Description("Use to add test cases to an existing run. Side effect: appends run cases.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_add_run_cases(
        [Description("Run ID.")] Guid runId,
        [Description("Test case IDs to add.")] Guid[] testCaseIds,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/runs/{runId}/cases", new { testCaseIds }, "Added cases to run.", cancellationToken);

    [McpServerTool]
    [Description("Use to remove a case from a run. Side effect: removes a run case. Requires confirm=true.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_remove_run_case(
        [Description("Run ID.")] Guid runId,
        [Description("Run case ID to remove.")] Guid caseId,
        [Description("Must be true to confirm removing this case from the run.")] bool confirm = false,
        CancellationToken cancellationToken = default) =>
        confirm
            ? _api.DeleteAsync($"/runs/{runId}/cases/{caseId}", "Removed case from run.", cancellationToken)
            : Task.FromResult(ConfirmRequired("remove_run_case", "Call azma_remove_run_case again with confirm=true."));

    [McpServerTool]
    [Description("Use to close a run. Side effect: marks the run closed.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_close_run(
        [Description("Run ID.")] Guid runId,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/runs/{runId}/close", null, "Closed run.", cancellationToken);

    [McpServerTool]
    [Description("Use to reopen a closed run. Side effect: marks the run open.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_reopen_run(
        [Description("Run ID.")] Guid runId,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/runs/{runId}/reopen", null, "Reopened run.", cancellationToken);

    [McpServerTool]
    [Description("Use to list cases in a run. No side effects. Supports result status, assignee, search, and pagination. pageSize maximum 500.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_list_run_cases(
        [Description("Run ID.")] Guid runId,
        [Description("Optional result status: Untested, Passed, Failed, Blocked, Retest, or Skipped.")] string? status = null,
        [Description("Optional assigned user ID filter.")] Guid? assignedTo = null,
        [Description("Optional case title/code search text.")] string? search = null,
        [Description("1-based page number.")] int page = 1,
        [Description("Page size. Default 100, maximum 500.")] int pageSize = 100,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync(
            $"/runs/{runId}/cases",
            "Listed run cases.",
            cancellationToken,
            Q("status", status),
            Q("assignedTo", assignedTo),
            Q("search", search),
            Q("page", Page(page)),
            Q("pageSize", PageSize(pageSize, defaultSize: 100, max: 500)));

    [McpServerTool]
    [Description("Use to record a result for one run case. Side effect: creates a result and updates current case status. status: Untested/Passed/Failed/Blocked/Retest/Skipped.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_record_result(
        [Description("Run case ID.")] Guid runCaseId,
        [Description("Result status: Untested, Passed, Failed, Blocked, Retest, or Skipped.")] string status,
        [Description("Optional result comment.")] string? comment = null,
        [Description("Optional elapsed seconds.")] int? elapsedSeconds = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync("/results", new { runCaseId, status, comment, elapsedSeconds }, "Recorded run result.", cancellationToken);

    [McpServerTool]
    [Description("Use to record the same result for multiple run cases. Side effect: creates results and updates current statuses.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_bulk_record_results(
        [Description("Run case IDs.")] Guid[] runCaseIds,
        [Description("Result status: Untested, Passed, Failed, Blocked, Retest, or Skipped.")] string status,
        [Description("Optional result comment.")] string? comment = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync("/results/bulk", new { runCaseIds, status, comment }, "Bulk-recorded run results.", cancellationToken);

    [McpServerTool]
    [Description("Use to assign or unassign a run case. Side effect: updates the current assignee. Pass null assignedToUserId to unassign.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_assign_run_case(
        [Description("Run case ID.")] Guid runCaseId,
        [Description("User ID to assign, or null to unassign.")] Guid? assignedToUserId = null,
        CancellationToken cancellationToken = default) =>
        _api.PutAsync($"/results/{runCaseId}/assignee", new { assignedToUserId }, "Updated run case assignee.", cancellationToken);

    [McpServerTool]
    [Description("Use to get result history for one run case. No side effects. Requires runCaseId.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_result_history(
        [Description("Run case ID.")] Guid runCaseId,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync($"/results/{runCaseId}", "Loaded result history.", cancellationToken);

    [McpServerTool]
    [Description("Use to execute one published recorded scenario. Side effect: triggers automation and records the result. Requires confirm=true.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_run_automatic_case(
        [Description("Run case ID.")] Guid runCaseId,
        [Description("Must be true to confirm executing automation.")] bool confirm = false,
        [Description("Optional target gateway URL.")] string? gateway = null,
        [Description("Optional target frontend URL.")] string? frontend = null,
        CancellationToken cancellationToken = default) =>
        confirm
            ? _api.PostAsync(
                $"/results/{runCaseId}/automatic",
                new { gateway, frontend },
                "Executed automated run case.",
                cancellationToken)
            : Task.FromResult(ConfirmRequired("run_automatic_case", "Call azma_run_automatic_case again with confirm=true."));

    [McpServerTool]
    [Description("Use to list run schedules. No side effects. Supports project filter, inactive inclusion, and pagination.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_list_run_schedules(
        [Description("Optional project ID filter.")] Guid? projectId = null,
        [Description("Include inactive/paused schedules when true.")] bool includeInactive = true,
        [Description("1-based page number.")] int page = 1,
        [Description("Page size. Default 25, maximum 100.")] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync(
            "/run-schedules",
            "Listed run schedules.",
            cancellationToken,
            Q("projectId", projectId),
            Q("includeInactive", includeInactive),
            Q("page", Page(page)),
            Q("pageSize", PageSize(pageSize)));

    [McpServerTool]
    [Description("Use to fetch a run schedule by ID. No side effects. Returns schedule details and cases.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_run_schedule(
        [Description("Run schedule ID.")] Guid scheduleId,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync($"/run-schedules/{scheduleId}", "Loaded run schedule.", cancellationToken);

    [McpServerTool]
    [Description("Use to create a run schedule. Side effect: creates an automated schedule. recurrenceType examples: Daily, Weekly.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_create_run_schedule(
        [Description("Project ID.")] Guid projectId,
        [Description("Schedule name.")] string name,
        [Description("Recurrence type, for example Daily or Weekly.")] string recurrenceType,
        [Description("Time of day in minutes after local midnight.")] int timeOfDayMinutes,
        [Description("Weekday bit mask used by the Runs API.")] int weekDaysMask,
        [Description("Test case IDs to include.")] Guid[]? testCaseIds = null,
        [Description("Optional description.")] string? description = null,
        [Description("Optional environment label.")] string? environment = null,
        [Description("Optional time zone ID.")] string? timeZoneId = null,
        [Description("Optional start time as ISO 8601 UTC string.")] string? startAtUtc = null,
        [Description("Optional end time as ISO 8601 UTC string.")] string? endAtUtc = null,
        [Description("Optional execution loop count.")] int? loops = null,
        [Description("Optional parallel worker count.")] int? threads = null,
        [Description("Optional ramp-up value.")] int? rampup = null,
        [Description("Optional skippable flag.")] bool? skippable = null,
        [Description("Optional target gateway URL.")] string? gateway = null,
        [Description("Optional target frontend URL.")] string? frontend = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync(
            "/run-schedules",
            new
            {
                projectId,
                name,
                description,
                environment,
                recurrenceType,
                timeOfDayMinutes,
                weekDaysMask,
                timeZoneId,
                startAtUtc,
                endAtUtc,
                testCaseIds,
                loops,
                threads,
                rampup,
                skippable,
                gateway,
                frontend
            },
            "Created run schedule.",
            cancellationToken);

    [McpServerTool]
    [Description("Use to update a run schedule. Side effect: updates schedule timing, cases, or execution settings. Omitted fields stay unchanged.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_update_run_schedule(
        [Description("Run schedule ID.")] Guid scheduleId,
        [Description("Optional schedule name.")] string? name = null,
        [Description("Optional description.")] string? description = null,
        [Description("Optional environment label.")] string? environment = null,
        [Description("Optional recurrence type, for example Daily or Weekly.")] string? recurrenceType = null,
        [Description("Optional time of day in minutes after local midnight.")] int? timeOfDayMinutes = null,
        [Description("Optional weekday bit mask used by the Runs API.")] int? weekDaysMask = null,
        [Description("Optional time zone ID.")] string? timeZoneId = null,
        [Description("Optional start time as ISO 8601 UTC string.")] string? startAtUtc = null,
        [Description("Optional end time as ISO 8601 UTC string.")] string? endAtUtc = null,
        [Description("Optional replacement test case IDs.")] Guid[]? testCaseIds = null,
        [Description("Optional execution loop count.")] int? loops = null,
        [Description("Optional parallel worker count.")] int? threads = null,
        [Description("Optional ramp-up value.")] int? rampup = null,
        [Description("Optional skippable flag.")] bool? skippable = null,
        [Description("Optional target gateway URL.")] string? gateway = null,
        [Description("Optional target frontend URL.")] string? frontend = null,
        CancellationToken cancellationToken = default) =>
        _api.PatchAsync(
            $"/run-schedules/{scheduleId}",
            new
            {
                name,
                description,
                environment,
                recurrenceType,
                timeOfDayMinutes,
                weekDaysMask,
                timeZoneId,
                startAtUtc,
                endAtUtc,
                testCaseIds,
                loops,
                threads,
                rampup,
                skippable,
                gateway,
                frontend
            },
            "Updated run schedule.",
            cancellationToken);

    [McpServerTool]
    [Description("Use to pause a run schedule. Side effect: marks the schedule inactive.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_pause_run_schedule(
        [Description("Run schedule ID.")] Guid scheduleId,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/run-schedules/{scheduleId}/pause", null, "Paused run schedule.", cancellationToken);

    [McpServerTool]
    [Description("Use to resume a paused run schedule. Side effect: marks the schedule active.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_resume_run_schedule(
        [Description("Run schedule ID.")] Guid scheduleId,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/run-schedules/{scheduleId}/resume", null, "Resumed run schedule.", cancellationToken);

    [McpServerTool]
    [Description("Use to run a schedule immediately. Side effect: triggers schedule execution. Requires confirm=true.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_run_schedule_now(
        [Description("Run schedule ID.")] Guid scheduleId,
        [Description("Must be true to confirm immediate schedule execution.")] bool confirm = false,
        CancellationToken cancellationToken = default) =>
        confirm
            ? _api.PostAsync($"/run-schedules/{scheduleId}/run-now", null, "Triggered run schedule now.", cancellationToken)
            : Task.FromResult(ConfirmRequired("run_schedule_now", "Call azma_run_schedule_now again with confirm=true."));

    [McpServerTool]
    [Description("Use to delete a run schedule. Side effect: removes the schedule. Requires confirm=true.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_delete_run_schedule(
        [Description("Run schedule ID.")] Guid scheduleId,
        [Description("Must be true to confirm deleting this schedule.")] bool confirm = false,
        CancellationToken cancellationToken = default) =>
        confirm
            ? _api.DeleteAsync($"/run-schedules/{scheduleId}", "Deleted run schedule.", cancellationToken)
            : Task.FromResult(ConfirmRequired("delete_run_schedule", "Call azma_delete_run_schedule again with confirm=true."));

    [McpServerTool]
    [Description("Use to list executions for a run schedule. No side effects. Supports pagination.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_list_schedule_executions(
        [Description("Run schedule ID.")] Guid scheduleId,
        [Description("1-based page number.")] int page = 1,
        [Description("Page size. Default 20, maximum 100.")] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync(
            $"/run-schedules/{scheduleId}/executions",
            "Listed run schedule executions.",
            cancellationToken,
            Q("page", Page(page)),
            Q("pageSize", PageSize(pageSize, defaultSize: 20)));

    [McpServerTool]
    [Description("Use to fetch the current workspace dashboard. No side effects. Returns counts and recent activity.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_workspace_dashboard(CancellationToken cancellationToken = default) =>
        _api.GetAsync("/dashboards/workspace", "Loaded workspace dashboard.", cancellationToken);

    [McpServerTool]
    [Description("Use to fetch a project dashboard. No side effects. Requires projectId.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_project_dashboard(
        [Description("Project ID.")] Guid projectId,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync($"/dashboards/project/{projectId}", "Loaded project dashboard.", cancellationToken);

    [McpServerTool]
    [Description("Use to fetch reporting trend buckets. No side effects. fromUtc/toUtc are ISO 8601 UTC strings; granularity is day/week/month.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_trends(
        [Description("Optional project ID filter.")] Guid? projectId = null,
        [Description("Optional start timestamp as ISO 8601 UTC string. Defaults to 30 days before toUtc.")] string? fromUtc = null,
        [Description("Optional end timestamp as ISO 8601 UTC string. Defaults to now.")] string? toUtc = null,
        [Description("Granularity: day, week, or month.")] string granularity = "day",
        CancellationToken cancellationToken = default) =>
        _api.GetAsync(
            "/reporting/trends",
            "Loaded reporting trends.",
            cancellationToken,
            Q("projectId", projectId),
            Q("from", fromUtc),
            Q("to", toUtc),
            Q("granularity", granularity));

    [McpServerTool]
    [Description("Use to list top failing tests. No side effects. Supports project filter, lookback days, and result limit.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_top_failures(
        [Description("Optional project ID filter.")] Guid? projectId = null,
        [Description("Lookback window in days.")] int days = 30,
        [Description("Maximum rows to return. Default 10, maximum 100.")] int limit = 10,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync(
            "/reporting/top-failures",
            "Loaded top failing tests.",
            cancellationToken,
            Q("projectId", projectId),
            Q("days", Math.Max(1, days)),
            Q("limit", PageSize(limit, defaultSize: 10)));

    [McpServerTool]
    [Description("Use to list user testing activity. No side effects. Supports project filter, lookback days, and result limit.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_get_user_activity(
        [Description("Optional project ID filter.")] Guid? projectId = null,
        [Description("Lookback window in days.")] int days = 30,
        [Description("Maximum rows to return. Default 25, maximum 100.")] int limit = 25,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync(
            "/reporting/users",
            "Loaded user activity.",
            cancellationToken,
            Q("projectId", projectId),
            Q("days", Math.Max(1, days)),
            Q("limit", PageSize(limit)));

    [McpServerTool]
    [Description("Use to list members in the current workspace. No side effects. Returns users, roles, and access scopes.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_list_members(CancellationToken cancellationToken = default) =>
        _api.GetAsync("/members", "Listed workspace members.", cancellationToken);

    [McpServerTool]
    [Description("Use to invite a user to the current workspace. Side effect: creates a workspace invitation. role examples: Admin, Tester, Viewer.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_invite_member(
        [Description("Invitee email address.")] string email,
        [Description("Workspace role, for example Admin, Tester, or Viewer.")] string role,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync("/members/invitations", new { email, role }, "Invited workspace member.", cancellationToken);

    [McpServerTool]
    [Description("Use to change a workspace member role. Side effect: updates role. role examples: Admin, Tester, Viewer.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_change_member_role(
        [Description("User ID.")] Guid userId,
        [Description("New workspace role, for example Admin, Tester, or Viewer.")] string role,
        CancellationToken cancellationToken = default) =>
        _api.PatchAsync($"/members/{userId}", new { role }, "Changed workspace member role.", cancellationToken);

    [McpServerTool]
    [Description("Use to remove a member from the current workspace. Side effect: removes workspace access. Requires confirm=true.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_remove_member(
        [Description("User ID.")] Guid userId,
        [Description("Must be true to confirm removing this workspace member.")] bool confirm = false,
        CancellationToken cancellationToken = default) =>
        confirm
            ? _api.DeleteAsync($"/members/{userId}", "Removed workspace member.", cancellationToken)
            : Task.FromResult(ConfirmRequired("remove_member", "Call azma_remove_member again with confirm=true."));

    [McpServerTool]
    [Description("Use to list project members. No side effects. Requires projectId.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_list_project_members(
        [Description("Project ID.")] Guid projectId,
        CancellationToken cancellationToken = default) =>
        _api.GetAsync($"/projects/{projectId}/members", "Listed project members.", cancellationToken);

    [McpServerTool]
    [Description("Use to add a user as a project member. Side effect: grants project access. role examples: Admin, Tester, Viewer, Excluded.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_add_project_member(
        [Description("Project ID.")] Guid projectId,
        [Description("User ID.")] Guid userId,
        [Description("Project role, for example Admin, Tester, Viewer, or Excluded.")] string? role = null,
        CancellationToken cancellationToken = default) =>
        _api.PostAsync($"/projects/{projectId}/members", new { userId, role }, "Added project member.", cancellationToken);

    [McpServerTool]
    [Description("Use to set a project member role. Side effect: changes project role. role examples: Admin, Tester, Viewer, Excluded.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_set_project_member_role(
        [Description("Project ID.")] Guid projectId,
        [Description("User ID.")] Guid userId,
        [Description("Project role, for example Admin, Tester, Viewer, or Excluded.")] string role,
        CancellationToken cancellationToken = default) =>
        _api.PutAsync($"/projects/{projectId}/members/{userId}", new { role }, "Changed project member role.", cancellationToken);

    [McpServerTool]
    [Description("Use to remove a project-specific membership override. Side effect: removes project role/access. Requires confirm=true.")]
    public Task<AzmaMcpResult<JsonElement?>> azma_remove_project_member(
        [Description("Project ID.")] Guid projectId,
        [Description("User ID.")] Guid userId,
        [Description("Must be true to confirm removing this project member.")] bool confirm = false,
        CancellationToken cancellationToken = default) =>
        confirm
            ? _api.DeleteAsync($"/projects/{projectId}/members/{userId}", "Removed project member.", cancellationToken)
            : Task.FromResult(ConfirmRequired("remove_project_member", "Call azma_remove_project_member again with confirm=true."));

    private static QueryParam Q(string name, object? value) => new(name, value);

    private static int Page(int page) => Math.Max(1, page);

    private static int PageSize(int pageSize, int defaultSize = 25, int max = 100)
    {
        var value = pageSize <= 0 ? defaultSize : pageSize;
        return Math.Clamp(value, 1, max);
    }

    private static AzmaMcpResult<JsonElement?> ConfirmRequired(string operation, string nextAction) =>
        AzmaMcpResult<JsonElement?>.Failure(
            $"Confirmation required before Azma can {operation}.",
            warnings: new[] { "This MCP tool changes or deletes Azma data." },
            nextActions: new[] { nextAction });
}

public sealed record AzmaStepInput(
    [property: Description("Action to perform in this test step.")] string Action,
    [property: Description("Expected result for this test step.")] string? ExpectedResult);
