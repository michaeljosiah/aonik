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

// ═══════════════════════════════════════════════════════════════════════════
// Commerce · Make — maker-operations mock data (Spec 058, landed Specs 050–057)
// ═══════════════════════════════════════════════════════════════════════════
// Continuous with the wellness-food CM_PRODUCTS catalog (screens/commerce-
// catalog.jsx): recipes attach to EXISTING variant SKUs (GRN-ALM-500,
// SHOT-GIN-1, DRK-CB-250, DRK-SMO-GIN, …) plus two food-service dishes in
// CM_MAKE_PRODUCTS below. Pinned family economics (Spec 058 R9) reproduced
// exactly: jollof ₦400/portion cost · 80% margin row; rice alert "2 kg
// available, reorder at 5 kg"; 25 kg rice sack @ ₦28,000 ⇒ ₦1,120/kg; prep
// netting 10 on hand − 8 reserved = 2 available vs 5 required ⇒ shortfall 3.
// Walk-through id chains (§14):
//   rice:   al-0458 (Ordered) → po_0112 (Pending, 15/25 received)
//           → rcpt_0141 (posted short, alert KEPT) → rice cost row ₦1,120
//   tomato: al-0431 (Resolved) → po_0104 (Complete)
//           → rcpt_0138 (posted full) → tomato cost row ₦800
// All timestamps are fixed strings (deterministic; "today" = Fri 3 Jul 2026,
// "this week" = half-open [2026-06-29, 2026-07-06) UTC).

// ─── Make-side products (existing CM_PRODUCTS shape) ────────────────────────
// The two plated dishes the 050–057 family's canonical examples need. They are
// NOT injected into CM_PRODUCTS (screens/*.jsx are untouched); make-side
// screens read them from here, or use cmAllProducts() for the merged set.
// cat 'meals' is intentionally outside CM_CATEGORIES (food-service dishes,
// not the retail rail) — cmCatName('meals') safely renders '—'.
const CM_MAKE_PRODUCTS = [
  { id: 'p-jollof', name: 'Jollof Rice (portion)', slug: 'jollof-rice-portion', cat: 'meals', kind: 'simple', status: 'active', emoji: '🍛', color: '#c2410c', tags: ['food-service'], media: 2,
    variants: [{ sku: 'JLF-RICE-1', opt: 'Single portion', weight: 350, active: true, ngn: 2000, gbp: 3.20, onHand: 38, reserved: 6 }] },
  { id: 'p-beefsrd', name: 'Seared Beef (portion)', slug: 'seared-beef-portion', cat: 'meals', kind: 'simple', status: 'active', emoji: '🥩', color: '#7f1d1d', tags: ['food-service'], media: 1,
    variants: [{ sku: 'BEEF-SRD-1', opt: 'Single portion', weight: 200, active: true, ngn: 1500, gbp: 2.40, onHand: 12, reserved: 2 }] },
];

// ─── Ingredients (Specs 050/051/052) ────────────────────────────────────────
// cost: effective-dated; history windows are contiguous half-open [from, to)
// with EXACTLY ONE open window (051). Rice carries the rich history + the one
// SCHEDULED future cost (its open window starts 2026-07-14 > today).
// unitLocked: the 050/051 guard — unit is immutable once recipes or cost rows
// reference the ingredient. Oat milk is the UNCOSTED ingredient (cost: null).
const CM_INGREDIENTS = [
  { id: 'ing-rice', name: 'Long-grain rice', emoji: '🍚', unit: 'kg', cat: 'Grains & staples',
    cost: { current: 1120, ccy: 'NGN', since: '2026-06-26', scheduled: { cost: 1180, from: '2026-07-14', source: 'manual reprice — supplier notified an increase' } },
    history: [
      { from: '2026-04-01', to: '2026-05-10', cost: 1000, source: 'manual' },
      { from: '2026-05-10', to: '2026-06-26', cost: 1080, source: 'manual' },
      { from: '2026-06-26', to: '2026-07-14', cost: 1120, source: 'goods receipt RCPT-2026-0141' },
      { from: '2026-07-14', to: null, cost: 1180, scheduled: true, source: 'manual reprice' },
    ],
    onHand: 10, reserved: 8, reorderPoint: 5, reorderQty: 25, active: true, unitLocked: true },
  { id: 'ing-tomato', name: 'Plum tomatoes', emoji: '🍅', unit: 'kg', cat: 'Fresh produce',
    cost: { current: 800, ccy: 'NGN', since: '2026-06-15' },
    history: [
      { from: '2026-05-01', to: '2026-06-15', cost: 700, source: 'manual' },
      { from: '2026-06-15', to: null, cost: 800, source: 'goods receipt RCPT-2026-0138' },
    ],
    onHand: 18, reserved: 4, reorderPoint: 10, reorderQty: 20, active: true, unitLocked: true },
  { id: 'ing-beef', name: 'Beef (boneless)', emoji: '🥩', unit: 'kg', cat: 'Proteins',
    cost: { current: 3000, ccy: 'NGN', since: '2026-06-01' },
    history: [
      { from: '2026-05-12', to: '2026-06-01', cost: 2800, source: 'manual' },
      { from: '2026-06-01', to: null, cost: 3000, source: 'manual' },
    ],
    onHand: 12, reserved: 2, reorderPoint: 8, reorderQty: 10, active: true, unitLocked: true },
  { id: 'ing-onion', name: 'Red onions', emoji: '🧅', unit: 'kg', cat: 'Fresh produce',
    cost: { current: 400, ccy: 'NGN', since: '2026-05-20' },
    history: [{ from: '2026-05-20', to: null, cost: 400, source: 'manual' }],
    onHand: 9, reserved: 1.5, reorderPoint: 4, reorderQty: 10, active: true, unitLocked: true },
  { id: 'ing-pepper', name: 'Scotch bonnet peppers', emoji: '🌶️', unit: 'kg', cat: 'Fresh produce',
    cost: { current: 900, ccy: 'NGN', since: '2026-06-10' },
    history: [{ from: '2026-06-10', to: null, cost: 900, source: 'manual' }],
    onHand: 3, reserved: 0.5, reorderPoint: 2, reorderQty: 5, active: true, unitLocked: true },
  { id: 'ing-oats', name: 'Rolled oats', emoji: '🌾', unit: 'kg', cat: 'Grains & staples',
    cost: { current: 1250, ccy: 'NGN', since: '2026-05-05' },
    history: [{ from: '2026-05-05', to: null, cost: 1250, source: 'manual' }],
    onHand: 40, reserved: 6, reorderPoint: 15, reorderQty: 20, active: true, unitLocked: true },
  { id: 'ing-honey', name: 'Wildflower honey', emoji: '🍯', unit: 'L', cat: 'Sweeteners & syrups',
    cost: { current: 5000, ccy: 'NGN', since: '2026-04-18' },
    history: [{ from: '2026-04-18', to: null, cost: 5000, source: 'manual' }],
    onHand: 10, reserved: 1, reorderPoint: null, reorderQty: null, active: true, unitLocked: true },
  { id: 'ing-ginger', name: 'Fresh ginger', emoji: '🫚', unit: 'kg', cat: 'Fresh produce',
    cost: { current: 2500, ccy: 'NGN', since: '2026-06-05' },
    history: [{ from: '2026-06-05', to: null, cost: 2500, source: 'manual' }],
    onHand: 4, reserved: 1.5, reorderPoint: 3, reorderQty: 5, active: true, unitLocked: true },
  { id: 'ing-coffee', name: 'Coffee beans (arabica)', emoji: '☕', unit: 'kg', cat: 'Beverages',
    cost: { current: 9000, ccy: 'NGN', since: '2026-05-25' },
    history: [{ from: '2026-05-25', to: null, cost: 9000, source: 'manual' }],
    onHand: 2, reserved: 0.8, reorderPoint: 2, reorderQty: 5, active: true, unitLocked: true },
  { id: 'ing-oatmilk', name: 'Oat milk', emoji: '🥛', unit: 'L', cat: 'Beverages',
    cost: null,   // UNCOSTED — referenced by the smoothie recipe, so its standard cost is null (051 diagnostics)
    history: [],
    onHand: 6, reserved: 0, reorderPoint: null, reorderQty: null, active: true, unitLocked: true },
  { id: 'ing-gnut', name: 'Groundnut oil', emoji: '🛢️', unit: 'L', cat: 'Oils & fats',
    cost: { current: 3500, ccy: 'NGN', since: '2026-03-01' },
    history: [{ from: '2026-03-01', to: null, cost: 3500, source: 'manual' }],
    onHand: 0, reserved: 0, reorderPoint: null, reorderQty: null, active: false, unitLocked: true,
    note: 'Deactivated 2026-05-30 — menu moved to coconut oil.' },
];

