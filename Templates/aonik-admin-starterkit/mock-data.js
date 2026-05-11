// Mock background-job data shaped to match the real Aonik jobService types.
// ScheduledJobSummary { jobName, groupName, description, cronExpression,
//   status, nextFireTimeUtc, previousFireTimeUtc, displayName, lastOutcome,
//   lastOutcomeSummary, lastDurationMs }
// + we add a small client-side `history` array (last 20 runs) for the sparkline,
//   and a `lastRun` object holding the rich slide-over content (logs, params,
//   stack trace, step trace).

const NOW = Date.now();
const m  = (n) => NOW - n * 60_000;
const h  = (n) => NOW - n * 3_600_000;
const d  = (n) => NOW - n * 86_400_000;
const iso = (t) => new Date(t).toISOString();

// Build a synthetic history of 20 runs given a final outcome bias.
function buildHistory(seed, finalOutcome, baseDur, intervalMin) {
  const rand = mulberry32(seed);
  const out = [];
  let t = NOW - intervalMin * 60_000;
  for (let i = 19; i >= 0; i--) {
    let outcome = 'Succeeded';
    const r = rand();
    if (finalOutcome === 'Failed' && i < 4) {
      // recent failures cluster
      outcome = r < 0.7 ? 'Failed' : 'Succeeded';
    } else if (finalOutcome === 'TimedOut' && i === 0) {
      outcome = 'TimedOut';
    } else if (r < 0.06) {
      outcome = 'Skipped';
    } else if (r < 0.09) {
      outcome = 'Failed';
    }
    const jitter = 0.7 + rand() * 0.6;
    out.push({
      idx: 20 - i,
      outcome,
      durationMs: Math.round(baseDur * jitter * (outcome === 'TimedOut' ? 4 : 1)),
      firedAt: t - i * intervalMin * 60_000,
    });
  }
  return out;
}

