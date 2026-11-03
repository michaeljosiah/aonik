// Fictional, fixed fixtures for design and marketing. No customer or production data.
export const businesses = {
  base: {
    name: 'General business', tenant: 'Northline', initials: 'N', profile: 'general',
    modules: ['finance', 'ai', 'agents', 'documents'],
    scene: 'home', subtitle: 'A clear view of your business. Every order, every decision, every day.',
    description: 'General profile · Finance, AI, Agents and Documents enabled for this demo.',
    metrics: [['Order value · GBP', '£128,450', '+12.8%', 'Across 842 orders this month'], ['Paid invoices · GBP', '£96,240', '+8.2%', '64 invoices this month'], ['Active customers', '286', '+18', '24 new customers this month'], ['Awaiting approval', '7', '', '3 high-risk proposals need review']],
    chart: { title: 'Order value', value: '£128,450', note: 'GBP only · October 2026', series: [24,32,28,43,39,48,44,57,51,65,58,74,64,79,72,87,76,93,82,98,87,108,96,116,107,123,113,134,122,146], labels: ['01 Oct','06 Oct','11 Oct','16 Oct','21 Oct','26 Oct','31 Oct'] },
    table: { title: 'Recent orders', note: 'Business intent linked to payment execution and ledger evidence.', columns: ['Order','Customer','Type','Amount','Status'], rows: [
      ['ORD-1086','Cedar Studio','Invoice settlement','£4,800.00','Completed'], ['ORD-1085','Atlas Works','Bill payment','£320.00','Processing'], ['ORD-1084','Fern & Co','Invoice settlement','£2,450.00','Completed'], ['ORD-1083','Harbour Labs','Bill payment','£186.50','Awaiting approval'], ['ORD-1082','Oak Collective','Invoice settlement','£8,120.00','Completed'], ['ORD-1081','Rowan Digital','Bill payment','£740.00','Completed']
    ] },
    attention: [['Approval queue','3 money-movement proposals require human approval.','warning'], ['Collections','4 invoices are due within the next 7 days.','info'], ['Ledger evidence','Completed demo orders link to balanced journal entries.','success']],
    agent: { name: 'Finance Agent', title: 'Your next move, prepared.', text: 'I found 3 orders ready for review. Supporting invoice references and policy checks are attached to each proposal.', run: 'RUN-DEMO-1042', action: 'Review proposals' }
  },
  simi: {
    name: 'Personal Finance', tenant: 'Personal Finance', initials: 'P', profile: 'general',
    modules: ['personal-finance','groups','documents','ai','agents','voice'], scene: 'ai.usage',
    description: 'Installed simi pack · General admin profile · Personal Finance is the display name.',
    subtitle: 'Understand how your financial companion is being used.',
    metrics: [['AI runs this month','12,840','+16.4%','Across 486 active customers'], ['Active customers','486','+42','Monthly active customers'], ['Tokens processed','4.82M','+9.6%','Input and output tokens'], ['Estimated AI cost · GBP','£84.60','','Fictional usage cost for October']],
    chart: { title: 'Companion activity', value: '12,840 runs', note: 'Daily AI runs · October 2026', series: [36,41,38,48,45,52,46,58,54,63,58,67,60,73,68,80,72,86,78,91,82,95,87,102,93,108,99,114,104,121], labels: ['01 Oct','06 Oct','11 Oct','16 Oct','21 Oct','26 Oct','31 Oct'] },
    table: { title: 'Recent AI runs', note: 'Reference-based activity. Personal financial details stay out of this view.', columns: ['AI run','Customer reference','Task','Tokens','Status'], rows: [
      ['RUN-DEMO-2048','CUS-00486','Spending summary','1,240','Completed'], ['RUN-DEMO-2047','CUS-00129','Bill explanation','860','Completed'], ['RUN-DEMO-2046','CUS-00314','Budget overview','1,680','Completed'], ['RUN-DEMO-2045','CUS-00202','Goal progress','920','Completed'], ['RUN-DEMO-2044','CUS-00083','Statement summary','2,140','Completed'], ['RUN-DEMO-2043','CUS-00271','Subscription review','1,120','Completed']
    ] },
    attention: [['Routing policy','All demo runs resolve through an AI route policy.','info'], ['Audit coverage','Every run carries an AI run reference.','success'], ['Customer privacy','Usage is shown by reference, without raw financial details.','info']],
    agent: { name: 'Personal Finance Agent', title: 'Clarity for everyday money.', text: 'Spending summaries, bill explanations and goal check-ins are the most-used companion tasks this month.', run: 'RUN-DEMO-2048', action: 'View run details' }
  },
  'food-commerce': {
    name: 'Food Commerce', tenant: 'The Sunday Kitchen', initials: 'S', profile: 'food-commerce',
    modules: ['commerce','ai','agents'], scene: 'commerce.orders',
    description: 'Installed food-commerce pack · Finance and Ordering are enabled through Commerce dependencies.',
    subtitle: 'The details behind every box. From the first selection to payment.',
    metrics: [['Orders this month','248','+18.1%','Single-currency demo window'], ['Order value · GBP','£14,880','+21.6%','248 orders · mean £60.00'], ['Awaiting payment','12','','Excludes cancelled orders'], ['Open carts','18','','6 contain all box selections']],
    chart: { title: 'Food order value', value: '£14,880', note: 'GBP only · October 2026', series: [17,22,28,21,34,40,37,29,42,48,41,53,46,60,55,49,64,71,61,76,69,82,73,88,80,94,85,102,92,112], labels: ['01 Oct','06 Oct','11 Oct','16 Oct','21 Oct','26 Oct','31 Oct'] },
    table: { title: 'Food Orders', note: 'Box selections are preserved inside the order item. Fulfilment is untracked.', columns: ['Order','Customer','Box / selections','Total','Payment'], rows: [
      ['FO-2048','Alex Morgan','Family box · 6 selections','£72.00','Paid'], ['FO-2047','Jamie Taylor','Classic box · 4 selections','£48.00','Awaiting payment'], ['FO-2046','Sam Parker','Family box · 6 selections','£72.00','Paid'], ['FO-2045','Casey Jordan','Classic box · 4 selections','£48.00','Paid'], ['FO-2044','Robin Ellis','Family box · 6 selections','£72.00','Paid'], ['FO-2043','Drew Quinn','Classic box · 4 selections','£48.00','Cancelled']
    ] },
    attention: [['Payment follow-up','12 non-terminal orders are awaiting payment.','warning'], ['Menu content','2 menu items have content awaiting review.','info'], ['Cart window','18 open carts in the demo window; 2 have selection drift.','warning']],
    agent: { name: 'Kitchen Agent', title: 'A little less admin. More kitchen.', text: 'Two menu descriptions are ready for review. I have prepared suggested edits with the original item references.', run: 'RUN-DEMO-3042', action: 'Review suggestions' }
  },
  'arke-kids': {
    name: 'Arke Kids', tenant: 'Arke Kids', initials: 'A', profile: 'arke-kids',
    modules: ['ai','agents'], scene: 'customers',
    description: 'Existing admin profile · AI and Agents assumed for the demo · No provisioning pack is installed.',
    subtitle: 'One customer registry. A focused workspace for your team.',
    metrics: [['Registered customers','328','+24','Fictional organisation accounts'], ['Active this month','286','+12.2%','Customer activity in October'], ['AI runs this month','8,460','+14.8%','Audited through AI run records'], ['Awaiting approval','3','','Human decisions required']],
    chart: { title: 'Customer activity', value: '286 active', note: 'Daily active customer references · October 2026', series: [45,49,47,56,52,62,58,68,62,76,70,79,75,86,80,92,87,99,91,106,98,113,104,119,111,127,118,134,125,142], labels: ['01 Oct','06 Oct','11 Oct','16 Oct','21 Oct','26 Oct','31 Oct'] },
    table: { title: 'Customers', note: 'Fictional adult account contacts; no child records or personal learning data.', columns: ['Customer','Reference','Type','Joined','Status'], rows: [
      ['Alex Morgan','CUS-00328','Person','28 Oct 2026','Active'], ['Jamie Taylor','CUS-00327','Person','27 Oct 2026','Active'], ['Cedar Learning','CUS-00326','Business','26 Oct 2026','Active'], ['Casey Jordan','CUS-00325','Person','25 Oct 2026','Active'], ['Oak Education','CUS-00324','Business','24 Oct 2026','Active'], ['Robin Ellis','CUS-00323','Person','23 Oct 2026','Active']
    ] },
    attention: [['Approval queue','3 proposals are waiting for an operator decision.','warning'], ['Usage visibility','8,460 runs are recorded with token usage.','info'], ['Focused navigation','Only customer, AI and administration screens are included.','success']],
    agent: { name: 'Operations Agent', title: 'The right context for every decision.', text: 'Your customer registry and AI activity are ready to review. Three proposals need a human decision before execution.', run: 'RUN-DEMO-4042', action: 'Review proposals' }
  },
  remittance: {
    name: 'Remittance', tenant: 'Meridian Remit', initials: 'M', profile: 'remittance',
    modules: ['finance','ai','agents','documents'], scene: 'finance.orders',
    description: 'Installed Remittance pack · Finance, AI, Agents and Documents, plus core dependencies.',
    subtitle: 'Move with confidence. Every transfer starts with an order.',
    metrics: [['Funded order value · GBP','£284,650','+14.2%','Source GBP only · October'], ['Transfer orders','1,248','+11.8%','Across 4 demo corridors'], ['Completed orders','1,196','','95.8% of this demo window'], ['Awaiting approval','8','','High-risk proposals require review']],
    chart: { title: 'Funded order value', value: '£284,650', note: 'Source GBP only · illustrative amounts', series: [38,46,42,58,51,65,59,74,67,82,75,91,84,101,93,110,100,121,111,132,119,140,127,152,138,163,149,175,160,188], labels: ['01 Oct','06 Oct','11 Oct','16 Oct','21 Oct','26 Oct','31 Oct'] },
    table: { title: 'Transfer orders', note: 'Orders capture intent. Payment and payout references track execution.', columns: ['Order','Customer reference','Corridor','Source amount','Status'], rows: [
      ['RMT-8048','CUS-00281','GBP → NGN','£1,250.00','Completed'], ['RMT-8047','CUS-00196','GBP → GHS','£480.00','Processing'], ['RMT-8046','CUS-00314','GBP → KES','£720.00','Awaiting approval'], ['RMT-8045','CUS-00092','GBP → EUR','£2,400.00','Completed'], ['RMT-8044','CUS-00204','GBP → NGN','£850.00','Completed'], ['RMT-8043','CUS-00178','GBP → GHS','£320.00','Processing']
    ] },
    attention: [['Human approval','8 transfer proposals require explicit approval before execution.','warning'], ['Partner references','Payment, payout and transmission references are linked per order.','info'], ['Ledger evidence','Completed orders link to immutable double-entry evidence.','success']],
    agent: { name: 'Finance Agent', title: 'Prepared by agents. Approved by people.', text: 'Three transfer proposals are ready for review. No money moves from this workspace without approval and durable execution.', run: 'RUN-DEMO-5042', action: 'Review proposals' }
  }
};

