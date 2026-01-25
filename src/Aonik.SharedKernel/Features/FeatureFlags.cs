namespace Aonik.SharedKernel.Features;

public static class FeatureFlags
{

    public static class BillPayments
    {
        public const string Prefix = "BillPayments";

        public static class Invoicing
        {
            public const string Create = $"{Prefix}.Invoicing.Create";
            public const string Issue = $"{Prefix}.Invoicing.Issue";
            public const string Payment = $"{Prefix}.Invoicing.Payment";
            public const string Discounts = $"{Prefix}.Invoicing.Discounts";
            public const string Allocations = $"{Prefix}.Invoicing.Allocations";
        }

        public static class CustomerAccounts
        {
            public const string Management = $"{Prefix}.CustomerAccounts.Management";
        }

        public static class Collections
        {
            public const string Dunning = $"{Prefix}.Collections.Dunning";
        }

        public static class BillerCatalog
        {
            public const string Browse = $"{Prefix}.BillerCatalog.Browse";
            public const string Services = $"{Prefix}.BillerCatalog.Services";
            public const string Featured = $"{Prefix}.BillerCatalog.Featured";
        }

        public static class BillPaymentOrders
        {
            public const string Create = $"{Prefix}.BillPaymentOrders.Create";
            public const string Submit = $"{Prefix}.BillPaymentOrders.Submit";
            public const string History = $"{Prefix}.BillPaymentOrders.History";
        }
    }

    public static class MoneyTransfer
    {
        public const string Prefix = "MoneyTransfer";

        public static class PaymentIntents
        {
            public const string Create = $"{Prefix}.PaymentIntents.Create";
            public const string Capture = $"{Prefix}.PaymentIntents.Capture";
            public const string Cancel = $"{Prefix}.PaymentIntents.Cancel";
        }

        public static class Payouts
        {
            public const string Create = $"{Prefix}.Payouts.Create";
            public const string Tracking = $"{Prefix}.Payouts.Tracking";
        }

        public static class Refunds
        {
            public const string Processing = $"{Prefix}.Refunds.Processing";
            public const string Chargebacks = $"{Prefix}.Refunds.Chargebacks";
        }

        public static class Fx
        {
            public const string RateQuotes = $"{Prefix}.FX.RateQuotes";
            public const string CurrencyConversion = $"{Prefix}.FX.CurrencyConversion";
        }

        public static class Pricing
        {
            public const string QuoteGeneration = $"{Prefix}.Pricing.QuoteGeneration";
            public const string TieredPricing = $"{Prefix}.Pricing.TieredPricing";
        }

        public static class Limits
        {
            public const string TransactionLimits = $"{Prefix}.Limits.TransactionLimits";
        }

        public static class Partners
        {
            public const string Management = $"{Prefix}.Partners.Management";
            public const string Connectors = $"{Prefix}.Partners.Connectors";
            public const string Routing = $"{Prefix}.Partners.Routing";
            public const string Transmission = $"{Prefix}.Partners.Transmission";
        }
    }

    public static class PersonalFinance
    {
        public const string Prefix = "PersonalFinance";

        public static class Budgets
        {
            public const string Create = $"{Prefix}.Budgets.Create";
            public const string Tracking = $"{Prefix}.Budgets.Tracking";
            public const string AiGenerated = $"{Prefix}.Budgets.AIGenerated";
        }

        public static class Goals
        {
            public const string Create = $"{Prefix}.Goals.Create";
            public const string Tracking = $"{Prefix}.Goals.Tracking";
        }

        public static class Subscriptions
        {
            public const string Detection = $"{Prefix}.Subscriptions.Detection";
            public const string Tracking = $"{Prefix}.Subscriptions.Tracking";
        }

        public static class Bills
        {
            public const string Tracking = $"{Prefix}.Bills.Tracking";
            public const string Reminders = $"{Prefix}.Bills.Reminders";
            public const string AutoPay = $"{Prefix}.Bills.AutoPay";
        }

        public static class Transactions
        {
            public const string Categorization = $"{Prefix}.Transactions.Categorization";
            public const string AiCategories = $"{Prefix}.Transactions.AICategories";
            public const string ManualCategories = $"{Prefix}.Transactions.ManualCategories";
        }

        public static class Household
        {
            public const string Management = $"{Prefix}.Household.Management";
            public const string SharedBudgets = $"{Prefix}.Household.SharedBudgets";
        }
    }

    public static class Ai
    {
        public const string Prefix = "AI";

        public static class Platform
        {
            public const string MultiProvider = $"{Prefix}.Platform.MultiProvider";
            public const string ModelSelection = $"{Prefix}.Platform.ModelSelection";
            public const string RoutePolicies = $"{Prefix}.Platform.RoutePolicies";
            public const string RunTracking = $"{Prefix}.Platform.RunTracking";
            public const string CostTracking = $"{Prefix}.Platform.CostTracking";
        }

        public static class Prompts
        {
            public const string Versioning = $"{Prefix}.Prompts.Versioning";
            public const string Templates = $"{Prefix}.Prompts.Templates";
            public const string Safety = $"{Prefix}.Prompts.Safety";
        }

        public static class Tools
        {
            public const string DomainTools = $"{Prefix}.Tools.DomainTools";
            public const string Authorization = $"{Prefix}.Tools.Authorization";
        }

        public static class Insights
        {
            public const string Invoice = $"{Prefix}.Insights.Invoice";
            public const string General = $"{Prefix}.Insights.General";
        }

        public static class Signals
        {
            public const string AnomalyDetection = $"{Prefix}.Signals.AnomalyDetection";
            public const string FraudDetection = $"{Prefix}.Signals.FraudDetection";
        }

        public static class Feedback
        {
            public const string Collection = $"{Prefix}.Feedback.Collection";
        }

        public static class Agents
        {
            public const string Management = $"{Prefix}.Agents.Management";
            public const string DomainAgents = $"{Prefix}.Agents.DomainAgents";
        }

        public static class Proposals
        {
            public const string Generation = $"{Prefix}.Proposals.Generation";
            public const string ApprovalWorkflow = $"{Prefix}.Proposals.ApprovalWorkflow";
        }

        public static class Orchestration
        {
            public const string MultiAgent = $"{Prefix}.Orchestration.MultiAgent";
        }

        public static class Evaluation
        {
            public const string Suites = $"{Prefix}.Evaluation.Suites";
            public const string ModelComparison = $"{Prefix}.Evaluation.ModelComparison";
        }
    }
}