// ─── Recipes (Spec 050 BOM + 051 rollup) ────────────────────────────────────
// components[].qty is per YIELD batch, in the ingredient's base unit.
// Per-portion component cost = qty × ingredient cost.current ÷ yield —
// jollof: rice 1×1,120/4 = 280 · tomato 0.5×800/4 = 100 · onion 0.2×400/4 = 20
// ⇒ perPortionCost 400 (the pinned 050/051/057 example, exact).
// perPortionCost is null when any component is uncosted (smoothie / oat milk).
// GRN-BER-500 and SHOT-TUR-1 deliberately have NO recipe → the "no recipe —
// excluded from prep & costing" diagnostic rows.
const CM_RECIPES = [
  { id: 'rcp-jollof', variantSku: 'JLF-RICE-1', product: 'Jollof Rice (portion)', emoji: '🍛',
    name: 'Signature jollof (party batch)', yield: 4, unit: 'portion',
    components: [
      { ing: 'ing-rice', qty: 1 },
      { ing: 'ing-tomato', qty: 0.5 },
      { ing: 'ing-onion', qty: 0.2 },
    ],
    perPortionCost: 400, ccy: 'NGN', updatedAt: '2026-06-28',
    note: 'Retuned 2026-06-28: rice 1.2 → 1.0 kg per batch. Runs created before then hold the old snapshot (see RUN-2026-0209).' },
  { id: 'rcp-beefsrd', variantSku: 'BEEF-SRD-1', product: 'Seared Beef (portion)', emoji: '🥩',
    name: 'Seared beef (pan batch)', yield: 4, unit: 'portion',
    components: [
      { ing: 'ing-beef', qty: 0.7 },
      { ing: 'ing-pepper', qty: 0.2 },
      { ing: 'ing-onion', qty: 0.3 },
    ],
    perPortionCost: 600, ccy: 'NGN', updatedAt: '2026-06-14' },
  { id: 'rcp-granola', variantSku: 'GRN-ALM-500', product: 'Almond & Honey Granola (500 g)', emoji: '🥣',
    name: 'Almond-honey granola (oven tray)', yield: 10, unit: 'portion',
    components: [
      { ing: 'ing-oats', qty: 4 },
      { ing: 'ing-honey', qty: 1 },
    ],
    perPortionCost: 1000, ccy: 'NGN', updatedAt: '2026-05-30' },
  { id: 'rcp-shot', variantSku: 'SHOT-GIN-1', product: 'Ginger Wellness Shot (Single)', emoji: '🫚',
    name: 'Ginger shot (press batch)', yield: 20, unit: 'portion',
    components: [
      { ing: 'ing-ginger', qty: 1 },
      { ing: 'ing-honey', qty: 0.5 },
    ],
    perPortionCost: 250, ccy: 'NGN', updatedAt: '2026-06-08' },
  { id: 'rcp-coldbrew', variantSku: 'DRK-CB-250', product: 'Cold-Brew Coffee (250 ml)', emoji: '☕',
    name: 'Cold-brew (steep batch)', yield: 8, unit: 'portion',
    components: [{ ing: 'ing-coffee', qty: 0.5 }],
    perPortionCost: 562.5, ccy: 'NGN', updatedAt: '2026-06-02' },   // 4dp-honest, not rounded to 563
  { id: 'rcp-smoothie', variantSku: 'DRK-SMO-GIN', product: 'Green Smoothie (Ginger)', emoji: '🥤',
    name: 'Green smoothie — ginger (blend batch)', yield: 6, unit: 'portion',
    components: [
      { ing: 'ing-oatmilk', qty: 1.5 },
      { ing: 'ing-ginger', qty: 0.3 },
    ],
    perPortionCost: null, ccy: 'NGN', updatedAt: '2026-06-20',
    uncosted: ['ing-oatmilk'] },   // rollup incomplete → "—", never a fake number (051)
];

// ─── Suppliers (Spec 053) ───────────────────────────────────────────────────
// party: linked ⇒ POs carry a Supplier party role; null ⇒ provenance-only
// (053 §11). Albion is GBP — its rows cannot price an NGN PO (053 honesty
// guard) and are excluded from NGN pack suggestions.
// Derived unit price = packPrice ÷ packSize (rice: 28,000 ÷ 25 = ₦1,120/kg).
const CM_SUPPLIERS = [
  { id: 'sup-lagosgrains', name: 'Lagos Grains Co', ccy: 'NGN', lead: 3, terms: 'Net 14',
    party: { id: 'pty_88a2', name: 'Lagos Grains Co Ltd' }, active: true,
    catalog: [
      { ing: 'ing-rice', sku: 'LG-RICE-25', packSize: 25, unit: 'kg', packLabel: '25 kg sack', packPrice: 28000, ccy: 'NGN', lead: 3 },
      { ing: 'ing-oats', sku: 'LG-OAT-10', packSize: 10, unit: 'kg', packLabel: '10 kg bag', packPrice: 12000, ccy: 'NGN', lead: 3 },
      { ing: 'ing-coffee', sku: 'LG-COF-5', packSize: 5, unit: 'kg', packLabel: '5 kg bag', packPrice: 44000, ccy: 'NGN', lead: 5 },
    ] },
  { id: 'sup-freshfarm', name: 'FreshFarm NG', ccy: 'NGN', lead: 2, terms: 'On delivery',
    party: null, active: true,
    linkNote: 'Not party-linked — purchase orders record this supplier as provenance only; no Supplier role is attached to the Order (053 §11).',
    catalog: [
      { ing: 'ing-tomato', sku: 'FF-TOM-10', packSize: 10, unit: 'kg', packLabel: '10 kg crate', packPrice: 7500, ccy: 'NGN', lead: 2 },
      { ing: 'ing-onion', sku: 'FF-ONI-10', packSize: 10, unit: 'kg', packLabel: '10 kg sack', packPrice: 3800, ccy: 'NGN', lead: 2 },
      { ing: 'ing-pepper', sku: 'FF-PEP-5', packSize: 5, unit: 'kg', packLabel: '5 kg crate', packPrice: 4200, ccy: 'NGN', lead: 2 },
      { ing: 'ing-ginger', sku: 'FF-GIN-5', packSize: 5, unit: 'kg', packLabel: '5 kg bag', packPrice: 11500, ccy: 'NGN', lead: 2 },
      { ing: 'ing-beef', sku: 'FF-BEF-10', packSize: 10, unit: 'kg', packLabel: '10 kg box', packPrice: 29000, ccy: 'NGN', lead: 1 },
    ] },
  { id: 'sup-albion', name: 'Albion Foods', ccy: 'GBP', lead: 10, terms: 'Net 30',
    party: null, active: true,
    mismatchNote: 'GBP catalog — rows cannot price an NGN purchase order and are excluded from NGN suggestions (053 currency honesty guard).',
    catalog: [
      { ing: 'ing-oats', sku: 'AL-OAT-20', packSize: 20, unit: 'kg', packLabel: '20 kg sack', packPrice: 26.00, ccy: 'GBP', lead: 10 },
      { ing: 'ing-honey', sku: 'AL-HON-5', packSize: 5, unit: 'L', packLabel: '5 L pail', packPrice: 38.50, ccy: 'GBP', lead: 10 },
      { ing: 'ing-coffee', sku: 'AL-COF-10', packSize: 10, unit: 'kg', packLabel: '10 kg sack', packPrice: 92.00, ccy: 'GBP', lead: 10 },
    ] },
];

