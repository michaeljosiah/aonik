using Aonik.Agents.Entities.Workflows;

namespace Aonik.Agents.Services.Seeding;

/// <summary>
/// Catalogue of demo workflows. Mirrors the WORKFLOWS array in
/// templates/aonik-admin-starterkit/screens/workflows.jsx 1:1, plus the
/// expanded graph + comments + recent-runs that the editor renders for
/// match_and_apply (workflow-editor-screen.jsx DEFAULT_WORKFLOW etc.).
///
/// Guids are deterministic so re-running the seed is idempotent and lets
/// the SPA's saved-link bookmarks resolve consistently across re-installs.
/// </summary>
internal static class WorkflowSeedCatalog
{
    public static IReadOnlyList<WorkflowSeed> Build(
        IReadOnlyDictionary<string, Guid> agentIdsByName,
        DateTime now)
    {
        Guid Agent(string name) => agentIdsByName.TryGetValue(name, out var id) ? id : Guid.Empty;

        return new List<WorkflowSeed>
        {
            BuildMatchAndApply(Agent, now),
            BuildSweepUnmatched(Agent, now),
            BuildDunningCadence(Agent, now),
            BuildForwardQuote(Agent, now),
            BuildKycRecheck(Agent, now),
            BuildMonthlyClose(Agent, now),
            BuildSpendAnomaly(Agent, now),
        };
    }

    // ── match_and_apply ────────────────────────────────────────────────
    // The flagship workflow. Full graph, version history, recent-runs
    // (some held over the ceiling), and Maria's pinned comment.

