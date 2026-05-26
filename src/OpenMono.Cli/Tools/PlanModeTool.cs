using System.Text.Json;
using OpenMono.Session;

namespace OpenMono.Tools;

public sealed class EnterPlanModeTool : ToolBase
{
    public override string Name => "EnterPlanMode";
    public override string Description =>
        """
        Use this tool proactively before starting any non-trivial implementation task.
        Getting user sign-off on your approach before writing code prevents wasted effort.

        ## When to use EnterPlanMode

        Use it when ANY of these apply:

        - New feature that involves architectural decisions (where does it go? what pattern?)
        - Multiple valid approaches exist and the choice meaningfully affects the codebase
        - Changes that touch more than 2-3 files
        - Unclear requirements — you need to explore before you can understand the scope
        - High-impact restructuring where the wrong approach causes significant rework
        - You would normally ask a clarifying question about the approach — plan instead

        ## When NOT to use EnterPlanMode

        Skip it for simple tasks:
        - Single-line or few-line fixes, typos, obvious bugs
        - The user gave specific, detailed instructions and the path is clear
        - Pure research/exploration (use the Agent tool with Explore type instead)
        - The user said "just do it" or "go ahead" — start working

        ## Examples

        GOOD — use EnterPlanMode:
          "Add user authentication" — session vs JWT, middleware structure, many files
          "Improve performance" — need to profile first, multiple strategies possible
          "Refactor the data layer" — architectural decisions, high impact

        BAD — do not use EnterPlanMode:
          "Fix the typo in the README"
          "Add a console.log to debug this"
          "What files handle routing?" — this is research, not implementation
        """;

    public override PermissionLevel DefaultPermission => PermissionLevel.AutoAllow;

    public override bool IsReadOnly => true;

    protected override SchemaBuilder DefineSchema() => new SchemaBuilder()
        .AddString("reason", "Why you are entering plan mode — what task are you planning?")
        .Require("reason");

    protected override Task<ToolResult> ExecuteCoreAsync(JsonElement input, ToolContext context, CancellationToken ct)
    {
        var reason = input.GetProperty("reason").GetString()!;

        if (context.Session.Meta.PlanMode)
            return Task.FromResult(ToolResult.Error("Already in plan mode. Use ExitPlanMode to leave."));

        context.Session.Meta.PlanMode = true;

        return Task.FromResult(ToolResult.Success(PlanModeInstructions.Activation(reason)));
    }
}

public sealed class ExitPlanModeTool : ToolBase
{
    public override string Name => "ExitPlanMode";
    public override string Description =>
        """
        Exit plan mode and present the implementation plan to the user for approval.
        Call this when your plan is complete and ready for the user to review.

        The `plan` argument must be a structured numbered plan — not vague prose.
        It should list: the approach, every file that changes, risks, and complexity.
        """;

    public override PermissionLevel DefaultPermission => PermissionLevel.AutoAllow;


    public override bool IsReadOnly => true;

    protected override SchemaBuilder DefineSchema() => new SchemaBuilder()
        .AddString("plan", "The full numbered implementation plan to present to the user")
        .Require("plan");

    protected override Task<ToolResult> ExecuteCoreAsync(JsonElement input, ToolContext context, CancellationToken ct)
    {
        var plan = input.GetProperty("plan").GetString()!;

        if (!context.Session.Meta.PlanMode)
            return Task.FromResult(ToolResult.Error(
                "Plan mode is not currently active — the previous plan was already presented to the user. " +
                "If you need to plan again, call EnterPlanMode first, then call ExitPlanMode with the new plan."));

        context.Session.Meta.PlanMode = false;
        context.Session.Meta.LastPlan = plan;

        context.WriteOutput($"\n## Plan\n\n{plan}\n");

        return Task.FromResult(ToolResult.Success(
            $"Exited plan mode. Present the plan below to the user, then stop — " +
            $"write tools will be available on the next turn.\n\n{plan}")
            .WithBreakTurn());
    }
}