// ─── Low-stock alerts (Spec 052) ────────────────────────────────────────────
// Landed vocabulary ONLY: Open | Acknowledged | Ordered | Resolved.
// Open + Acknowledged form the one ACTIVE set (nav badge 3 = al-0491 +
// al-0489 + al-0476). Ordered/Resolved have left the active set — which is
// why a SECOND rice alert (al-0491) may legally open while al-0458 (Ordered)
// still has 10 kg outstanding on PO-2026-0112.
// A re-scan REFRESHES an active alert's snapshot in place (al-0476) — it
// never re-opens it and never opens a duplicate.
const CM_ALERTS = [
  { id: 'al-0491', ref: 'AL-0491', ing: 'ing-rice', name: 'Long-grain rice', emoji: '🍚', unit: 'kg',
    status: 'open', availableAtRaise: 2, reorderPoint: 5, raisedAt: 'Today 07:12',
    message: '2 kg available, reorder at 5 kg',
    note: 'Raised while PO-2026-0112 still has 10 kg outstanding — Ordered alerts are not active, so the scan opened a fresh one.' },
  { id: 'al-0489', ref: 'AL-0489', ing: 'ing-ginger', name: 'Fresh ginger', emoji: '🫚', unit: 'kg',
    status: 'open', availableAtRaise: 2.5, reorderPoint: 3, raisedAt: 'Today 06:45',
    message: '2.5 kg available, reorder at 3 kg' },
  { id: 'al-0476', ref: 'AL-0476', ing: 'ing-coffee', name: 'Coffee beans (arabica)', emoji: '☕', unit: 'kg',
    status: 'acknowledged', availableAtRaise: 1.5, reorderPoint: 2, raisedAt: 'Yesterday 18:40',
    message: '1.5 kg available, reorder at 2 kg',
    acknowledgedAt: 'Yesterday 19:05', acknowledgedBy: 'Oliver Chen',
    refreshedAt: 'Today 06:00', refreshedAvailable: 1.2,
    refreshNote: 'Nightly re-scan refreshed the snapshot (1.5 → 1.2 kg available); status unchanged — an acknowledged alert is never re-opened by a refresh.' },
  { id: 'al-0458', ref: 'AL-0458', ing: 'ing-rice', name: 'Long-grain rice', emoji: '🍚', unit: 'kg',
    status: 'ordered', availableAtRaise: 4, reorderPoint: 5, raisedAt: 'Mon 22 Jun',
    message: '4 kg available, reorder at 5 kg',
    orderedAt: 'Tue 23 Jun', po: 'po_0112', poRef: 'PO-2026-0112' },
  { id: 'al-0431', ref: 'AL-0431', ing: 'ing-tomato', name: 'Plum tomatoes', emoji: '🍅', unit: 'kg',
    status: 'resolved', availableAtRaise: 6, reorderPoint: 10, raisedAt: '12 Jun',
    message: '6 kg available, reorder at 10 kg',
    orderedAt: '12 Jun', po: 'po_0104', poRef: 'PO-2026-0104',
    resolvedAt: '15 Jun', receipt: 'rcpt_0138', receiptRef: 'RCPT-2026-0138' },
];

// ─── Purchase orders (Spec 053) ─────────────────────────────────────────────
// Landed codes ONLY: Draft | Pending | Complete | Cancelled. Submit lands on
// Pending ("submitted to supplier"); there is NO Submitted/Received status —
// partial receipt is the DERIVED received-vs-ordered progress (ProgressCells),
// never a status. lines[].received is cumulative across non-voided receipts.
const CM_POS = [
  { id: 'po_0117', ref: 'PO-2026-0117', status: 'draft', supplier: 'sup-freshfarm', supplierName: 'FreshFarm NG', ccy: 'NGN',
    createdAt: 'Today 08:05', createdBy: 'Oliver Chen', provenance: 'manual',
    lines: [
      { ing: 'ing-ginger', name: 'Fresh ginger', emoji: '🫚', qty: 5, unit: 'kg', unitPrice: 2300, lineTotal: 11500, received: 0 },
      { ing: 'ing-pepper', name: 'Scotch bonnet peppers', emoji: '🌶️', qty: 5, unit: 'kg', unitPrice: 840, lineTotal: 4200, received: 0 },
    ],
    total: 15700, receipts: [], alerts: [] },
  { id: 'po_0112', ref: 'PO-2026-0112', status: 'pending', supplier: 'sup-lagosgrains', supplierName: 'Lagos Grains Co', ccy: 'NGN',
    createdAt: 'Tue 23 Jun', createdBy: 'Oliver Chen', submittedAt: 'Tue 23 Jun', expectedBy: 'Fri 26 Jun',
    provenance: 'from-shortfall', provenanceNote: 'Seeded from low-stock alerts · AL-0458 (pack-rounded to 1 × 25 kg sack, min one pack)',
    lines: [
      { ing: 'ing-rice', name: 'Long-grain rice', emoji: '🍚', qty: 25, unit: 'kg', unitPrice: 1120, lineTotal: 28000, received: 15 },
    ],
    total: 28000, receipts: ['rcpt_0141'], alerts: ['al-0458'] },
  { id: 'po_0119', ref: 'PO-2026-0119', status: 'pending', supplier: 'sup-lagosgrains', supplierName: 'Lagos Grains Co', ccy: 'NGN',
    createdAt: 'Today 08:40', createdBy: 'Oliver Chen', submittedAt: 'Today 08:44', expectedBy: 'Mon 6 Jul',
    provenance: 'manual',
    lines: [
      { ing: 'ing-oats', name: 'Rolled oats', emoji: '🌾', qty: 20, unit: 'kg', unitPrice: 1200, lineTotal: 24000, received: 0 },
    ],
    total: 24000, receipts: [], alerts: [] },
  { id: 'po_0104', ref: 'PO-2026-0104', status: 'complete', supplier: 'sup-freshfarm', supplierName: 'FreshFarm NG', ccy: 'NGN',
    createdAt: '12 Jun', createdBy: 'Oliver Chen', submittedAt: '13 Jun', completedAt: '15 Jun',
    provenance: 'from-shortfall', provenanceNote: 'Seeded from low-stock alerts · AL-0431',
    lines: [
      { ing: 'ing-tomato', name: 'Plum tomatoes', emoji: '🍅', qty: 20, unit: 'kg', unitPrice: 800, lineTotal: 16000, received: 20 },
    ],
    total: 16000, receipts: ['rcpt_0138'], alerts: ['al-0431'] },
  { id: 'po_0109', ref: 'PO-2026-0109', status: 'cancelled', supplier: 'sup-freshfarm', supplierName: 'FreshFarm NG', ccy: 'NGN',
    createdAt: '18 Jun', createdBy: 'Oliver Chen', submittedAt: '18 Jun',
    cancelledAt: '20 Jun', cancelledBy: 'Oliver Chen',
    cancelReason: 'Supplier confirmed a two-week beef stockout — cancelled and resourcing locally.',
    provenance: 'manual',
    staleSubmit: { message: 'Stale submit rejected — this PO changed since it was loaded (compare-and-set guard). Reload to see the cancellation.' },
    lines: [
      { ing: 'ing-beef', name: 'Beef (boneless)', emoji: '🥩', qty: 10, unit: 'kg', unitPrice: 2900, lineTotal: 29000, received: 0 },
    ],
    total: 29000, receipts: [], alerts: [] },
];

// ─── Goods receipts (Spec 054) ──────────────────────────────────────────────
// Claim-first + idempotent: a keyed retry returns the SAME receipt, applied
// once. Posting outcomes: stock applied, effective-dated cost rows written
// (when an actual unit cost is given), alerts resolved — or KEPT when still
// below the reorder point (the short-receipt honesty rule) — and PO
// completion. Over-receipt is rejected outright (cumulative received may
// never exceed ordered; v1 tolerance: none) — see the kind:'rejected' sample.
const CM_RECEIPTS = [
  { id: 'rcpt_0138', ref: 'RCPT-2026-0138', kind: 'posted', po: 'po_0104', poRef: 'PO-2026-0104',
    supplier: 'sup-freshfarm', supplierName: 'FreshFarm NG',
    receivedAt: '15 Jun 11:20', postedBy: 'Oliver Chen', idempotencyKey: 'rcv-7f21c4',
    lines: [
      { ing: 'ing-tomato', name: 'Plum tomatoes', emoji: '🍅', qty: 20, unit: 'kg', ordered: 20, previouslyReceived: 0, actualUnitCost: 800 },
    ],
    outcomes: {
      stockApplied: [{ ing: 'ing-tomato', name: 'Plum tomatoes', qty: 20, unit: 'kg', onHandAfter: 26, availableAfter: 21 }],
      costRowsWritten: [{ ing: 'ing-tomato', name: 'Plum tomatoes', cost: 800, ccy: 'NGN', effectiveFrom: '2026-06-15' }],
      alertsResolved: ['al-0431'],
      alertsKept: [],
      poStatus: 'complete',
      remaining: [],
    },
    retryNote: 'Keyed retry returns this same receipt — applied once (claim-first).' },
  { id: 'rcpt_0141', ref: 'RCPT-2026-0141', kind: 'posted', po: 'po_0112', poRef: 'PO-2026-0112',
    supplier: 'sup-lagosgrains', supplierName: 'Lagos Grains Co',
    receivedAt: 'Fri 26 Jun 09:40', postedBy: 'Oliver Chen', idempotencyKey: 'rcv-2ab9e7',
    lines: [
      { ing: 'ing-rice', name: 'Long-grain rice', emoji: '🍚', qty: 15, unit: 'kg', ordered: 25, previouslyReceived: 0, actualUnitCost: 1120 },
    ],
    outcomes: {
      stockApplied: [{ ing: 'ing-rice', name: 'Long-grain rice', qty: 15, unit: 'kg', onHandAfter: 19, availableAfter: 4 }],
      costRowsWritten: [{ ing: 'ing-rice', name: 'Long-grain rice', cost: 1120, ccy: 'NGN', effectiveFrom: '2026-06-26' }],
      alertsResolved: [],
      alertsKept: [{ alert: 'al-0458', reason: 'Available 4 kg still below reorder point 5 kg — alert kept, not resolved.' }],
      poStatus: 'pending',
      remaining: [{ ing: 'ing-rice', name: 'Long-grain rice', qty: 10, unit: 'kg' }],
    },
    retryNote: 'Keyed retry returns this same receipt — applied once (claim-first).' },
  // Error-state sample — NOT a posted receipt. Cumulative 15 + attempted 12 > ordered 25.
  { id: 'rcpt_rej01', kind: 'rejected', po: 'po_0112', poRef: 'PO-2026-0112', attemptedAt: 'Today 09:02',
    error: {
      code: 'OverReceipt',
      ing: 'ing-rice', name: 'Long-grain rice', unit: 'kg',
      ordered: 25, alreadyReceived: 15, attempted: 12,
      message: 'Over-receipt rejected: Long-grain rice — 15 kg of 25 kg already received; receiving 12 kg would exceed the ordered quantity (v1 tolerance: none).',
    } },
];