    private static WorkflowSeed BuildMatchAndApply(Func<string, Guid> agent, DateTime now)
    {
        var n1 = Guid.Parse("11111111-aaaa-0001-0000-000000000001");
        var n2 = Guid.Parse("11111111-aaaa-0001-0000-000000000002");
        var n3 = Guid.Parse("11111111-aaaa-0001-0000-000000000003");
        var n4 = Guid.Parse("11111111-aaaa-0001-0000-000000000004");
        var n5 = Guid.Parse("11111111-aaaa-0001-0000-000000000005");
        var n6 = Guid.Parse("11111111-aaaa-0001-0000-000000000006");
        var n7 = Guid.Parse("11111111-aaaa-0001-0000-000000000007");
        var n8 = Guid.Parse("11111111-aaaa-0001-0000-000000000008");

        return new WorkflowSeed(
            WorkflowId: Guid.Parse("11111111-aaaa-0001-1111-111111111111"),
            Slug: "match_and_apply",
            Name: "Match & apply",
            Description: "Reconcile invoice → bank txn, draft an entry, surface it for review when over policy ceiling.",
            OwnerAgentId: agent("Billing"),
            OwnerColor: "#eb5c37",
            ContributorAgentIds: new[] { agent("Ledger"), agent("Compliance") },
            State: WorkflowStates.Active,
            Version: "v1.4",
            AutoRetry: true,
            TriggerCount: 4,
            Nodes: new[]
            {
                new WorkflowNodeSeed(n1, WorkflowNodeKinds.Trigger, "On bank txn", "banking.transaction.received", 64, 240,
                    """{"source":"banking.transaction.received","filter":"amount > 0"}"""),
                new WorkflowNodeSeed(n2, WorkflowNodeKinds.Tool, "Find candidate invoices", "search_invoices", 320, 240,
                    """{"tool":"search_invoices","params":"{ \"amount_eps\": 0.01 }"}"""),
                new WorkflowNodeSeed(n3, WorkflowNodeKinds.Agent, "Score match", "Billing · confidence ≥ 0.85", 576, 240,
                    """{"agent":"Billing","task":"Score candidate invoices and pick best match. Cite reasoning."}"""),
                new WorkflowNodeSeed(n4, WorkflowNodeKinds.Decision, "Above ceiling?", "amount > 50000", 832, 240,
                    """{"expr":"amount > 50000","yesLabel":"Yes","noLabel":"No"}"""),
                new WorkflowNodeSeed(n5, WorkflowNodeKinds.Human, "Treasury approval", "group: Treasury · 4h SLA", 1088, 144,
                    """{"group":"Treasury","sla":"4h"}"""),
                new WorkflowNodeSeed(n6, WorkflowNodeKinds.Tool, "Draft journal entry", "AR · 1200", 1088, 336,
                    """{"tool":"draft_journal_entry","params":"{ \"account\": \"1200\" }"}"""),
                new WorkflowNodeSeed(n7, WorkflowNodeKinds.Notify, "Send receipt", "email · receipt_v2", 1344, 240,
                    """{"channel":"email","template":"receipt_v2"}"""),
                new WorkflowNodeSeed(n8, WorkflowNodeKinds.End, "Match applied", null, 1600, 240, "{}"),
            },
            Edges: new[]
            {
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0001-eeee-000000000001"), n1, n2, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0001-eeee-000000000002"), n2, n3, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0001-eeee-000000000003"), n3, n4, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0001-eeee-000000000004"), n4, n5, 0, "yes"),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0001-eeee-000000000005"), n4, n6, 1, "no"),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0001-eeee-000000000006"), n5, n6, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0001-eeee-000000000007"), n6, n7, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0001-eeee-000000000008"), n7, n8, 0, null),
            },
            Comments: new[]
            {
                new WorkflowCommentSeed(
                    Guid.Parse("11111111-aaaa-0001-cccc-000000000001"),
                    1024, 60,
                    "Maria · Treasury",
                    "Approval ceiling raised from £25K → £50K on 12 Apr per CFO memo."),
            },
            Versions: new[]
            {
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0001-vvvv-000000000004"), "v1.4", "Raised approval ceiling £25K → £50K. Added Treasury approval branch.", "Maria",  "#eb5c37", now.AddHours(-3)),
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0001-vvvv-000000000003"), "v1.3", "Auto-link to receipt template after journal entry posted.",                  "Aonik",  "#055a60", now.AddDays(-8)),
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0001-vvvv-000000000002"), "v1.2", "Switch matcher from regex to fuzzy + score.",                                "Rafa",   "#7b76b6", now.AddDays(-21)),
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0001-vvvv-000000000001"), "v1.1", "Initial draft auto-generated from playbook.",                                 "Aonik",  "#055a60", now.AddDays(-60)),
            },
            Runs: BuildMatchAndApplyRuns(now, n1, n2, n3, n4, n5, n6, n7, n8));
    }

    private static IReadOnlyList<WorkflowRunSeed> BuildMatchAndApplyRuns(DateTime now, params Guid[] n)
    {
        var fast = new[] { n[0], n[1], n[2], n[3], n[5], n[6], n[7] };
        var held = new[] { n[0], n[1], n[2], n[3], n[4] };
        return new[]
        {
            new WorkflowRunSeed(Guid.Parse("11111111-aaaa-0001-rrrr-000000000001"), now.AddMinutes(-2),  now.AddMinutes(-2).AddSeconds(2),  WorkflowRunStatuses.Success, 2200,   "auto · banking.transaction.received", fast),
            new WorkflowRunSeed(Guid.Parse("11111111-aaaa-0001-rrrr-000000000002"), now.AddMinutes(-14), now.AddMinutes(-14).AddSeconds(2), WorkflowRunStatuses.Success, 2480,   "auto · banking.transaction.received", fast),
            new WorkflowRunSeed(Guid.Parse("11111111-aaaa-0001-rrrr-000000000003"), now.AddMinutes(-38), null,                                WorkflowRunStatuses.Held,    434000, "held · over ceiling",                  held),
            new WorkflowRunSeed(Guid.Parse("11111111-aaaa-0001-rrrr-000000000004"), now.AddHours(-1),    now.AddHours(-1).AddSeconds(2),    WorkflowRunStatuses.Success, 2350,   "auto",                                 fast),
            new WorkflowRunSeed(Guid.Parse("11111111-aaaa-0001-rrrr-000000000005"), now.AddHours(-2),    now.AddHours(-2).AddMilliseconds(960), WorkflowRunStatuses.Failed, 960,   "tool: read_timeout",                   new[] { n[0], n[1] }),
            new WorkflowRunSeed(Guid.Parse("11111111-aaaa-0001-rrrr-000000000006"), now.AddHours(-3),    now.AddHours(-3).AddSeconds(2),    WorkflowRunStatuses.Success, 2430,   "auto",                                 fast),
        };
    }

    // ── sweep_unmatched ────────────────────────────────────────────────

    private static WorkflowSeed BuildSweepUnmatched(Func<string, Guid> agent, DateTime now)
    {
        var n1 = Guid.Parse("11111111-aaaa-0002-0000-000000000001");
        var n2 = Guid.Parse("11111111-aaaa-0002-0000-000000000002");
        var n3 = Guid.Parse("11111111-aaaa-0002-0000-000000000003");
        var n4 = Guid.Parse("11111111-aaaa-0002-0000-000000000004");
        var n5 = Guid.Parse("11111111-aaaa-0002-0000-000000000005");
        var n6 = Guid.Parse("11111111-aaaa-0002-0000-000000000006");

        return new WorkflowSeed(
            WorkflowId: Guid.Parse("11111111-aaaa-0002-2222-222222222222"),
            Slug: "sweep_unmatched",
            Name: "Sweep unmatched",
            Description: "Hourly retry pass for invoices that fell through earlier. Loosens fuzzy matching as time passes.",
            OwnerAgentId: agent("Billing"),
            OwnerColor: "#eb5c37",
            ContributorAgentIds: Array.Empty<Guid>(),
            State: WorkflowStates.Active,
            Version: "v0.9",
            AutoRetry: false,
            TriggerCount: 1,
            Nodes: new[]
            {
                new WorkflowNodeSeed(n1, WorkflowNodeKinds.Trigger, "Hourly tick", "schedule.cron", 64, 240, """{"source":"schedule.hourly","filter":""}"""),
                new WorkflowNodeSeed(n2, WorkflowNodeKinds.Tool,    "list_open_invoices", "aged > 24h", 320, 240, """{"tool":"list_open_invoices","params":"{ \"aged_h\": 24 }"}"""),
                new WorkflowNodeSeed(n3, WorkflowNodeKinds.Agent,   "Billing · fuzzy match", "tier escalates", 576, 240, """{"agent":"Billing","task":"Fuzzy-match candidates with escalating tier"}"""),
                new WorkflowNodeSeed(n4, WorkflowNodeKinds.Decision,"Match found?", null, 832, 240, """{"expr":"match.confidence >= 0.6","yesLabel":"Match","noLabel":"Skip"}"""),
                new WorkflowNodeSeed(n5, WorkflowNodeKinds.Tool,    "apply_match", "auto-apply", 1088, 144, """{"tool":"apply_match","params":"{}"}"""),
                new WorkflowNodeSeed(n6, WorkflowNodeKinds.End,     "Sweep complete", null, 1344, 240, "{}"),
            },
            Edges: new[]
            {
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0002-eeee-000000000001"), n1, n2, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0002-eeee-000000000002"), n2, n3, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0002-eeee-000000000003"), n3, n4, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0002-eeee-000000000004"), n4, n5, 0, "yes"),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0002-eeee-000000000005"), n4, n6, 1, "no"),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0002-eeee-000000000006"), n5, n6, 0, null),
            },
            Comments: Array.Empty<WorkflowCommentSeed>(),
            Versions: new[]
            {
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0002-vvvv-000000000001"), "v0.9", "Loosen tier-3 threshold to 0.6.",  "Rafa", "#7b76b6", now.AddDays(-7)),
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0002-vvvv-000000000000"), "v0.1", "Initial draft.",                    "Aonik", "#055a60", now.AddDays(-30)),
            },
            Runs: BuildScheduledRuns(now, "sweep", 24, 18000, 0.71, n1, n2, n3, n4, n5, n6));
    }

    // ── dunning_cadence ────────────────────────────────────────────────

    private static WorkflowSeed BuildDunningCadence(Func<string, Guid> agent, DateTime now)
    {
        var n1 = Guid.Parse("11111111-aaaa-0003-0000-000000000001");
        var n2 = Guid.Parse("11111111-aaaa-0003-0000-000000000002");
        var n3 = Guid.Parse("11111111-aaaa-0003-0000-000000000003");
        var n4 = Guid.Parse("11111111-aaaa-0003-0000-000000000004");
        var n5 = Guid.Parse("11111111-aaaa-0003-0000-000000000005");
        var n6 = Guid.Parse("11111111-aaaa-0003-0000-000000000006");
        var n7 = Guid.Parse("11111111-aaaa-0003-0000-000000000007");
        var n8 = Guid.Parse("11111111-aaaa-0003-0000-000000000008");

        return new WorkflowSeed(
            WorkflowId: Guid.Parse("11111111-aaaa-0003-3333-333333333333"),
            Slug: "dunning_cadence",
            Name: "Dunning cadence",
            Description: "Send overdue reminders on a per-customer rhythm. Escalates tone every 7 days, hands to phone after day 21.",
            OwnerAgentId: agent("Dunning"),
            OwnerColor: "#5facbd",
            ContributorAgentIds: new[] { agent("Compliance") },
            State: WorkflowStates.Paused,
            Version: "v2.0",
            AutoRetry: true,
            TriggerCount: 2,
            Nodes: new[]
            {
                new WorkflowNodeSeed(n1, WorkflowNodeKinds.Trigger,  "Invoice overdue", "billing.invoice.overdue", 64, 240, """{"source":"billing.invoice.overdue"}"""),
                new WorkflowNodeSeed(n2, WorkflowNodeKinds.Tool,     "lookup_customer", "segment + tier",          320, 240, """{"tool":"lookup_customer","params":"{}"}"""),
                new WorkflowNodeSeed(n3, WorkflowNodeKinds.Decision, "Days overdue",    "7 / 14 / 21",             576, 240, """{"expr":"daysOverdue","yesLabel":"≤14","noLabel":">14"}"""),
                new WorkflowNodeSeed(n4, WorkflowNodeKinds.Agent,    "Dunning · compose", "tone keyed to days",    832, 240, """{"agent":"Dunning","task":"Compose reminder; tone scales with daysOverdue."}"""),
                new WorkflowNodeSeed(n5, WorkflowNodeKinds.Human,    "Approve outbound", "tier-1 only",            1088, 144, """{"group":"Compliance","sla":"24h"}"""),
                new WorkflowNodeSeed(n6, WorkflowNodeKinds.Notify,   "Send email",       "or SMS",                 1344, 240, """{"channel":"email","template":"dunning_v3"}"""),
                new WorkflowNodeSeed(n7, WorkflowNodeKinds.Wait,     "Wait 7 days",      "or until paid",          1600, 240, """{"duration":"7d"}"""),
                new WorkflowNodeSeed(n8, WorkflowNodeKinds.End,      "Cadence step done", null,                    1856, 240, "{}"),
            },
            Edges: new[]
            {
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0003-eeee-000000000001"), n1, n2, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0003-eeee-000000000002"), n2, n3, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0003-eeee-000000000003"), n3, n4, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0003-eeee-000000000004"), n4, n5, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0003-eeee-000000000005"), n5, n6, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0003-eeee-000000000006"), n6, n7, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0003-eeee-000000000007"), n7, n8, 0, null),
            },
            Comments: Array.Empty<WorkflowCommentSeed>(),
            Versions: new[]
            {
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0003-vvvv-000000000002"), "v2.0", "Pause + add Compliance approval gate while we re-run KYB.", "Maria", "#eb5c37", now.AddDays(-11)),
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0003-vvvv-000000000001"), "v1.4", "Tier-1 customers always require human approval.",            "Aonik", "#055a60", now.AddDays(-65)),
            },
            Runs: BuildScheduledRuns(now, "dunning", 14, 4100, 0.88, n1, n2, n3, n4, n5, n6, n7, n8));
    }

    // ── forward_quote ──────────────────────────────────────────────────

    private static WorkflowSeed BuildForwardQuote(Func<string, Guid> agent, DateTime now)
    {
        var n1 = Guid.Parse("11111111-aaaa-0004-0000-000000000001");
        var n2 = Guid.Parse("11111111-aaaa-0004-0000-000000000002");
        var n3 = Guid.Parse("11111111-aaaa-0004-0000-000000000003");
        var n4 = Guid.Parse("11111111-aaaa-0004-0000-000000000004");
        var n5 = Guid.Parse("11111111-aaaa-0004-0000-000000000005");
        var n6 = Guid.Parse("11111111-aaaa-0004-0000-000000000006");

        return new WorkflowSeed(
            WorkflowId: Guid.Parse("11111111-aaaa-0004-4444-444444444444"),
            Slug: "forward_quote",
            Name: "Forward quote",
            Description: "Quote a forward FX contract for cross-border invoices. Fetches rate fixings, calculates markup, drafts the contract.",
            OwnerAgentId: agent("FX"),
            OwnerColor: "#3ab795",
            ContributorAgentIds: new[] { agent("Compliance") },
            State: WorkflowStates.Active,
            Version: "v1.1",
            AutoRetry: false,
            TriggerCount: 2,
            Nodes: new[]
            {
                new WorkflowNodeSeed(n1, WorkflowNodeKinds.Trigger, "Cross-border invoice", "billing.invoice.cross_border", 64, 240, """{"source":"billing.invoice.cross_border"}"""),
                new WorkflowNodeSeed(n2, WorkflowNodeKinds.Tool,    "fetch_fx_fix",         "CME · WMR",                    320, 240, """{"tool":"fetch_fx_fix","params":"{}"}"""),
                new WorkflowNodeSeed(n3, WorkflowNodeKinds.Agent,   "FX · price quote",     "+spread",                      576, 240, """{"agent":"FX","task":"Price the forward quote with our spread."}"""),
                new WorkflowNodeSeed(n4, WorkflowNodeKinds.Tool,    "draft_forward_contract", null,                          832, 240, """{"tool":"draft_forward_contract","params":"{}"}"""),
                new WorkflowNodeSeed(n5, WorkflowNodeKinds.Human,   "Counterparty signs",    null,                          1088, 240, """{"group":"Treasury","sla":"24h"}"""),
                new WorkflowNodeSeed(n6, WorkflowNodeKinds.End,     "Quote delivered",       null,                          1344, 240, "{}"),
            },
            Edges: new[]
            {
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0004-eeee-000000000001"), n1, n2, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0004-eeee-000000000002"), n2, n3, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0004-eeee-000000000003"), n3, n4, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0004-eeee-000000000004"), n4, n5, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0004-eeee-000000000005"), n5, n6, 0, null),
            },
            Comments: Array.Empty<WorkflowCommentSeed>(),
            Versions: new[]
            {
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0004-vvvv-000000000001"), "v1.1", "Switched to WMR fixings during London close window.", "Rafa", "#7b76b6", now.AddDays(-6)),
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0004-vvvv-000000000000"), "v1.0", "Initial cut.",                                         "Aonik", "#055a60", now.AddDays(-45)),
            },
            Runs: BuildScheduledRuns(now, "fx", 8, 1800, 0.99, n1, n2, n3, n4, n5, n6));
    }

    // ── kyc_recheck ────────────────────────────────────────────────────

    private static WorkflowSeed BuildKycRecheck(Func<string, Guid> agent, DateTime now)
    {
        var n1 = Guid.Parse("11111111-aaaa-0005-0000-000000000001");
        var n2 = Guid.Parse("11111111-aaaa-0005-0000-000000000002");
        var n3 = Guid.Parse("11111111-aaaa-0005-0000-000000000003");
        var n4 = Guid.Parse("11111111-aaaa-0005-0000-000000000004");
        var n5 = Guid.Parse("11111111-aaaa-0005-0000-000000000005");
        var n6 = Guid.Parse("11111111-aaaa-0005-0000-000000000006");
        var n7 = Guid.Parse("11111111-aaaa-0005-0000-000000000007");

        return new WorkflowSeed(
            WorkflowId: Guid.Parse("11111111-aaaa-0005-5555-555555555555"),
            Slug: "kyc_recheck",
            Name: "KYC re-check",
            Description: "Re-screen counterparty against sanctions and PEP lists. Triggered on a 90-day rotation or risk-flag changes.",
            OwnerAgentId: agent("Compliance"),
            OwnerColor: "#7b76b6",
            ContributorAgentIds: Array.Empty<Guid>(),
            State: WorkflowStates.Active,
            Version: "v3.2",
            AutoRetry: true,
            TriggerCount: 3,
            Nodes: new[]
            {
                new WorkflowNodeSeed(n1, WorkflowNodeKinds.Trigger,  "On schedule · or flag", "compliance.recheck", 64, 240, """{"source":"compliance.recheck"}"""),
                new WorkflowNodeSeed(n2, WorkflowNodeKinds.Tool,     "fetch_sanctions_lists", null,                  320, 240, """{"tool":"fetch_sanctions_lists","params":"{}"}"""),
                new WorkflowNodeSeed(n3, WorkflowNodeKinds.Tool,     "screen_counterparty",   "OFAC · UN · EU · UK", 576, 240, """{"tool":"screen_counterparty","params":"{}"}"""),
                new WorkflowNodeSeed(n4, WorkflowNodeKinds.Decision, "Hit?",                  null,                  832, 240, """{"expr":"screen.hits.length > 0","yesLabel":"Hit","noLabel":"Clear"}"""),
                new WorkflowNodeSeed(n5, WorkflowNodeKinds.Human,    "Compliance review",     "if hit",              1088, 144, """{"group":"Compliance","sla":"4h"}"""),
                new WorkflowNodeSeed(n6, WorkflowNodeKinds.Emit,     "compliance.recheck.done", null,                1088, 336, """{"event":"compliance.recheck.done"}"""),
                new WorkflowNodeSeed(n7, WorkflowNodeKinds.End,      "Cleared",                null,                  1344, 240, "{}"),
            },
            Edges: new[]
            {
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0005-eeee-000000000001"), n1, n2, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0005-eeee-000000000002"), n2, n3, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0005-eeee-000000000003"), n3, n4, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0005-eeee-000000000004"), n4, n5, 0, "hit"),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0005-eeee-000000000005"), n4, n6, 1, "clear"),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0005-eeee-000000000006"), n5, n7, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0005-eeee-000000000007"), n6, n7, 0, null),
            },
            Comments: Array.Empty<WorkflowCommentSeed>(),
            Versions: new[]
            {
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0005-vvvv-000000000002"), "v3.2", "Add UK sanctions list to the screening fan-out.", "Maria", "#eb5c37", now.AddDays(-2)),
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0005-vvvv-000000000001"), "v3.0", "Restructure: emit instead of notify when clear.", "Aonik", "#055a60", now.AddDays(-90)),
            },
            Runs: BuildScheduledRuns(now, "kyc", 6, 920, 0.99, n1, n2, n3, n4, n5, n6, n7));
    }

    // ── monthly_close ──────────────────────────────────────────────────

    private static WorkflowSeed BuildMonthlyClose(Func<string, Guid> agent, DateTime now)
    {
        var n1 = Guid.Parse("11111111-aaaa-0006-0000-000000000001");
        var n2 = Guid.Parse("11111111-aaaa-0006-0000-000000000002");
        var n3 = Guid.Parse("11111111-aaaa-0006-0000-000000000003");
        var n4 = Guid.Parse("11111111-aaaa-0006-0000-000000000004");
        var n5 = Guid.Parse("11111111-aaaa-0006-0000-000000000005");
        var n6 = Guid.Parse("11111111-aaaa-0006-0000-000000000006");
        var n7 = Guid.Parse("11111111-aaaa-0006-0000-000000000007");
        var n8 = Guid.Parse("11111111-aaaa-0006-0000-000000000008");

        return new WorkflowSeed(
            WorkflowId: Guid.Parse("11111111-aaaa-0006-6666-666666666666"),
            Slug: "monthly_close",
            Name: "Month-end close",
            Description: "Sequences the close playbook end-to-end. Accruals, FX revaluation, intercompany eliminations, sign-off.",
            OwnerAgentId: agent("Close"),
            OwnerColor: "#0097a9",
            ContributorAgentIds: new[] { agent("Ledger"), agent("FX") },
            State: WorkflowStates.Active,
            Version: "v2.7",
            AutoRetry: false,
            TriggerCount: 1,
            Nodes: new[]
            {
                new WorkflowNodeSeed(n1, WorkflowNodeKinds.Trigger, "Last business day", "schedule.monthly_close", 64, 240, """{"source":"schedule.monthly_close"}"""),
                new WorkflowNodeSeed(n2, WorkflowNodeKinds.Agent,   "Ledger · post accruals", null, 320, 240, """{"agent":"Ledger","task":"Post period accruals."}"""),
                new WorkflowNodeSeed(n3, WorkflowNodeKinds.Agent,   "FX · revalue balances",   null, 576, 240, """{"agent":"FX","task":"Revalue foreign-currency balances."}"""),
                new WorkflowNodeSeed(n4, WorkflowNodeKinds.Agent,   "Ledger · intercompany",   null, 832, 240, """{"agent":"Ledger","task":"Eliminate intercompany balances."}"""),
                new WorkflowNodeSeed(n5, WorkflowNodeKinds.Human,   "Controller sign-off",     "mandatory", 1088, 240, """{"group":"Treasury","sla":"24h"}"""),
                new WorkflowNodeSeed(n6, WorkflowNodeKinds.Tool,    "lock_period",             null, 1344, 240, """{"tool":"lock_period","params":"{}"}"""),
                new WorkflowNodeSeed(n7, WorkflowNodeKinds.Notify,  "Close package · email",   null, 1600, 240, """{"channel":"email","template":"close_package"}"""),
                new WorkflowNodeSeed(n8, WorkflowNodeKinds.End,     "Period closed",            null, 1856, 240, "{}"),
            },
            Edges: new[]
            {
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0006-eeee-000000000001"), n1, n2, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0006-eeee-000000000002"), n2, n3, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0006-eeee-000000000003"), n3, n4, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0006-eeee-000000000004"), n4, n5, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0006-eeee-000000000005"), n5, n6, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0006-eeee-000000000006"), n6, n7, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0006-eeee-000000000007"), n7, n8, 0, null),
            },
            Comments: Array.Empty<WorkflowCommentSeed>(),
            Versions: new[]
            {
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0006-vvvv-000000000001"), "v2.7", "Add intercompany elimination step before lock.", "Aonik", "#055a60", now.AddDays(-17)),
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0006-vvvv-000000000000"), "v2.0", "Initial close playbook.",                          "Aonik", "#055a60", now.AddDays(-180)),
            },
            // Long-running, no runs in last 24h — match template.
            Runs: Array.Empty<WorkflowRunSeed>());
    }

    // ── spend_anomaly ──────────────────────────────────────────────────

    private static WorkflowSeed BuildSpendAnomaly(Func<string, Guid> agent, DateTime now)
    {
        var n1 = Guid.Parse("11111111-aaaa-0007-0000-000000000001");
        var n2 = Guid.Parse("11111111-aaaa-0007-0000-000000000002");
        var n3 = Guid.Parse("11111111-aaaa-0007-0000-000000000003");
        var n4 = Guid.Parse("11111111-aaaa-0007-0000-000000000004");
        var n5 = Guid.Parse("11111111-aaaa-0007-0000-000000000005");
        var n6 = Guid.Parse("11111111-aaaa-0007-0000-000000000006");

        return new WorkflowSeed(
            WorkflowId: Guid.Parse("11111111-aaaa-0007-7777-777777777777"),
            Slug: "spend_anomaly",
            Name: "Spend anomaly review",
            Description: "When a spend category exceeds its 30-day rolling average by more than σ, surface a narrative for review.",
            OwnerAgentId: agent("Insights"),
            OwnerColor: "#d4a843",
            ContributorAgentIds: Array.Empty<Guid>(),
            State: WorkflowStates.Draft,
            Version: "v0.3",
            AutoRetry: false,
            TriggerCount: 1,
            Nodes: new[]
            {
                new WorkflowNodeSeed(n1, WorkflowNodeKinds.Trigger,  "Daily roll-up", "schedule.daily", 64, 240, """{"source":"schedule.daily"}"""),
                new WorkflowNodeSeed(n2, WorkflowNodeKinds.Tool,     "aggregate_spend", "by category", 320, 240, """{"tool":"aggregate_spend","params":"{}"}"""),
                new WorkflowNodeSeed(n3, WorkflowNodeKinds.Decision, "Anomaly?",        "> 2σ",         576, 240, """{"expr":"zscore > 2","yesLabel":"Yes","noLabel":"No"}"""),
                new WorkflowNodeSeed(n4, WorkflowNodeKinds.Agent,    "Insights · narrative", null,      832, 240, """{"agent":"Insights","task":"Write a narrative summary."}"""),
                new WorkflowNodeSeed(n5, WorkflowNodeKinds.Notify,   "Post to My Space", null,           1088, 240, """{"channel":"in_app","template":"spend_anomaly"}"""),
                new WorkflowNodeSeed(n6, WorkflowNodeKinds.End,      "Review filed",     null,           1344, 240, "{}"),
            },
            Edges: new[]
            {
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0007-eeee-000000000001"), n1, n2, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0007-eeee-000000000002"), n2, n3, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0007-eeee-000000000003"), n3, n4, 0, "yes"),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0007-eeee-000000000004"), n3, n6, 1, "no"),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0007-eeee-000000000005"), n4, n5, 0, null),
                new WorkflowEdgeSeed(Guid.Parse("11111111-aaaa-0007-eeee-000000000006"), n5, n6, 0, null),
            },
            Comments: Array.Empty<WorkflowCommentSeed>(),
            Versions: new[]
            {
                new WorkflowVersionSeed(Guid.Parse("11111111-aaaa-0007-vvvv-000000000001"), "v0.3", "Tweaked threshold from 1.5σ to 2σ.", "Rafa", "#7b76b6", now.AddHours(-4)),
            },
            Runs: BuildScheduledRuns(now, "anomaly", 3, 5400, 0.85, n1, n2, n3, n4, n5, n6));
    }

    // ── Helper: synthesise N runs spanning the last 24h with the given
    //    weighted success rate. Sequence walks all nodes in order.
    private static IReadOnlyList<WorkflowRunSeed> BuildScheduledRuns(
        DateTime now,
        string slugPrefix,
        int count,
        int avgMs,
        double successRate,
        params Guid[] nodes)
    {
        var runs = new List<WorkflowRunSeed>();
        var rng = new Random(slugPrefix.GetHashCode());
        var failurePoint = Math.Max(0, nodes.Length - 2);

        for (var i = 0; i < count; i++)
        {
            var startedAt = now.AddMinutes(-(i + 1) * (24 * 60 / Math.Max(1, count)));
            var jitter = rng.Next(-300, 300);
            var duration = Math.Max(80, avgMs + jitter);
            var success = rng.NextDouble() < successRate;
            var status = success ? WorkflowRunStatuses.Success : WorkflowRunStatuses.Failed;

            runs.Add(new WorkflowRunSeed(
                RunId: Guid.NewGuid(),
                StartedAt: startedAt,
                CompletedAt: startedAt.AddMilliseconds(duration),
                Status: status,
                DurationMs: duration,
                StartedBy: success ? "auto" : "tool: read_timeout",
                Sequence: success ? nodes : nodes.Take(failurePoint).ToArray()));
        }
        return runs;
    }
}

