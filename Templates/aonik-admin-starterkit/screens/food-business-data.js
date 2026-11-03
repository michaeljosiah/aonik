// Simplified Food Business design inventory. Fictional GBP workspace; no live services.
const FOOD_AREAS = [
  {id:'today',label:'Overview',icon:'overview',scene:'today'},
  {id:'customers',label:'Customers',icon:'customers',scene:'customers'},
  {id:'orders',label:'Orders',icon:'food-orders',scene:'orders'},
  {id:'kitchen',label:'Kitchen',icon:'kitchen',scene:'plan'},
  {id:'stock',label:'Stock & purchasing',icon:'inventory',scene:'ingredients'},
  {id:'finance',label:'Finance',icon:'billing',scene:'invoices'},
  {id:'shop',label:'Shop',icon:'storefront-config',scene:'products'},
  {id:'reports',label:'Reports',icon:'margin',scene:'sales'},
];
const FOOD_SCENES = [
  {id:'today',legacy:'commerce-overview',area:'today',title:'Business overview',subtitle:'Customers, sales and operations in one connected workspace.',action:'Open kitchen plan',icon:'overview'},
  {id:'orders',legacy:'commerce-orders',area:'orders',title:'Customer orders',subtitle:'See what is paid, what needs making and what is ready to leave.',action:'Open packing',icon:'food-orders'},
  {id:'packing',legacy:'food-packing',area:'orders',title:'Pack today’s orders',subtitle:'Check the contents, then mark an eligible order ready for dispatch.',action:'Review dispatch',icon:'packing'},
  {id:'carts',legacy:'commerce-carts',area:'orders',title:'Open baskets',subtitle:'Useful context for customer follow-up. Baskets are not kitchen demand.',action:'Review basket',icon:'carts'},
  {id:'plan',legacy:'cm-prod-sheet',area:'kitchen',title:'Kitchen plan',subtitle:'A short plan from confirmed customer demand for today’s service.',action:'Create batch',icon:'planning'},
  {id:'prep',legacy:'cm-prep-list',area:'kitchen',title:'Ingredients for this batch',subtitle:'Compare the recipe quantities with stock that is actually available.',action:'Order shortfall',icon:'ingredients'},
  {id:'batches',legacy:'cm-prod-orders',area:'kitchen',title:'Kitchen batches',subtitle:'Move each batch forward with a clear view of its stock impact.',action:'Review batch',icon:'production'},
  {id:'kitchen-sheet',legacy:'cm-kitchen-sheet',area:'kitchen',title:'Kitchen sheet',subtitle:'The saved quantities for batch KT-1103-A. Ready to share with the team.',action:'Print sheet',icon:'recipes'},
  {id:'recipes',legacy:'cm-recipes',area:'kitchen',title:'Recipes',subtitle:'One recipe per dish variant, with a clear yield and ingredient cost.',action:'Add recipe',icon:'recipes'},
  {id:'ingredients',legacy:'cm-ingredients',area:'stock',title:'Ingredients',subtitle:'Stock, reorder levels and current cost in one simple list.',action:'Add ingredient',icon:'ingredients'},
  {id:'inventory',legacy:'commerce-inventory',area:'stock',title:'Finished dishes',subtitle:'On hand, reserved and available stock at your selected location.',action:'Adjust stock',icon:'inventory'},
  {id:'alerts',legacy:'cm-lowstock',area:'stock',title:'Stock needing attention',subtitle:'Review the shortfall, its owner and any purchase order already raised.',action:'Review shortfall',icon:'low-stock'},
  {id:'purchase-orders',legacy:'cm-pos',area:'stock',title:'Purchase orders',subtitle:'Keep ordering and receiving together, with progress beside each order.',action:'New purchase order',icon:'purchase-orders'},
  {id:'receiving',legacy:'cm-receipt',area:'stock',title:'Receive a delivery',subtitle:'Check what arrived against PO-2038 before adding it to stock.',action:'Review receipt',icon:'delivery'},
  {id:'suppliers',legacy:'cm-suppliers',area:'stock',title:'Suppliers',subtitle:'Compare pack sizes and prices in the same currency.',action:'Add supplier',icon:'suppliers'},
  {id:'products',legacy:'commerce-products',area:'shop',title:'Your menu',subtitle:'Make each dish easy to find, choose and order.',action:'Add dish',icon:'products'},
  {id:'categories',legacy:'commerce-categories',area:'shop',title:'Menu categories',subtitle:'A small, clear structure for the dishes your customers browse.',action:'Add category',icon:'categories'},
  {id:'options',legacy:'cs-options',area:'shop',title:'Customer choices',subtitle:'Keep the recommended choice and any price difference easy to understand.',action:'Review default',icon:'personalisation'},
  {id:'content',legacy:'cs-content',area:'shop',title:'Food labels',subtitle:'Review the exact preparation before publishing ingredients and allergens.',action:'Review label',icon:'content'},
  {id:'box-plans',legacy:'cs-boxplan',area:'shop',title:'Meal box sizes',subtitle:'Set the box price once, then preview exactly what a customer will pay.',action:'Edit prices',icon:'box-plans'},
  {id:'merchandising',legacy:'cs-merch',area:'shop',title:'Shop homepage',subtitle:'A few useful collections, in the order customers see them.',action:'Review changes',icon:'merchandising'},
  {id:'discounts',legacy:'commerce-discounts',area:'shop',title:'Discounts',subtitle:'Simple offers with clear limits, dates and usage.',action:'Create discount',icon:'discounts'},
  {id:'delivery',legacy:'cs-delivery',area:'shop',title:'Delivery schedule',subtitle:'Show the delivery date only when a schedule can support the promise.',action:'Review schedule',icon:'delivery'},
  {id:'settings',legacy:'cs-config',area:'shop',title:'Shop settings',subtitle:'The small details customers see throughout the shop.',action:'Review changes',icon:'settings'},
  {id:'margin',legacy:'cm-margin',area:'reports',title:'Dish margins',subtitle:'Review costed sales and keep missing costs out of the margin calculation.',action:'Review missing cost',icon:'margin'},
  {id:'customers',legacy:'food-customers',area:'customers',title:'Customers',subtitle:'Customer details, orders and account history, together.',action:'Find customer',icon:'customers'},
  {id:'customer',legacy:'food-customer',area:'customers',title:'Customer account',subtitle:'Follow the relationship from contact details to orders and invoices.',action:'View customer order',icon:'customers'},
  {id:'order-detail',legacy:'food-order-detail',area:'orders',title:'Order details',subtitle:'Business intent, payment and fulfilment, linked with clear responsibilities.',action:'View invoice',icon:'food-orders'},
  {id:'invoices',legacy:'food-invoices',area:'finance',title:'Invoices',subtitle:'Issued invoices, outstanding balances and their linked orders.',action:'Review outstanding',icon:'invoices'},
  {id:'invoice',legacy:'food-invoice',area:'finance',title:'Invoice details',subtitle:'The customer, order, payment allocation and accounting references.',action:'View customer',icon:'invoices'},
  {id:'payments',legacy:'food-payments',area:'finance',title:'Payments',subtitle:'Successful execution and allocation, separate from order fulfilment.',action:'Review payment',icon:'transactions'},
  {id:'payment',legacy:'food-payment',area:'finance',title:'Payment details',subtitle:'Trace a payment to its customer, invoice and ledger evidence.',action:'View invoice',icon:'transactions'},
  {id:'credit-notes',legacy:'food-credit-notes',area:'finance',title:'Credit notes',subtitle:'Review adjustments without confusing a credit with a refund.',action:'Review draft',icon:'invoices'},
  {id:'accounting',legacy:'food-accounting',area:'finance',title:'Accounting',subtitle:'Read-only financial evidence for authorized finance users.',action:'View invoice',icon:'ledger'},
  {id:'sales',legacy:'food-sales',area:'reports',title:'Sales report',subtitle:'Understand ordered sales, received payments and outstanding balances.',action:'Export report',icon:'margin'},
  {id:'customer-orders',tabTitle:'Customer · Orders',legacy:'food-customer-orders',area:'customers',title:'Customer account',subtitle:'Orders and saved choices for the selected customer.',action:'View customer order',icon:'food-orders'},
  {id:'customer-commerce',tabTitle:'Customer · Commerce',legacy:'food-customer-commerce',area:'customers',title:'Customer account',subtitle:'Storefront profile, recorded shop identity and food order history.',action:'View customer order',icon:'products'},
  {id:'customer-finance',tabTitle:'Customer · Finance',legacy:'food-customer-finance',area:'customers',title:'Customer account',subtitle:'Customer invoices, successful payments and credit notes.',action:'View customer order',icon:'invoices'},
  {id:'customer-insights',tabTitle:'Customer · Insights',legacy:'food-customer-insights',area:'customers',title:'Customer account',subtitle:'Customer-specific summaries with supporting record references.',action:'View customer order',icon:'agents'},
  {id:'customer-documents',tabTitle:'Customer · Documents',legacy:'food-customer-documents',area:'customers',title:'Customer account',subtitle:'Customer files with a clear purpose and access scope.',action:'View customer order',icon:'content'},
  {id:'customer-activity',tabTitle:'Customer · Activity',legacy:'food-customer-activity',area:'customers',title:'Customer account',subtitle:'A chronological account of customer, order and financial events.',action:'View customer order',icon:'audit'},
];
const FOOD_DISHES = [
  {id:'jollof',name:'Jollof rice & chicken',variant:'Classic · chicken',category:'Rice dishes',price:9.5,cost:3.4,sold:40,recipe:true,label:'Published',initials:'JR'},
  {id:'coconut',name:'Coconut rice & beef',variant:'Classic · beef',category:'Rice dishes',price:10,cost:3.8,sold:24,recipe:true,label:'Published',initials:'CR'},
  {id:'egusi',name:'Egusi soup',variant:'Standard · with fish',category:'Soups',price:11,cost:4.4,sold:18,recipe:true,label:'Needs review',initials:'ES'},
  {id:'veg',name:'Vegetable jollof',variant:'Plant-based',category:'Rice dishes',price:8,cost:2.2,sold:14,recipe:false,label:'Draft',initials:'VJ'},
  {id:'puff',name:'Puff puff',variant:'Box of 12',category:'Sides & snacks',price:6,cost:1.8,sold:10,recipe:true,label:'Published',initials:'PP'},
  {id:'prawns',name:'Peppered prawns',variant:'Standard',category:'Extras',price:12,cost:null,sold:8,recipe:true,label:'Published',initials:'PR'},
];
const FOOD_INGREDIENTS = [
  {id:'rice',name:'Basmati rice',unit:'kg',onHand:10,reserved:5,reorder:6,cost:2.1,supplier:'Greenfield Foods',pack:5},
  {id:'chicken',name:'Chicken thigh',unit:'kg',onHand:15,reserved:2,reorder:5,cost:4.8,supplier:'Market Fresh',pack:5},
  {id:'tomato',name:'Tomato base',unit:'kg',onHand:8,reserved:1,reorder:3,cost:2.6,supplier:'Greenfield Foods',pack:2},
  {id:'melon',name:'Ground melon seed',unit:'kg',onHand:6,reserved:1,reorder:2,cost:6.2,supplier:'Greenfield Foods',pack:2},
  {id:'oil',name:'Vegetable oil',unit:'L',onHand:12,reserved:2,reorder:4,cost:1.9,supplier:'Greenfield Foods',pack:5},
  {id:'prawn',name:'King prawns',unit:'kg',onHand:4,reserved:1,reorder:2,cost:null,supplier:'Coastal Foods',pack:2},
];
const FOOD_BATCH_RECIPE = [
  {id:'rice',name:'Basmati rice',qty:8,unit:'kg',perPortion:.2},
  {id:'chicken',name:'Chicken thigh',qty:6.4,unit:'kg',perPortion:.16},
  {id:'tomato',name:'Tomato base',qty:4,unit:'kg',perPortion:.1},
];
const FOOD_RECIPE_COMPONENTS = {
  jollof: FOOD_BATCH_RECIPE,
  coconut: [{id:'rice',name:'Basmati rice',unit:'kg',perPortion:.2},{id:'beef',name:'Beef',unit:'kg',perPortion:.15},{id:'coconut',name:'Coconut milk',unit:'L',perPortion:.08}],
  egusi: [{id:'melon',name:'Ground melon seed',unit:'kg',perPortion:.08},{id:'tomato',name:'Tomato base',unit:'kg',perPortion:.04},{id:'oil',name:'Vegetable oil',unit:'L',perPortion:.025}],
  puff: [{id:'flour',name:'Flour',unit:'kg',perPortion:.2},{id:'oil',name:'Vegetable oil',unit:'L',perPortion:.03}],
  prawns: [{id:'prawn',name:'King prawns',unit:'kg',perPortion:.15},{id:'oil',name:'Vegetable oil',unit:'L',perPortion:.02}],
};
const FOOD_ORDERS = [
  {id:'ORD-2048',name:'Nadia Mensah',items:'Family box · 6 dishes',amount:42,payment:'Paid',status:'Packing',slot:'Friday, 6 Nov',initials:'NM'},
  {id:'ORD-2047',name:'Marcus Lee',items:'Jollof & chicken × 4',amount:38,payment:'Paid',status:'In kitchen',slot:'Friday, 6 Nov',initials:'ML'},
  {id:'ORD-2046',name:'Priya Shah',items:'Family box · 8 dishes',amount:54,payment:'Paid',status:'Ready',slot:'Friday, 6 Nov',initials:'PS'},
  {id:'ORD-2045',name:'Alex Morgan',items:'Vegetable jollof × 3',amount:24,payment:'Pending',status:'On hold',slot:'Not confirmed',initials:'AM'},
  {id:'ORD-2044',name:'Lina Kamara',items:'Egusi soup × 2',amount:22,payment:'Paid',status:'Packing',slot:'Friday, 6 Nov',initials:'LK'},
];
const FOOD_TABLES = {
  carts:{columns:['Customer','Basket','Total','Last activity','Status'],rows:[['Guest basket','Family box · 6 dishes','£42.00','24 minutes ago','Open'],['Daniel Okafor','Jollof & chicken × 2','£19.00','Yesterday, 16:40','Abandoned'],['Sofia Martinez','Family box · 4 dishes','£30.00','Yesterday, 15:12','Checked out']],note:'Only paid or committed orders enter the kitchen plan.'},
  recipes:{columns:['Dish / variant','Recipe yield','Cost per portion','Recipe','Action'],rows:[]},
  categories:{columns:['Category','Parent','Dishes','Visibility'],rows:[['Rice dishes','Main menu','3','Visible'],['Soups','Main menu','1','Visible'],['Sides & snacks','Main menu','1','Visible'],['Extras','Main menu','1','Visible']]},
  suppliers:{columns:['Supplier','Supply','Pack price','Unit price','Currency'],rows:[['Greenfield Foods','Basmati rice · 5 kg','£10.50','£2.10 / kg','GBP'],['Market Fresh','Chicken thigh · 5 kg','£24.00','£4.80 / kg','GBP'],['Coastal Foods','King prawns · 2 kg','Cost needed','—','GBP'],['Lagos Wholesale','Basmati rice · 25 kg','₦28,000','Not comparable','NGN']],note:'Unit prices are compared within one currency. No exchange rate is assumed.'},
  discounts:{columns:['Offer','Discount','Usage','Ends','Status'],rows:[['FAMILY10','10%','32 / 100 redemptions','30 Nov 2026','Active'],['WELCOME5','£5.00','18 / 50 redemptions','15 Nov 2026','Active'],['AUTUMN15','15%','75 / 75 redemptions','31 Oct 2026','Expired']]},
};
const foodMoney = value => value==null?'—':new Intl.NumberFormat('en-GB',{style:'currency',currency:'GBP'}).format(value);
const foodQty = value => Number(value.toFixed(2)).toString();
const foodBoxPrice = (size,plan={baseSize:6,basePrice:42,step:7,four:30,six:42,eight:54}) => ({4:Number(plan.four),6:Number(plan.six),8:Number(plan.eight)}[size] ?? Number(plan.basePrice)+(size-Number(plan.baseSize))*Number(plan.step));
const FOOD_LEGACY_MAP = Object.fromEntries(FOOD_SCENES.map(scene=>[scene.legacy,scene.id]));
Object.assign(window,{FOOD_SCENES,FOOD_LEGACY_MAP});