// ─── Production sheet — windowed demand (Spec 055) ──────────────────────────
// Demand predicate: paid-or-committed order statuses only; Draft checkouts are
// excluded. Bundle lines explode into component variants (bundleExpanded).
// rows[].orders counts contributing orders per variant; sheet.orders is the
// DISTINCT order count for the window (orders contribute multiple variants).
const CM_PROD_SHEET = {
  window: { label: 'This week', from: '2026-06-29', to: '2026-07-06' },
  orders: 18, portions: 168,
  demandRule: 'Paid-or-committed orders only — Draft checkouts are excluded.',
  rows: [
    { variantSku: 'JLF-RICE-1', name: 'Jollof Rice (portion)', emoji: '🍛', portions: 20, orders: 6, bundleExpanded: false, hasRecipe: true },
    { variantSku: 'BEEF-SRD-1', name: 'Seared Beef (portion)', emoji: '🥩', portions: 12, orders: 4, bundleExpanded: false, hasRecipe: true },
    { variantSku: 'GRN-ALM-500', name: 'Almond & Honey Granola (500 g)', emoji: '🥣', portions: 30, orders: 7, bundleExpanded: true, hasRecipe: true },
    { variantSku: 'SHOT-GIN-1', name: 'Ginger Wellness Shot (Single)', emoji: '🫚', portions: 60, orders: 9, bundleExpanded: false, hasRecipe: true },
    { variantSku: 'DRK-CB-250', name: 'Cold-Brew Coffee (250 ml)', emoji: '☕', portions: 16, orders: 3, bundleExpanded: false, hasRecipe: true },
    { variantSku: 'GRN-BER-500', name: 'Berry Bliss Granola (500 g)', emoji: '🍓', portions: 18, orders: 2, bundleExpanded: false, hasRecipe: false },
    { variantSku: 'SHOT-TUR-1', name: 'Turmeric Shot (Single)', emoji: '🟡', portions: 12, orders: 2, bundleExpanded: false, hasRecipe: false },
  ],
};

// ─── Production orders (Spec 056) ───────────────────────────────────────────
// Landed lifecycle: Planned | Released | InProgress | Completed | Cancelled.
// lines[].snapshot is the PER-PORTION bill FROZEN AT CREATION — release and
// the kitchen sheet replay it; a later recipe edit changes nothing here
// (RUN-2026-0209 deliberately shows rice 0.30/portion vs the live 0.25).
// Release is all-or-nothing: RUN-2026-0221 is blocked by rice (needs 5 kg,
// 2 kg available — nothing applied). Cancelling a released run does NOT
// restock (RUN-2026-0203).
const CM_PROD_ORDERS = [
  { id: 'run_0221', ref: 'RUN-2026-0221', status: 'planned', plannedFor: 'Sat 4 Jul',
    createdAt: 'Today 07:45', createdBy: 'Oliver Chen', fromSheet: true,
    note: 'Created from the production sheet [2026-06-29, 2026-07-06). Recipe snapshots frozen at creation.',
    lines: [
      { variantSku: 'JLF-RICE-1', name: 'Jollof Rice (portion)', emoji: '🍛', plannedPortions: 20,
        snapshot: [
          { ing: 'ing-rice', name: 'Long-grain rice', perPortion: 0.25, unit: 'kg' },
          { ing: 'ing-tomato', name: 'Plum tomatoes', perPortion: 0.125, unit: 'kg' },
          { ing: 'ing-onion', name: 'Red onions', perPortion: 0.05, unit: 'kg' },
        ] },
      { variantSku: 'BEEF-SRD-1', name: 'Seared Beef (portion)', emoji: '🥩', plannedPortions: 12,
        snapshot: [
          { ing: 'ing-beef', name: 'Beef (boneless)', perPortion: 0.175, unit: 'kg' },
          { ing: 'ing-pepper', name: 'Scotch bonnet peppers', perPortion: 0.05, unit: 'kg' },
          { ing: 'ing-onion', name: 'Red onions', perPortion: 0.075, unit: 'kg' },
        ] },
    ],
    releasePreview: [
      { ing: 'ing-rice', name: 'Long-grain rice', emoji: '🍚', required: 5, available: 2, unit: 'kg', ok: false },
      { ing: 'ing-tomato', name: 'Plum tomatoes', emoji: '🍅', required: 2.5, available: 14, unit: 'kg', ok: true },
      { ing: 'ing-beef', name: 'Beef (boneless)', emoji: '🥩', required: 2.1, available: 10, unit: 'kg', ok: true },
      { ing: 'ing-onion', name: 'Red onions', emoji: '🧅', required: 1.9, available: 7.5, unit: 'kg', ok: true },
      { ing: 'ing-pepper', name: 'Scotch bonnet peppers', emoji: '🌶️', required: 0.6, available: 2.5, unit: 'kg', ok: true },
    ],
    releaseBlocked: {
      ing: 'ing-rice', name: 'Long-grain rice', required: 5, available: 2, unit: 'kg',
      message: 'Insufficient stock: Long-grain rice — required 5 kg, available 2 kg. Release applies nothing (all-or-nothing).',
    } },
  { id: 'run_0218', ref: 'RUN-2026-0218', status: 'released', plannedFor: 'Today',
    createdAt: 'Yesterday 17:20', createdBy: 'Oliver Chen', releasedAt: 'Today 06:10',
    note: 'Release drew 12 kg oats + 3 L honey down in one all-or-nothing application.',
    lines: [
      { variantSku: 'GRN-ALM-500', name: 'Almond & Honey Granola (500 g)', emoji: '🥣', plannedPortions: 30,
        snapshot: [
          { ing: 'ing-oats', name: 'Rolled oats', perPortion: 0.4, unit: 'kg' },
          { ing: 'ing-honey', name: 'Wildflower honey', perPortion: 0.1, unit: 'L' },
        ] },
    ] },
  { id: 'run_0215', ref: 'RUN-2026-0215', status: 'inprogress', plannedFor: 'Today',
    createdAt: 'Yesterday 17:15', createdBy: 'Oliver Chen', releasedAt: 'Today 06:15', startedAt: 'Today 08:30',
    lines: [
      { variantSku: 'SHOT-GIN-1', name: 'Ginger Wellness Shot (Single)', emoji: '🫚', plannedPortions: 60,
        snapshot: [
          { ing: 'ing-ginger', name: 'Fresh ginger', perPortion: 0.05, unit: 'kg' },
          { ing: 'ing-honey', name: 'Wildflower honey', perPortion: 0.025, unit: 'L' },
        ] },
    ] },
  { id: 'run_0209', ref: 'RUN-2026-0209', status: 'completed', plannedFor: 'Yesterday',
    createdAt: '27 Jun', createdBy: 'Oliver Chen', releasedAt: 'Yesterday 06:05', startedAt: 'Yesterday 07:00', completedAt: 'Yesterday 13:40',
    snapshotNote: 'Snapshot shows rice 0.30 kg/portion — the live recipe was retuned to 0.25 on 28 Jun, AFTER this run was created. The run consumed its frozen snapshot, never the live recipe.',
    lines: [
      { variantSku: 'JLF-RICE-1', name: 'Jollof Rice (portion)', emoji: '🍛', plannedPortions: 40, producedPortions: 38,
        snapshot: [
          { ing: 'ing-rice', name: 'Long-grain rice', perPortion: 0.30, unit: 'kg' },   // ≠ live 0.25 — frozen at creation
          { ing: 'ing-tomato', name: 'Plum tomatoes', perPortion: 0.125, unit: 'kg' },
          { ing: 'ing-onion', name: 'Red onions', perPortion: 0.05, unit: 'kg' },
        ] },
    ],
    yieldedFinishedGoods: true,
    yielded: [{ variantSku: 'JLF-RICE-1', name: 'Jollof Rice (portion)', qty: 38 }],
    yieldNote: 'Completion yielded 38 finished portions into sellable stock (JLF-RICE-1 on-hand).' },
  { id: 'run_0203', ref: 'RUN-2026-0203', status: 'cancelled', plannedFor: '28 Jun',
    createdAt: '27 Jun', createdBy: 'Oliver Chen', releasedAt: '28 Jun 06:00', cancelledAt: '28 Jun 10:15', cancelledBy: 'Oliver Chen',
    cancelReason: 'Chiller failure — batch scrapped.',
    note: 'Cancelled after release — released runs do not restock; the drawn ingredients stay consumed.',
    lines: [
      { variantSku: 'DRK-CB-250', name: 'Cold-Brew Coffee (250 ml)', emoji: '☕', plannedPortions: 24,
        snapshot: [{ ing: 'ing-coffee', name: 'Coffee beans (arabica)', perPortion: 0.0625, unit: 'kg' }] },
    ] },
];