function mulberry32(a) {
  return function() {
    let t = (a += 0x6D2B79F5);
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

const JOBS = [
  // ── 1. Recent FAILURE — bank-feed sync ─────────────────────────────────
  {
    jobName: 'BankFeed.Sync',
    groupName: 'Integrations',
    displayName: 'Bank feed sync',
    description: 'Pulls transaction batches from connected bank providers and queues them for matching.',
    cronExpression: '0 */15 * * * ?',
    status: 'Active',
    nextFireTimeUtc: iso(m(-9)),       // in 9 min
    previousFireTimeUtc: iso(m(6)),
    lastOutcome: 'Failed',
    lastOutcomeSummary: 'Mono provider returned 502 Bad Gateway after 3 retries — 0 of 4 institutions synced.',
    lastDurationMs: 38_240,
    history: buildHistory(11, 'Failed', 22_000, 15),
    lastRun: {
      runId: 'run_01HZWQ9K3M7N2P4R6T8V0X2Y4',
      fireInstanceId: 'fire_d6f1a23e',
      triggeredBy: 'Schedule',
      startedAt: iso(m(7)),
      endedAt: iso(m(6)),
      durationMs: 38_240,
      params: {
        institutionFilter: null,
        sinceCursor: '2026-05-04T13:00:00Z',
        retryPolicy: { maxAttempts: 3, backoffMs: 2000 },
      },
      steps: [
        { name: 'load_credentials',     status: 'ok',     durationMs: 142 },
        { name: 'open_provider_session',status: 'ok',     durationMs: 980 },
        { name: 'fetch_mono_batch',     status: 'failed', durationMs: 36_120,
          message: 'HTTP 502 after 3 retries (mono.co)' },
        { name: 'fetch_okra_batch',     status: 'skipped',durationMs: 0 },
        { name: 'persist_transactions', status: 'skipped',durationMs: 0 },
      ],
      error: {
        type: 'UpstreamProviderException',
        message: 'Mono provider returned 502 Bad Gateway after 3 retries.',
        stack: `Aonik.Workers.Exceptions.UpstreamProviderException: Mono provider returned 502 Bad Gateway after 3 retries.
   at Aonik.Workers.BankFeeds.MonoClient.FetchBatchAsync(SyncCursor cursor, CancellationToken ct) in /src/Workers/BankFeeds/MonoClient.cs:line 184
   at Aonik.Workers.BankFeeds.BankFeedSyncJob.SyncProviderAsync(IBankProvider provider, CancellationToken ct) in /src/Workers/BankFeeds/BankFeedSyncJob.cs:line 87
   at Aonik.Workers.BankFeeds.BankFeedSyncJob.Execute(IJobExecutionContext context) in /src/Workers/BankFeeds/BankFeedSyncJob.cs:line 41
   at Quartz.Core.JobRunShell.RunAsync(CancellationToken cancellationToken)
 ---> System.Net.Http.HttpRequestException: Response status code does not indicate success: 502 (Bad Gateway).
   at System.Net.Http.HttpResponseMessage.EnsureSuccessStatusCode()
   at Aonik.Workers.BankFeeds.MonoClient.<>c__DisplayClass4_0.<<FetchBatchAsync>b__0>d.MoveNext()
--- End of inner exception stack trace ---`,
      },
      logs: [
        { t: '14:15:00.142', level: 'info',  msg: 'Loaded credentials for 4 institutions' },
        { t: '14:15:01.122', level: 'info',  msg: 'Opened provider session · session_id=ps_2f8a1' },
        { t: '14:15:01.404', level: 'info',  msg: 'Fetching mono batch · cursor=2026-05-04T13:00:00Z' },
        { t: '14:15:13.002', level: 'warn',  msg: 'mono.co returned 502 (attempt 1/3) · retry in 2s' },
        { t: '14:15:27.331', level: 'warn',  msg: 'mono.co returned 502 (attempt 2/3) · retry in 4s' },
        { t: '14:15:37.804', level: 'warn',  msg: 'mono.co returned 502 (attempt 3/3) · giving up' },
        { t: '14:15:38.240', level: 'error', msg: 'UpstreamProviderException at BankFeedSyncJob.cs:184' },
        { t: '14:15:38.241', level: 'info',  msg: 'Job halted · 0 of 4 institutions synced' },
      ],
    },
    agent: {
      name: 'Ops',
      confidence: 0.91,
      summary: 'mono.co has returned 502s for the last 3 runs. Their status page reports a provider incident.',
      action: 'Pause Bank feed sync for 30 min and retry once Mono recovers.',
      reasoning: 'Failure pattern is upstream-only (Okra and Stitch synced cleanly on parallel jobs). 30-min pause matches Mono\'s typical incident window.',
    },
  },

  // ── 2. RUNNING — invoice matching ──────────────────────────────────────
  {
    jobName: 'Reconciliation.MatchInvoices',
    groupName: 'Finance',
    displayName: 'Invoice matching',
    description: 'Matches unsettled invoices against bank transactions using ML similarity + rule-based heuristics.',
    cronExpression: '0 0 */1 * * ?',
    status: 'Active',
    nextFireTimeUtc: iso(h(-1) + m(48)),
    previousFireTimeUtc: iso(h(1)),
    lastOutcome: 'Running',
    lastOutcomeSummary: 'In progress · 1,284 of 2,140 invoices scored.',
    lastDurationMs: null,
    history: buildHistory(2, 'Succeeded', 92_000, 60),
    lastRun: {
      runId: 'run_01HZWS4T8M2N9P1R5T7V0X4Y6',
      fireInstanceId: 'fire_8a2c11b',
      triggeredBy: 'Schedule',
      startedAt: iso(m(2)),
      endedAt: null,
      durationMs: null,
      params: {
        confidenceThreshold: 0.78,
        autoApply: false,
        scope: 'org_primrose',
      },
      steps: [
        { name: 'load_unsettled_invoices', status: 'ok',      durationMs: 312, message: '2,140 invoices loaded' },
        { name: 'load_recent_bank_txns',   status: 'ok',      durationMs: 487, message: '8,902 txns in window' },
        { name: 'score_pairs',             status: 'running', durationMs: 78_400, message: '1,284 / 2,140 scored' },
        { name: 'persist_proposals',       status: 'pending', durationMs: 0 },
      ],
      logs: [
        { t: '15:00:00.312', level: 'info', msg: 'Loaded 2,140 unsettled invoices' },
        { t: '15:00:00.799', level: 'info', msg: 'Loaded 8,902 bank transactions (last 30d window)' },
        { t: '15:00:01.022', level: 'info', msg: 'Starting pair scoring · threshold=0.78' },
        { t: '15:01:18.400', level: 'info', msg: 'Progress · 1,284 / 2,140 (60%)' },
      ],
    },
  },

  // ── 3. SUCCESS — yesterday's payouts ──────────────────────────────────
  {
    jobName: 'Payouts.SettleDaily',
    groupName: 'Finance',
    displayName: 'Daily payout settlement',
    description: 'Settles batched payouts across NGN/USD/GBP rails after EOD cutoff.',
    cronExpression: '0 0 23 * * ?',
    status: 'Active',
    nextFireTimeUtc: iso(h(7) + m(20)),
    previousFireTimeUtc: iso(h(16)),
    lastOutcome: 'Succeeded',
    lastOutcomeSummary: 'Settled 412 payouts · $1,820,400.00 total · 0 retries.',
    lastDurationMs: 142_800,
    history: buildHistory(3, 'Succeeded', 140_000, 1440),
    lastRun: {
      runId: 'run_01HZW8Y7K2M3N4P5R6T7V8X9Y',
      fireInstanceId: 'fire_2b9f4e1',
      triggeredBy: 'Schedule',
      startedAt: iso(h(16) + m(2)),
      endedAt: iso(h(16)),
      durationMs: 142_800,
      params: {
        cutoffUtc: '2026-05-03T23:00:00Z',
        rails: ['NGN', 'USD', 'GBP'],
        dryRun: false,
      },
      steps: [
        { name: 'collect_pending_payouts', status: 'ok', durationMs: 1_240, message: '412 payouts queued' },
        { name: 'apply_fx_rates',          status: 'ok', durationMs: 320 },
        { name: 'submit_to_rails',         status: 'ok', durationMs: 138_400, message: '412 submissions accepted' },
        { name: 'post_journal_entries',    status: 'ok', durationMs: 2_840, message: 'JE-88440 → JE-88851' },
      ],
      success: {
        recordsProcessed: 412,
        totalValue: '$1,820,400.00',
        breakdown: [
          { label: 'NGN payouts', value: 287, amount: '₦1,210,400,000' },
          { label: 'USD payouts', value:  98, amount: '$  892,140.00'   },
          { label: 'GBP payouts', value:  27, amount: '£   71,820.00'   },
        ],
      },
      logs: [
        { t: '23:00:01.240', level: 'info', msg: 'Collected 412 pending payouts' },
        { t: '23:00:01.560', level: 'info', msg: 'Applied FX rates from rate_snapshot_20260503' },
        { t: '23:02:19.960', level: 'info', msg: 'Submitted 412 payouts across 3 rails' },
        { t: '23:02:22.800', level: 'info', msg: 'Posted journal entries JE-88440 → JE-88851' },
        { t: '23:02:22.801', level: 'info', msg: 'Run complete · 0 retries · 0 failures' },
      ],
    },
  },

  // ── 4. PAUSED ─────────────────────────────────────────────────────────
  {
    jobName: 'Compliance.SanctionsRescreen',
    groupName: 'Compliance',
    displayName: 'Sanctions rescreen',
    description: 'Re-screens active counterparties against OFAC + EU consolidated sanctions lists.',
    cronExpression: '0 0 4 * * ?',
    status: 'Paused',
    nextFireTimeUtc: null,
    previousFireTimeUtc: iso(d(2)),
    lastOutcome: 'Succeeded',
    lastOutcomeSummary: 'Paused by ada.okafor@primrose · investigating 4 false positives from 2-day-old run.',
    lastDurationMs: 88_120,
    history: buildHistory(4, 'Succeeded', 86_000, 1440),
    lastRun: {
      runId: 'run_01HZW0X7K2M3N4P5R6T7V8X9Y',
      fireInstanceId: 'fire_4d2a98c',
      triggeredBy: 'Schedule',
      startedAt: iso(d(2) + m(1)),
      endedAt: iso(d(2)),
      durationMs: 88_120,
      params: { screeningSet: 'ofac_eu_consolidated', threshold: 0.92 },
      steps: [
        { name: 'load_counterparties', status: 'ok', durationMs: 4_120 },
        { name: 'screen_batch',        status: 'ok', durationMs: 81_400, message: '14,208 counterparties screened' },
        { name: 'flag_potential_hits', status: 'ok', durationMs: 2_600, message: '4 potential matches flagged for review' },
      ],
      success: {
        recordsProcessed: 14_208,
        flagged: 4,
        message: 'Screening clean. 4 potential matches flagged for human review (all > 0.92 confidence).',
      },
      logs: [
        { t: '04:00:04.120', level: 'info', msg: 'Loaded 14,208 active counterparties' },
        { t: '04:01:25.520', level: 'info', msg: 'Screening complete · 4 potential matches' },
        { t: '04:01:28.120', level: 'info', msg: 'Run complete' },
      ],
    },
  },

  // ── 5. FAILURE — report generation ────────────────────────────────────
  {
    jobName: 'Reports.MonthlyClose',
    groupName: 'Reporting',
    displayName: 'Monthly close report',
    description: 'Generates the monthly P&L, balance sheet, and cash flow statements per legal entity.',
    cronExpression: '0 0 2 1 * ?',
    status: 'Active',
    nextFireTimeUtc: iso(d(28) + h(2)),
    previousFireTimeUtc: iso(d(3)),
    lastOutcome: 'Failed',
    lastOutcomeSummary: 'Period 2026-04 has 3 unposted journal entries — close cannot proceed.',
    lastDurationMs: 4_120,
    history: buildHistory(5, 'Failed', 720_000, 43200),
    lastRun: {
      runId: 'run_01HZVQ3K7M2N9P1R5T7V0X4Y6',
      fireInstanceId: 'fire_91c4f2a',
      triggeredBy: 'Schedule',
      startedAt: iso(d(3) + m(0.07)),
      endedAt: iso(d(3)),
      durationMs: 4_120,
      params: { period: '2026-04', entities: ['primrose-ng', 'primrose-uk', 'primrose-us'] },
      steps: [
        { name: 'validate_period',    status: 'failed', durationMs: 4_120,
          message: '3 unposted journal entries in period 2026-04' },
        { name: 'generate_pnl',       status: 'skipped', durationMs: 0 },
        { name: 'generate_bs',        status: 'skipped', durationMs: 0 },
        { name: 'generate_cf',        status: 'skipped', durationMs: 0 },
        { name: 'distribute_reports', status: 'skipped', durationMs: 0 },
      ],
      error: {
        type: 'PeriodValidationException',
        message: 'Period 2026-04 has 3 unposted journal entries (JE-88102, JE-88197, JE-88203). Close cannot proceed.',
        stack: `Aonik.Workers.Reporting.PeriodValidationException: Period 2026-04 has 3 unposted journal entries.
   at Aonik.Workers.Reporting.PeriodValidator.EnsureClosable(PeriodId period, CancellationToken ct) in /src/Workers/Reporting/PeriodValidator.cs:line 56
   at Aonik.Workers.Reporting.MonthlyCloseJob.Execute(IJobExecutionContext context) in /src/Workers/Reporting/MonthlyCloseJob.cs:line 28
   at Quartz.Core.JobRunShell.RunAsync(CancellationToken cancellationToken)`,
      },
      logs: [
        { t: '02:00:00.412', level: 'info',  msg: 'Validating period 2026-04' },
        { t: '02:00:04.118', level: 'error', msg: '3 unposted JE found — JE-88102, JE-88197, JE-88203' },
        { t: '02:00:04.120', level: 'error', msg: 'PeriodValidationException · halting close' },
      ],
    },
    agent: {
      name: 'Ledger',
      confidence: 0.96,
      summary: 'JE-88102, JE-88197, JE-88203 are draft from 2026-04 with all matched evidence — propose posting them and re-running.',
      action: 'Post 3 draft journals + retry monthly close',
      reasoning: 'Each JE has confidence ≥ 0.94 against bank transactions. Posting them satisfies period validator.',
    },
  },

  // ── 6. SKIPPED ────────────────────────────────────────────────────────
  {
    jobName: 'Cleanup.OrphanedAttachments',
    groupName: 'Maintenance',
    displayName: 'Cleanup orphan attachments',
    description: 'Deletes blob attachments that no longer reference an active record after a 30-day grace period.',
    cronExpression: '0 0 3 * * ?',
    status: 'Active',
    nextFireTimeUtc: iso(h(11) + m(20)),
    previousFireTimeUtc: iso(h(13)),
    lastOutcome: 'Skipped',
    lastOutcomeSummary: 'No orphan attachments past grace window — nothing to clean.',
    lastDurationMs: 1_840,
    history: buildHistory(6, 'Succeeded', 12_000, 1440),
    lastRun: {
      runId: 'run_01HZWFK7M2N3P4R5T6V7X8Y9Z',
      fireInstanceId: 'fire_77a91d3',
      triggeredBy: 'Schedule',
      startedAt: iso(h(13) + m(0.03)),
      endedAt: iso(h(13)),
      durationMs: 1_840,
      params: { graceWindowDays: 30 },
      steps: [
        { name: 'scan_orphans',  status: 'ok',      durationMs: 1_840, message: '0 candidates found' },
        { name: 'delete_blobs',  status: 'skipped', durationMs: 0 },
      ],
      success: {
        recordsProcessed: 0,
        message: 'No orphan attachments past the 30-day grace window. Nothing to clean.',
      },
      logs: [
        { t: '03:00:01.840', level: 'info', msg: 'Scan complete · 0 candidates' },
        { t: '03:00:01.842', level: 'info', msg: 'Skipping delete step · nothing to do' },
      ],
    },
  },

  // ── 7. RETRYING ───────────────────────────────────────────────────────
  {
    jobName: 'Tax.VATFilingExport',
    groupName: 'Compliance',
    displayName: 'VAT filing export',
    description: 'Exports VAT filings for HMRC and FIRS in their respective XML/JSON formats.',
    cronExpression: '0 0 6 * * MON',
    status: 'Active',
    nextFireTimeUtc: iso(d(3) + h(6)),
    previousFireTimeUtc: iso(m(11)),
    lastOutcome: 'Retrying',
    lastOutcomeSummary: 'Attempt 2 of 5 · HMRC sandbox returned 429. Backing off 60s.',
    lastDurationMs: null,
    history: buildHistory(7, 'Succeeded', 38_000, 10080),
    lastRun: {
      runId: 'run_01HZWPK7M2N3P4R5T6V7X8Y9Q',
      fireInstanceId: 'fire_5e1b2a8',
      triggeredBy: 'Schedule',
      startedAt: iso(m(11)),
      endedAt: null,
      durationMs: null,
      params: { jurisdictions: ['UK', 'NG'], period: '2026-Q1' },
      steps: [
        { name: 'build_uk_payload', status: 'ok',       durationMs: 8_400 },
        { name: 'submit_hmrc',      status: 'retrying', durationMs: 32_120, message: 'HTTP 429 · attempt 2/5' },
        { name: 'build_ng_payload', status: 'pending',  durationMs: 0 },
        { name: 'submit_firs',      status: 'pending',  durationMs: 0 },
      ],
      logs: [
        { t: '14:09:08.400', level: 'info',  msg: 'UK payload built · 1,402 transactions' },
        { t: '14:09:18.812', level: 'warn',  msg: 'HMRC sandbox returned 429 (attempt 1/5)' },
        { t: '14:09:48.812', level: 'warn',  msg: 'HMRC sandbox returned 429 (attempt 2/5) · backoff 60s' },
      ],
    },
  },

  // ── 8. SUCCESS — index refresh ────────────────────────────────────────
  {
    jobName: 'Search.RebuildIndex',
    groupName: 'Maintenance',
    displayName: 'Rebuild search index',
    description: 'Rebuilds the org-wide search index (invoices, customers, journals).',
    cronExpression: '0 0 1 * * ?',
    status: 'Active',
    nextFireTimeUtc: iso(h(13) + m(40)),
    previousFireTimeUtc: iso(h(15)),
    lastOutcome: 'Succeeded',
    lastOutcomeSummary: 'Indexed 81,402 documents · 4.2 GB · 0 errors.',
    lastDurationMs: 224_120,
    history: buildHistory(8, 'Succeeded', 220_000, 1440),
    lastRun: {
      runId: 'run_01HZW9YK7M2N3P4R5T6V7X8Y9',
      fireInstanceId: 'fire_3c8d4f1',
      triggeredBy: 'Schedule',
      startedAt: iso(h(15) + m(3.7)),
      endedAt: iso(h(15)),
      durationMs: 224_120,
      params: { full: true, chunkSize: 5000 },
      steps: [
        { name: 'snapshot_sources', status: 'ok', durationMs: 12_400 },
        { name: 'build_segments',   status: 'ok', durationMs: 198_300, message: '81,402 docs across 17 segments' },
        { name: 'swap_alias',       status: 'ok', durationMs: 13_420 },
      ],
      success: {
        recordsProcessed: 81_402,
        message: 'Indexed 81,402 documents into 17 segments. Alias swapped atomically. 0 errors.',
        breakdown: [
          { label: 'Invoices',   value: 38_204 },
          { label: 'Customers',  value:  4_882 },
          { label: 'Journals',   value: 38_316 },
        ],
      },
      logs: [
        { t: '01:00:12.400', level: 'info', msg: 'Snapshot complete · 81,402 source documents' },
        { t: '01:03:30.700', level: 'info', msg: 'Segments built · 17 total · 4.2 GB' },
        { t: '01:03:44.120', level: 'info', msg: 'Alias swapped · old index scheduled for GC' },
      ],
    },
  },

  // ── 9. TIMED OUT ─────────────────────────────────────────────────────
  {
    jobName: 'Forecasting.CashRunway',
    groupName: 'Finance',
    displayName: 'Cash runway forecast',
    description: 'Computes 90-day cash runway forecast using ML model + scheduled commitments.',
    cronExpression: '0 30 * * * ?',
    status: 'Active',
    nextFireTimeUtc: iso(m(-22)),
    previousFireTimeUtc: iso(m(38)),
    lastOutcome: 'TimedOut',
    lastOutcomeSummary: 'Exceeded 5m budget at scenario_simulation step.',
    lastDurationMs: 300_000,
    history: buildHistory(9, 'TimedOut', 180_000, 60),
    lastRun: {
      runId: 'run_01HZWNQ7M2N3P4R5T6V7X8Y9P',
      fireInstanceId: 'fire_a72b8e2',
      triggeredBy: 'Schedule',
      startedAt: iso(m(43)),
      endedAt: iso(m(38)),
      durationMs: 300_000,
      params: { horizonDays: 90, scenarios: 1000 },
      steps: [
        { name: 'load_commitments',     status: 'ok',       durationMs: 4_120 },
        { name: 'load_historical',      status: 'ok',       durationMs: 18_400 },
        { name: 'scenario_simulation',  status: 'timedout', durationMs: 277_480, message: 'Hit 5m budget at scenario 612 / 1000' },
      ],
      error: {
        type: 'JobExecutionTimeoutException',
        message: 'Job exceeded its 5-minute execution budget at step scenario_simulation (612 / 1000 scenarios complete).',
        stack: `Aonik.Workers.JobExecutionTimeoutException: Job exceeded its 5-minute execution budget.
   at Aonik.Workers.Forecasting.CashRunwayJob.Execute(IJobExecutionContext context) in /src/Workers/Forecasting/CashRunwayJob.cs:line 112
   at Quartz.Core.JobRunShell.RunAsync(CancellationToken cancellationToken)`,
      },
      logs: [
        { t: '13:42:04.120', level: 'info',  msg: 'Loaded 18,402 commitments' },
        { t: '13:42:22.520', level: 'info',  msg: 'Loaded 24 months historical' },
        { t: '13:46:59.880', level: 'warn',  msg: 'Scenario simulation slow · 612 / 1000 at 4m 37s' },
        { t: '13:47:00.000', level: 'error', msg: 'Execution budget exceeded · halting' },
      ],
    },
    agent: {
      name: 'Ops',
      confidence: 0.84,
      summary: 'Scenario count was raised from 500 → 1000 last week. p95 runtime is now 6m 12s vs. the 5m budget.',
      action: 'Lower scenario count to 750 OR raise execution budget to 8m.',
      reasoning: 'Either change brings p95 within budget. Lowering scenarios costs 0.4% accuracy on backtests.',
    },
  },

  // ── 10. QUEUED ────────────────────────────────────────────────────────
  {
    jobName: 'Notifications.WeeklyDigest',
    groupName: 'Notifications',
    displayName: 'Weekly digest email',
    description: 'Sends weekly activity digest to all org admins.',
    cronExpression: '0 0 8 * * MON',
    status: 'Active',
    nextFireTimeUtc: iso(d(3) + h(8)),
    previousFireTimeUtc: iso(d(4)),
    lastOutcome: 'Queued',
    lastOutcomeSummary: 'Awaiting next scheduled fire · 3d from now.',
    lastDurationMs: 6_840,
    history: buildHistory(10, 'Succeeded', 6_500, 10080),
    lastRun: {
      runId: 'run_01HZW2YK7M2N3P4R5T6V7X8Y9',
      fireInstanceId: 'fire_d4f1a23',
      triggeredBy: 'Schedule',
      startedAt: iso(d(4) + m(0.11)),
      endedAt: iso(d(4)),
      durationMs: 6_840,
      params: { recipients: 14 },
      steps: [
        { name: 'compose_digest', status: 'ok', durationMs: 2_120 },
        { name: 'send_emails',    status: 'ok', durationMs: 4_720, message: '14 sent · 0 bounced' },
      ],
      success: {
        recordsProcessed: 14,
        message: '14 digests sent · 0 bounced · 0 deferred.',
      },
      logs: [
        { t: '08:00:02.120', level: 'info', msg: 'Composed digest · 14 recipients' },
        { t: '08:00:06.840', level: 'info', msg: 'All 14 emails accepted by SMTP' },
      ],
    },
  },

  // ── 11. DISABLED ──────────────────────────────────────────────────────
  {
    jobName: 'Legacy.MigrateXeroV1',
    groupName: 'Legacy',
    displayName: 'Migrate Xero v1 records',
    description: '[Deprecated] One-time migration job kept for rollback during the v1 → v2 cutover.',
    cronExpression: '0 0 2 * * ?',
    status: 'Disabled',
    nextFireTimeUtc: null,
    previousFireTimeUtc: iso(d(45)),
    lastOutcome: 'Succeeded',
    lastOutcomeSummary: 'Disabled by ada.okafor on 2026-03-20 — cutover complete.',
    lastDurationMs: 412_000,
    history: buildHistory(12, 'Succeeded', 410_000, 1440),
  },

  // ── 12. SUCCESS — frequent ────────────────────────────────────────────
  {
    jobName: 'Webhooks.RetryDeadLetter',
    groupName: 'Integrations',
    displayName: 'Retry webhook dead-letter',
    description: 'Retries webhook deliveries that failed and were parked in the dead-letter queue.',
    cronExpression: '0 */5 * * * ?',
    status: 'Active',
    nextFireTimeUtc: iso(m(-3)),
    previousFireTimeUtc: iso(m(2)),
    lastOutcome: 'Succeeded',
    lastOutcomeSummary: 'Replayed 12 deliveries · 11 ok, 1 still failing (will retry).',
    lastDurationMs: 4_240,
    history: buildHistory(13, 'Succeeded', 4_200, 5),
    lastRun: {
      runId: 'run_01HZWYK7M2N3P4R5T6V7X8Y9R',
      fireInstanceId: 'fire_19b4e2a',
      triggeredBy: 'Schedule',
      startedAt: iso(m(2.07)),
      endedAt: iso(m(2)),
      durationMs: 4_240,
      params: { maxBatch: 50 },
      steps: [
        { name: 'pop_batch',  status: 'ok', durationMs: 240, message: '12 deliveries' },
        { name: 'replay',     status: 'ok', durationMs: 3_900 },
        { name: 'requeue_failed', status: 'ok', durationMs: 100, message: '1 requeued' },
      ],
      success: {
        recordsProcessed: 12,
        message: 'Replayed 12 dead-letter webhook deliveries. 11 succeeded, 1 still failing (requeued).',
      },
      logs: [
        { t: '14:18:00.240', level: 'info', msg: 'Popped 12 deliveries from DLQ' },
        { t: '14:18:04.140', level: 'info', msg: 'Replay complete · 11 ok · 1 failed' },
        { t: '14:18:04.240', level: 'info', msg: 'Requeued 1 delivery for next run' },
      ],
    },
  },
];

// Roll up KPIs over the fleet.
const FLEET = (() => {
  const total = JOBS.length;
  const failing = JOBS.filter(j => j.lastOutcome === 'Failed' || j.lastOutcome === 'TimedOut').length;
  const running = JOBS.filter(j => j.lastOutcome === 'Running' || j.lastOutcome === 'Retrying').length;
  const paused  = JOBS.filter(j => j.status === 'Paused' || j.status === 'Disabled').length;

  const allRuns = JOBS.flatMap(j => j.history || []);
  const ok = allRuns.filter(r => r.outcome === 'Succeeded').length;
  const successRate = ((ok / allRuns.length) * 100).toFixed(1);

  return { total, failing, running, paused, successRate, totalRuns: allRuns.length };
})();

window.JOBS = JOBS;
window.FLEET = FLEET;