export const detailTables = {
  'finance.invoices': { title: 'Invoices', note: 'Fictional GBP invoices. Business intent and payment execution remain separate.', columns: ['Invoice','Customer','Due date','Amount','Status'], rows: [ ['INV-2048','Cedar Studio','06 Nov 2026','£4,800.00','Issued'], ['INV-2047','Atlas Works','04 Nov 2026','£1,920.00','Paid'], ['INV-2046','Fern & Co','03 Nov 2026','£2,450.00','Issued'], ['INV-2045','Harbour Labs','30 Oct 2026','£3,600.00','Overdue'], ['INV-2044','Oak Collective','28 Oct 2026','£8,120.00','Paid'], ['INV-2043','Rowan Digital','27 Oct 2026','£740.00','Draft'] ] },
  'commerce.products': { title: 'Menu', note: 'Storefront menu items and publication state. No unsupported retail-price column.', columns: ['Menu item','Reference','Category','Content','Status'], rows: [ ['Smoky jollof rice','DISH-001','Rice & grains','Reviewed','Published'], ['Suya chicken','DISH-002','Mains','Reviewed','Published'], ['Roasted plantain','DISH-003','Sides','Review required','Draft'], ['Coconut rice','DISH-004','Rice & grains','Reviewed','Published'], ['Garden vegetable stew','DISH-005','Mains','Review required','Draft'], ['Ginger & lime drink','DISH-006','Drinks','Reviewed','Published'] ] },
  'finance.countries': { title: 'Corridors', note: 'Illustrative coverage configuration. No live exchange rates or service claims.', columns: ['Corridor','Source','Destination','Currency pair','Status'], rows: [ ['United Kingdom → Nigeria','United Kingdom','Nigeria','GBP / NGN','Active'], ['United Kingdom → Ghana','United Kingdom','Ghana','GBP / GHS','Active'], ['United Kingdom → Kenya','United Kingdom','Kenya','GBP / KES','Active'], ['United Kingdom → France','United Kingdom','France','GBP / EUR','Active'] ] }
};