// ─── Margin report (Spec 057) ───────────────────────────────────────────────
// Revenue is discount-allocated; COGS = qty × per-portion standard cost.
// Unknown COGS ⇒ cogs/margin are NULL ("—"), NEVER zero, and the row's
// revenue is EXCLUDED from the aggregate denominator (surfaced in its own
// unknownCogsRevenue tile). All derived figures (grossMargin, marginPct, row
// status, totals) are COMPUTED from the base rows below, so the §14 figures
// audit reconciles by construction. Jollof pins the family economics:
// ₦400/portion COGS, ₦2,000 price ⇒ 80% margin vs target 65.
const CM_MARGIN = (() => {
  const base = [
    { product: 'p-jollof', variantSku: 'JLF-RICE-1', name: 'Jollof Rice (portion)', emoji: '🍛',
      qty: 62, revenue: 124000, cogsPerUnit: 400, cogs: 24800, targetPct: 65, bundleExpanded: false },
    { product: 'p-alm', variantSku: 'GRN-ALM-500', name: 'Almond & Honey Granola (500 g)', emoji: '🥣',
      qty: 24, revenue: 96000, cogsPerUnit: 1000, cogs: 24000, targetPct: 60, bundleExpanded: true },   // includes box-expanded units at the standalone-price split
    { product: 'p-ginger', variantSku: 'SHOT-GIN-1', name: 'Ginger Wellness Shot (Single)', emoji: '🫚',
      qty: 85, revenue: 76500, cogsPerUnit: 250, cogs: 21250, targetPct: 70, bundleExpanded: false },
    { product: 'p-cb', variantSku: 'DRK-CB-250', name: 'Cold-Brew Coffee (250 ml)', emoji: '☕',
      qty: 40, revenue: 72000, cogsPerUnit: 562.5, cogs: 22500, targetPct: null, bundleExpanded: false },   // no target · cannot judge
    { product: 'p-beefsrd', variantSku: 'BEEF-SRD-1', name: 'Seared Beef (portion)', emoji: '🥩',
      qty: 40, revenue: 60000, cogsPerUnit: 600, cogs: 24000, targetPct: 65, bundleExpanded: false },   // 60% < 65 ⇒ BelowTarget
    { product: 'p-ber', variantSku: 'GRN-BER-500', name: 'Berry Bliss Granola (500 g)', emoji: '🍓',
      qty: 18, revenue: 93600, cogsPerUnit: null, cogs: null, targetPct: 55, bundleExpanded: false,
      unknownReason: 'no recipe' },   // unknown COGS — null, never zero
  ];
  const rows = base.map(r => {
    if (r.cogs == null) return { ...r, grossMargin: null, marginPct: null, status: 'unknown' };
    const grossMargin = r.revenue - r.cogs;
    const marginPct = +((grossMargin / r.revenue) * 100).toFixed(1);
    const status = r.targetPct == null ? 'notarget' : (marginPct >= r.targetPct ? 'above' : 'below');
    return { ...r, grossMargin, marginPct, status };
  });
  const known = rows.filter(r => r.cogs != null);
  const revenue = rows.reduce((a, r) => a + r.revenue, 0);
  const knownCogsRevenue = known.reduce((a, r) => a + r.revenue, 0);
  const cogs = known.reduce((a, r) => a + r.cogs, 0);
  const grossMargin = knownCogsRevenue - cogs;
  const marginPct = +((grossMargin / knownCogsRevenue) * 100).toFixed(1);
  const unknownCogsRevenue = revenue - knownCogsRevenue;
  // What a dishonest zero-cost treatment WOULD claim — for the tile caption only.
  const zeroedCounterfactualPct = +(((revenue - cogs) / revenue) * 100).toFixed(1);
  return {
    window: { label: 'This week', from: '2026-06-29', to: '2026-07-06' },
    currency: 'NGN',
    rows,
    totals: { revenue, knownCogsRevenue, cogs, grossMargin, marginPct, unknownCogsRevenue, zeroedCounterfactualPct },
  };
})();

// ─── Status-tone lookups (landed vocabularies ONLY) ─────────────────────────
const CM_ALERT_STATUS = {
  open:         { tone: 'danger',  label: 'Open' },
  acknowledged: { tone: 'warning', label: 'Acknowledged' },
  ordered:      { tone: 'pending', label: 'Ordered' },
  resolved:     { tone: 'success', label: 'Resolved' },
};
const CM_PO_STATUS = {
  draft:     { tone: 'muted',   label: 'Draft' },
  pending:   { tone: 'warning', label: 'Pending', hint: 'submitted to supplier' },
  complete:  { tone: 'success', label: 'Complete' },
  cancelled: { tone: 'danger',  label: 'Cancelled' },
};
const CM_RUN_STATUS = {
  planned:    { tone: 'muted',   label: 'Planned' },
  released:   { tone: 'pending', label: 'Released' },
  inprogress: { tone: 'warning', label: 'In progress' },
  completed:  { tone: 'success', label: 'Completed' },
  cancelled:  { tone: 'danger',  label: 'Cancelled' },
};
const CM_MARGIN_STATUS = {
  above:    { tone: 'success', label: 'Above target' },
  below:    { tone: 'danger',  label: 'Below target' },
  notarget: { tone: 'muted',   label: 'No target' },
  unknown:  { tone: 'warning', label: 'Unknown COGS' },
};

// ─── Helpers (deterministic — no Date.now, no randomness) ───────────────────
// cmUnit(10, 'kg') → "10 kg" · cmUnit(4.5, 'L') → "4.5 L" ·
// cmUnit(2, '25 kg sack') → "2 × 25 kg sack" (any non-base unit is a pack label).
const cmUnit = (qty, unit) => {
  if (qty == null) return '—';
  const n = typeof qty === 'number' ? qty.toLocaleString('en-NG', { maximumFractionDigits: 4 }) : String(qty);
  if (unit === 'kg' || unit === 'L' || unit === 'each') return n + ' ' + unit;
  return n + ' × ' + unit;
};

// Available = OnHand − Reserved (the 052/055 netting quantity).
const cmIngAvail = (ing) => ing.onHand - ing.reserved;

// Sell-side catalog + the make-side dishes. CM_PRODUCTS is declared by
// screens/commerce-catalog.jsx (a sibling Babel script) — resolved at CALL
// time, so load order never matters.
const cmAllProducts = () =>
  (typeof CM_PRODUCTS !== 'undefined' ? CM_PRODUCTS : (window.CM_PRODUCTS || [])).concat(CM_MAKE_PRODUCTS);