// ── Records ─────────────────────────────────────────────────────────────

internal sealed record WorkflowSeed(
    Guid WorkflowId,
    string Slug,
    string Name,
    string Description,
    Guid OwnerAgentId,
    string OwnerColor,
    IReadOnlyList<Guid> ContributorAgentIds,
    string State,
    string Version,
    bool AutoRetry,
    int TriggerCount,
    IReadOnlyList<WorkflowNodeSeed> Nodes,
    IReadOnlyList<WorkflowEdgeSeed> Edges,
    IReadOnlyList<WorkflowCommentSeed> Comments,
    IReadOnlyList<WorkflowVersionSeed> Versions,
    IReadOnlyList<WorkflowRunSeed> Runs);

internal sealed record WorkflowNodeSeed(
    Guid NodeId,
    string Kind,
    string Label,
    string? Summary,
    int X,
    int Y,
    string? ParamsJson);

internal sealed record WorkflowEdgeSeed(
    Guid EdgeId,
    Guid FromNodeId,
    Guid ToNodeId,
    int FromIndex,
    string? Label);

internal sealed record WorkflowCommentSeed(
    Guid CommentId,
    int X,
    int Y,
    string Author,
    string Body);

internal sealed record WorkflowVersionSeed(
    Guid VersionId,
    string Tag,
    string Message,
    string AuthorName,
    string AuthorColor,
    DateTime CreatedAt);

internal sealed record WorkflowRunSeed(
    Guid RunId,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string Status,
    int DurationMs,
    string StartedBy,
    IReadOnlyList<Guid> Sequence);