// Prep list for the CM_PROD_SHEET window (Spec 055): requirements exploded
// from recipes, netted against Available (OnHand − Reserved).
// Shortfall = max(required − available, 0); suggested order = whole packs of
// the cheapest same-currency supplier (min one pack), null when shortfall = 0.
// Static + deterministic — the `window` arg is accepted for API shape only.
// Includes the pinned Codex netting row: rice 10 on hand / 8 reserved /
// 5 required ⇒ shortfall 3, suggested "1 × 25 kg sack".
// NOTE coffee: available 1.2 is below its reorder point (active alert) yet
// shortfall is 0 — alerts and prep netting are different lenses.
const CM_PREP_ROWS = [
  { ing: 'ing-rice', name: 'Long-grain rice', emoji: '🍚', unit: 'kg',
    required: 5, onHand: 10, reserved: 8, available: 2, shortfall: 3,
    suggested: { packs: 1, packLabel: '25 kg sack', label: '1 × 25 kg sack', supplier: 'sup-lagosgrains', supplierName: 'Lagos Grains Co', est: 28000, ccy: 'NGN' },
    cheapest: { supplier: 'sup-lagosgrains', supplierName: 'Lagos Grains Co', unitPrice: 1120, packLabel: '25 kg sack' } },
  { ing: 'ing-ginger', name: 'Fresh ginger', emoji: '🫚', unit: 'kg',
    required: 3, onHand: 4, reserved: 1.5, available: 2.5, shortfall: 0.5,
    suggested: { packs: 1, packLabel: '5 kg bag', label: '1 × 5 kg bag', supplier: 'sup-freshfarm', supplierName: 'FreshFarm NG', est: 11500, ccy: 'NGN' },
    cheapest: { supplier: 'sup-freshfarm', supplierName: 'FreshFarm NG', unitPrice: 2300, packLabel: '5 kg bag' } },
  { ing: 'ing-tomato', name: 'Plum tomatoes', emoji: '🍅', unit: 'kg',
    required: 2.5, onHand: 18, reserved: 4, available: 14, shortfall: 0, suggested: null,
    cheapest: { supplier: 'sup-freshfarm', supplierName: 'FreshFarm NG', unitPrice: 750, packLabel: '10 kg crate' } },
  { ing: 'ing-onion', name: 'Red onions', emoji: '🧅', unit: 'kg',
    required: 1.9, onHand: 9, reserved: 1.5, available: 7.5, shortfall: 0, suggested: null,
    cheapest: { supplier: 'sup-freshfarm', supplierName: 'FreshFarm NG', unitPrice: 380, packLabel: '10 kg sack' } },
  { ing: 'ing-beef', name: 'Beef (boneless)', emoji: '🥩', unit: 'kg',
    required: 2.1, onHand: 12, reserved: 2, available: 10, shortfall: 0, suggested: null,
    cheapest: { supplier: 'sup-freshfarm', supplierName: 'FreshFarm NG', unitPrice: 2900, packLabel: '10 kg box' } },
  { ing: 'ing-pepper', name: 'Scotch bonnet peppers', emoji: '🌶️', unit: 'kg',
    required: 0.6, onHand: 3, reserved: 0.5, available: 2.5, shortfall: 0, suggested: null,
    cheapest: { supplier: 'sup-freshfarm', supplierName: 'FreshFarm NG', unitPrice: 840, packLabel: '5 kg crate' } },
  { ing: 'ing-oats', name: 'Rolled oats', emoji: '🌾', unit: 'kg',
    required: 12, onHand: 40, reserved: 6, available: 34, shortfall: 0, suggested: null,
    cheapest: { supplier: 'sup-lagosgrains', supplierName: 'Lagos Grains Co', unitPrice: 1200, packLabel: '10 kg bag' } },
  { ing: 'ing-honey', name: 'Wildflower honey', emoji: '🍯', unit: 'L',
    required: 4.5, onHand: 10, reserved: 1, available: 9, shortfall: 0, suggested: null,
    cheapest: null, cheapestNote: 'No NGN supplier — the only catalog row is GBP (Albion Foods), excluded by the 053 currency guard.' },
  { ing: 'ing-coffee', name: 'Coffee beans (arabica)', emoji: '☕', unit: 'kg',
    required: 1, onHand: 2, reserved: 0.8, available: 1.2, shortfall: 0, suggested: null,
    cheapest: { supplier: 'sup-lagosgrains', supplierName: 'Lagos Grains Co', unitPrice: 8800, packLabel: '5 kg bag' } },
];
const cmPrepRows = (window) => CM_PREP_ROWS;

// Margin rows + the honest aggregate (known-COGS denominator).
const cmMarginRows = (window) => CM_MARGIN;

// ─── Window exports (Spec 058) ──────────────────────────────────────────────
window.CM_MAKE_PRODUCTS = CM_MAKE_PRODUCTS;
window.CM_INGREDIENTS = CM_INGREDIENTS;
window.CM_RECIPES = CM_RECIPES;
window.CM_SUPPLIERS = CM_SUPPLIERS;
window.CM_ALERTS = CM_ALERTS;
window.CM_POS = CM_POS;
window.CM_RECEIPTS = CM_RECEIPTS;
window.CM_PROD_SHEET = CM_PROD_SHEET;
window.CM_PROD_ORDERS = CM_PROD_ORDERS;
window.CM_MARGIN = CM_MARGIN;
window.CM_ALERT_STATUS = CM_ALERT_STATUS;
window.CM_PO_STATUS = CM_PO_STATUS;
window.CM_RUN_STATUS = CM_RUN_STATUS;
window.CM_MARGIN_STATUS = CM_MARGIN_STATUS;
window.CM_PREP_ROWS = CM_PREP_ROWS;
window.cmUnit = cmUnit;
window.cmIngAvail = cmIngAvail;
window.cmAllProducts = cmAllProducts;
window.cmPrepRows = cmPrepRows;
window.cmMarginRows = cmMarginRows;

/* ═══════════════════════════════════════════════════════════════════════════
   COMMERCE · STOREFRONT — Specs 066–072 (AbbysTable, the first storefront tenant)
   One coherent narrative: Abby's UK meal-box shop on a mock "today" of
   Tue 4 Aug 2026, 17:20 Europe/London — before this week's Tuesday 18:00 cutoff.
   Money is GBP via csMoney. Screens read these at render time.
   ═══════════════════════════════════════════════════════════════════════════ */

const csMoney = a => a == null ? '—' : (a < 0 ? '−£' + Math.abs(a).toFixed(2) : '£' + a.toFixed(2));
const csSigned = a => a === 0 ? 'included' : (a > 0 ? '+£' + a.toFixed(2) : '−£' + Math.abs(a).toFixed(2));

// ── Spec 066 — option groups (tenant catalogue) ────────────────────────────
// PUT /commerce/admin/option-groups/{id}/recommended-default reports affected
// products; that report is what flags Spec 067 content for review.
const CS_OPTION_GROUPS = [
  { id: 'og-portion', key: 'portion', label: 'Portion', mode: 'One', ccy: 'GBP', sort: 0, active: true, help: 'Plated size per dish',
    choices: [
      { key: 'regular', label: 'Regular', note: '330 g', price: 0,    dflt: true,  active: true },
      { key: 'large',   label: 'Large',   note: '450 g', price: 2.50, dflt: false, active: true },
    ] },
  { id: 'og-protein', key: 'protein', label: 'Protein', mode: 'One', ccy: 'GBP', sort: 1, active: true, help: 'Swap the centre of the dish',
    choices: [
      { key: 'chicken', label: 'Chicken',      note: null,        price: 0,    dflt: true,  active: true },
      { key: 'beef',    label: 'Beef',         note: null,        price: 0,    dflt: false, active: true },
      { key: 'salmon',  label: 'Suya salmon',  note: 'signature', price: 1.50, dflt: false, active: true },
      { key: 'tofu',    label: 'Tofu',         note: 'plant',     price: 0,    dflt: false, active: true },
      { key: 'goat',    label: 'Goat',         note: 'weekend',   price: 2.00, dflt: false, active: true },
    ] },
  { id: 'og-side', key: 'side', label: 'Side', mode: 'One', ccy: 'GBP', sort: 2, active: true, help: 'Served alongside',
    choices: [
      { key: 'plantain',  label: 'Fried plantain', note: null,  price: 0,     dflt: true,  active: true },
      { key: 'xplantain', label: 'Extra plantain', note: null,  price: 1.50,  dflt: false, active: true },
      { key: 'salad',     label: 'Garden salad',   note: null,  price: 0,     dflt: false, active: true },
      { key: 'noside',    label: 'No side',        note: null,  price: -2.00, dflt: false, active: true },
    ] },
  { id: 'og-heat', key: 'heat', label: 'Heat', mode: 'One', ccy: 'GBP', sort: 3, active: true, help: 'Scotch-bonnet scale',
    choices: [
      { key: 'none',   label: 'None',   note: null, price: 0, dflt: false, active: true },
      { key: 'mild',   label: 'Mild',   note: null, price: 0, dflt: false, active: true },
      { key: 'medium', label: 'Medium', note: null, price: 0, dflt: true,  active: true },
      { key: 'hot',    label: 'Hot',    note: 'honest heat', price: 0, dflt: false, active: true },
    ] },
];

// Yesterday's default move: Side default salad → plantain. The endpoint's
// RecommendedDefaultChangeResult listed these slugs; 067 flagged their blocks.
const CS_DEFAULT_MOVE = {
  group: 'side', from: 'salad', to: 'plantain', when: 'Yesterday 16:42', by: 'abby@abbystable.co.uk',
  affected: ['jollof-chicken', 'egusi-beef', 'suya-salmon', 'ofada-turkey', 'catfish-pepper-soup'],
};

// ── Dishes (products through the storefront lens) ──────────────────────────
// attrs mirror the storefront attribute contract {heatStep, protein, meal};
// groups = the product's 066 narrowing; surcharge = per-unit signature upgrade.
// content: state authored | withheld | review;  gaps = single-choice coverage gaps.
const CS_DISHES = [
  { slug: 'jollof-chicken', name: 'Jollof Rice & Chicken', emoji: '🍚', color: '#b3541e', status: 'active',
    attrs: { heatStep: 2, protein: 'Chicken', meal: 'Bowl' }, tags: ['bestseller', 'gluten-free'], keywords: ['party rice', 'smoky', 'firewood'],
    groups: ['portion', 'protein', 'side', 'heat'], surcharge: null,
    content: { state: 'authored', servingLabel: 'Per serving (regular, standard preparation)',
      fig: { kcal: 620, protein: 34, carbs: 71, fat: 22, fibre: 6, sugars: 8, salt: 1.8 },
      ingredients: 'Long-grain rice, chicken thigh, tomato, red pepper, scotch bonnet, onion, groundnut oil, thyme, bay leaf',
      allergens: 'None declared for the standard preparation',
      heating: [{ m: 'Oven', b: '180°C fan for 18–20 min, covered' }, { m: 'Microwave', b: '900 W for 4 min, rest 1 min' }],
      variants: [{ sel: 'protein: salmon', note: 'declares Fish' }, { sel: 'portion: large', note: 'figures re-authored' }],
      gaps: [{ group: 'protein', choice: 'goat' }] } },
  { slug: 'egusi-beef', name: 'Egusi & Beef', emoji: '🥘', color: '#7a6a1e', status: 'active',
    attrs: { heatStep: 2, protein: 'Beef', meal: 'Stew' }, tags: ['high-fibre'], keywords: ['melon seed', 'soup', 'swallow'],
    groups: ['portion', 'protein', 'side', 'heat'], surcharge: null,
    content: { state: 'review', reviewReason: 'Variant references retired choice "okra side"',
      servingLabel: 'Per serving (regular, standard preparation)',
      fig: { kcal: 710, protein: 41, carbs: 38, fat: 44, fibre: 9, sugars: 5, salt: 2.1 },
      ingredients: 'Ground egusi (melon seed), beef shin, spinach, palm oil, crayfish, scotch bonnet, onion, locust bean',
      allergens: 'Crustaceans (crayfish)',
      heating: [{ m: 'Hob', b: 'Gentle simmer 8–10 min, stir through' }],
      variants: [{ sel: 'side: okra (retired)', note: 'needs re-author', stale: true }],
      gaps: [] } },
  { slug: 'suya-salmon', name: 'Suya-Spiced Salmon Bowl', emoji: '🐟', color: '#a34226', status: 'active',
    attrs: { heatStep: 3, protein: 'Fish', meal: 'Bowl' }, tags: ['signature', 'dairy-free'], keywords: ['yaji', 'grilled', 'spice crust'],
    groups: ['portion', 'side', 'heat'], surcharge: 2.00,
    content: { state: 'review', reviewReason: 'Side default moved salad → plantain (yesterday)',
      servingLabel: 'Per serving (regular, standard preparation)',
      fig: { kcal: 540, protein: 38, carbs: 42, fat: 24, fibre: 5, sugars: 6, salt: 1.6 },
      ingredients: 'Salmon fillet, yaji (groundnut, ginger, chilli), brown rice, plantain, lime',
      allergens: 'Fish · Peanuts (yaji crust)',
      heating: [{ m: 'Oven', b: '170°C fan for 12 min — do not microwave the crust' }],
      variants: [],
      gaps: [{ group: 'heat', choice: 'none' }] } },
  { slug: 'moi-moi-garden', name: 'Moi Moi Garden Plate', emoji: '🫘', color: '#2e6b4f', status: 'active',
    attrs: { heatStep: 0, protein: 'Plant-based', meal: 'Bowl' }, tags: ['vegan', 'gluten-free'], keywords: ['bean pudding', 'steamed'],
    groups: ['portion', 'side', 'heat'], surcharge: null,
    content: { state: 'authored', servingLabel: 'Per serving (regular)',
      fig: { kcal: 430, protein: 21, carbs: 52, fat: 14, fibre: 12, sugars: 4, salt: 1.1 },
      ingredients: 'Black-eyed beans, red pepper, onion, vegetable oil, ginger, greens of the week',
      allergens: 'None declared',
      heating: [{ m: 'Steam', b: 'Re-steam 10 min or microwave 3 min covered' }],
      variants: [],
      gaps: [{ group: 'portion', choice: 'large' }] } },
  { slug: 'ofada-turkey', name: 'Ofada Rice & Turkey', emoji: '🍛', color: '#4f5d2e', status: 'active',
    attrs: { heatStep: 3, protein: 'Turkey', meal: 'Bowl' }, tags: ['bold'], keywords: ['ayamase sauce', 'local rice'],
    groups: ['portion', 'protein', 'side', 'heat'], surcharge: null,
    content: { state: 'withheld', servingLabel: 'Per serving (regular, standard preparation)',
      fig: { kcal: 680, protein: 36, carbs: 74, fat: 26, fibre: 7, sugars: 5, salt: 2.3 },
      ingredients: null, allergens: null,
      heating: [],
      variants: [],
      gaps: [] } },
  { slug: 'catfish-pepper-soup', name: 'Catfish Pepper Soup', emoji: '🍲', color: '#8a3a2a', status: 'active',
    attrs: { heatStep: 3, protein: 'Fish', meal: 'Soup' }, tags: ['dairy-free', 'low-carb'], keywords: ['point and kill', 'uziza', 'broth'],
    groups: ['portion', 'heat'], surcharge: null,
    content: { state: 'authored', servingLabel: 'Per serving (regular)',
      fig: { kcal: 310, protein: 33, carbs: 9, fat: 15, fibre: 2, sugars: 2, salt: 2.0 },
      ingredients: 'Catfish, uziza leaf, calabash nutmeg, scotch bonnet, stock',
      allergens: 'Fish',
      heating: [{ m: 'Hob', b: 'Bring to a gentle simmer — never boil hard' }],
      variants: [],
      gaps: [] } },
  { slug: 'garden-jollof', name: 'Garden Jollof', emoji: '🥗', color: '#3f7a3a', status: 'active',
    attrs: { heatStep: 1, protein: 'Plant-based', meal: 'Bowl' }, tags: ['vegan'], keywords: ['veg', 'mild'],
    groups: ['portion', 'side', 'heat'], surcharge: null,
    content: { state: 'authored', servingLabel: 'Per serving (regular)',
      fig: { kcal: 480, protein: 12, carbs: 78, fat: 12, fibre: 8, sugars: 9, salt: 1.4 },
      ingredients: 'Long-grain rice, tomato, red pepper, carrot, green beans, onion, vegetable oil',
      allergens: 'None declared',
      heating: [{ m: 'Microwave', b: '900 W for 3½ min, stir halfway' }],
      variants: [],
      gaps: [] } },
  { slug: 'ayamase-designer', name: 'Ayamase Designer Stew', emoji: '🫑', color: '#5a6b2a', status: 'draft',
    attrs: { heatStep: 3, protein: 'Beef', meal: 'Stew' }, tags: [], keywords: ['ofada sauce', 'green pepper'],
    groups: [], surcharge: null,
    content: { state: 'withheld', servingLabel: null, fig: null, ingredients: null, allergens: null, heating: [], variants: [], gaps: [] } },
];
const csDish = slug => CS_DISHES.find(d => d.slug === slug) || null;
const csGroup = key => CS_OPTION_GROUPS.find(g => g.key === key) || null;

// ── Spec 068 — the box size plan (PUT /products/{id}/size-plan) ────────────
// Presets WIN at their size; every other size prices base + (size − 6) × perSpace.
// Growing a box always charges boxPrice(target) − boxPrice(current).
const CS_PLAN = {
  bundleSlug: 'abbys-box', bundleName: "Abby's Box", ccy: 'GBP',
  min: 6, max: 30, baseSize: 6, basePrice: 95, perSpace: 15,
  presets: [
    { size: 6,  price: 95,  badge: 'Starter',      blurb: 'The classic week',      saving: null, sort: 0 },
    { size: 8,  price: 120, badge: 'Most popular', blurb: 'Two extra dinners',     saving: 5,    sort: 1 },
    { size: 12, price: 170, badge: 'Best value',   blurb: 'Feeds the whole table', saving: 15,   sort: 2 },
  ] };
const csBoxPrice = size => {
  const p = CS_PLAN.presets.find(x => x.size === size);
  return p ? p.price : CS_PLAN.basePrice + (size - CS_PLAN.baseSize) * CS_PLAN.perSpace;
};

// ── Spec 069 — fulfilment calendar + the promise it computes ───────────────
const CS_CALENDAR = {
  timezone: 'Europe/London', deliveryDays: ['Thursday'], cutoffLocal: '18:00', cutoffDayOfWeek: 'Tuesday',
  leadDays: 2, blackoutDates: ['2026-08-27'], active: true,
  promise: { date: '2026-08-06', label: 'Thursday 6 August' },
  // August 2026 for the month grid: the 1st is a Saturday.
  monthLabel: 'August 2026', firstDow: 6, days: 31,
};

// ── Spec 070/071 — collections, facet groups, storefront config ────────────
const CS_COLLECTIONS = [
  { id: 'col-feat', slug: 'featured', title: 'A taste of the table', subtitle: 'The homepage rail', kind: 'curated', active: true,
    items: [
      { slug: 'jollof-chicken', rank: 1 }, { slug: 'suya-salmon', rank: 2 }, { slug: 'egusi-beef', rank: 3 },
      { slug: 'moi-moi-garden', rank: 4 }, { slug: 'catfish-pepper-soup', rank: 5 }, { slug: 'ofada-turkey', rank: 6 },
      { slug: 'ayamase-designer', rank: 7 }, // draft — staged invisibly, surfaces on activation
    ] },
  { id: 'col-carb', slug: 'carb-conscious', title: 'Carb-conscious', subtitle: 'Homepage category rail', kind: 'curated', active: true,
    items: [{ slug: 'catfish-pepper-soup', rank: 1 }, { slug: 'moi-moi-garden', rank: 2 }, { slug: 'suya-salmon', rank: 3 }] },
  { id: 'col-protein', slug: 'protein-led', title: 'Protein-led', subtitle: 'Homepage category rail', kind: 'curated', active: true,
    items: [{ slug: 'egusi-beef', rank: 1 }, { slug: 'suya-salmon', rank: 2 }, { slug: 'jollof-chicken', rank: 3 }, { slug: 'ofada-turkey', rank: 4 }] },
  { id: 'col-plant', slug: 'plant-led', title: 'Plant-led', subtitle: 'Homepage category rail', kind: 'curated', active: true,
    items: [{ slug: 'moi-moi-garden', rank: 1 }, { slug: 'garden-jollof', rank: 2 }] },
  { id: 'col-extras', slug: 'extras', title: 'Extras', subtitle: 'Add-ons alongside the box (Spec 071)', kind: 'curated', active: true,
    items: [
      { slug: 'puff-puff', rank: 1, name: 'Puff Puff (6)', price: 4.50, emoji: '🍩' },
      { slug: 'suya-skewers', rank: 2, name: 'Beef Suya Skewers (2)', price: 6.50, emoji: '🍢' },
      { slug: 'chin-chin', rank: 3, name: 'Chin Chin Tub', price: 3.50, emoji: '🍪' },
      { slug: 'zobo', rank: 4, name: 'Zobo (500 ml)', price: 3.00, emoji: '🧃' },
      { slug: 'plantain-chips', rank: 5, name: 'Plantain Chips', price: 3.00, emoji: '🍌' },
      { slug: 'pepper-sauce', rank: 6, name: "Abby's Pepper Sauce", price: 2.50, emoji: '🌶️' },
    ], skipped: 1, skippedNote: 'Honey Cake has no GBP retail price — omitted and counted, never silently dropped' },
];
const CS_FACETS = [
  { id: 'fg-protein', key: 'protein', label: 'Protein', match: 'Tag', source: null, sort: 0, active: true,
    options: [{ v: 'chicken', l: 'Chicken' }, { v: 'beef', l: 'Beef' }, { v: 'fish', l: 'Fish' }, { v: 'turkey', l: 'Turkey' }, { v: 'plant-based', l: 'Plant-based' }] },
  { id: 'fg-wellness', key: 'wellness', label: 'Wellness goal', match: 'Tag', source: null, sort: 1, active: true,
    options: [{ v: 'carb-conscious', l: 'Carb-conscious' }, { v: 'protein-led', l: 'Protein-led' }, { v: 'plant-led', l: 'Plant-led' }, { v: 'dash', l: 'DASH' }] },
  { id: 'fg-meal', key: 'meal', label: 'Meal type', match: 'Attribute', source: 'attributes.meal', sort: 2, active: true,
    options: [{ v: 'bowl', l: 'Bowl' }, { v: 'soup', l: 'Soup' }, { v: 'stew', l: 'Stew' }, { v: 'salad', l: 'Salad' }] },
  { id: 'fg-dietary', key: 'dietary', label: 'Dietary', match: 'Tag', source: null, sort: 3, active: true,
    options: [{ v: 'gluten-free', l: 'Gluten-free' }, { v: 'dairy-free', l: 'Dairy-free' }, { v: 'vegan', l: 'Vegan' }, { v: 'high-fibre', l: 'High-fibre' }] },
  { id: 'fg-heat', key: 'heat', label: 'Heat', match: 'Range', source: 'attributes.heatStep', sort: 4, active: true,
    options: [{ v: 'mild', l: 'Mild', min: 0, max: 2 }, { v: 'medium', l: 'Medium', min: 2, max: 3 }, { v: 'hot', l: 'Hot', min: 3, max: 4 }] },
];
const CS_CONFIG = {
  currency: 'GBP', recommendedChoiceLabel: "Abby's choice", resultsPageSize: 24,
  backToTop: '{"type":"cardIndex","value":10}',
  delivery: { list: 10, charged: 0 },
  defaultBoxSlug: 'abbys-box', extrasCollectionSlug: 'extras',
};

// ── Spec 072 — storefront customers (the unified Customers view's commerce lens)
const CS_CUSTOMERS = [
  { id: 'party_f3a1', name: 'Femi Adesanya', email: 'femi.a@gmail.com', type: 'Person', domains: ['Storefront'],
    orders: 4, value: 505, last: 'Today 09:14', since: 'Mar 2026',
    adoption: { built: 'Built a 6-box as guest · Tue 21:14', signed: 'Registered · Wed 08:40', adopted: 'Cart adopted — guest token retired' },
    cart: null },
  { id: 'party_a2c8', name: 'Adaeze Nwosu', email: 'adaeze@nwosu.co', type: 'Person', domains: ['Storefront', 'Payabo'],
    orders: 7, value: 812, last: 'Yesterday', since: 'Jan 2026', adoption: null, cart: null },
  { id: 'party_t9b2', name: 'Tunde Bello', email: 'tunde.bello@outlook.com', type: 'Person', domains: ['Storefront'],
    orders: 1, value: 95, last: '2 weeks ago', since: 'Jul 2026', adoption: null, cart: null },
  { id: 'party_h5e7', name: 'Halima Yusuf', email: 'halima.y@yahoo.com', type: 'Person', domains: ['Storefront'],
    orders: 2, value: 260, last: '5 days ago', since: 'May 2026', adoption: null,
    cart: { id: 'cart_ab12', size: 8, filled: 5, extras: 2 } },
  { id: 'party_prim', name: 'Primrose Logistics', email: 'accounts@primrose.ng', type: 'Business', domains: ['Billing'],
    orders: 12, value: 4180, last: 'Today 07:00', since: 'Nov 2025', adoption: null, cart: null },
];

Object.assign(window, {
  csMoney, csSigned, csDish, csGroup, csBoxPrice,
  CS_OPTION_GROUPS, CS_DEFAULT_MOVE, CS_DISHES, CS_PLAN, CS_CALENDAR,
  CS_COLLECTIONS, CS_FACETS, CS_CONFIG, CS_CUSTOMERS,
});
