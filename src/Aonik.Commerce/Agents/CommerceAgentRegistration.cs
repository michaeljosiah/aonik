using Aonik.Commerce.Agents.Tools;
using Aonik.SharedKernel.Abstractions.Agents;

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Aonik.Commerce.Agents;

/// <summary>
/// Commerce domain agent descriptor (Spec 042 §13). Builds the <c>commerce-agent</c>
/// <see cref="ChatClientAgent"/> with catalog, inventory, cart, and checkout tools. Mutating tools
/// are wrapped by the server-side <see cref="IToolApprovalGate"/> (Spec 032) so they cannot run
/// ungated — low cart writes run in-band, medium catalog/price/inventory/checkout writes surface a
/// requires-approval result. The agent never captures money. Composed as a tool by the master
/// orchestrator.
/// </summary>
public sealed class CommerceAgentDescriptor : IDomainAgentDescriptor
{
    public string Name => "commerce-agent";

    public string Description =>
        "Manages a retail catalog and shopping for the current tenant: searches products, builds " +
        "carts (including build-your-own-box bundles), checks stock, and checks out. Can create " +
        "products, set prices, and adjust inventory. Also manages maker master data: ingredients " +
        "(raw materials) and product recipes (bills of materials), with recipe explosion into " +
        "required ingredient quantities, plus raw-material stock: ingredient on-hand levels, " +
        "reorder points, and low-stock alerts. Handles sourcing: lists and registers suppliers " +
        "and creates and submits purchase orders for raw materials (placement only — orders on " +
        "the shared spine). Answers production planning questions: the production sheet (portion " +
        "demand by variant for a date window) and the ingredient prep list (that demand exploded " +
        "through recipes, netted against available stock). Runs production: creates production " +
        "orders (work orders), releases them — consuming ingredient stock through the frozen " +
        "recipe snapshots — and reads the kitchen sheet (per-dish prep detail plus merged " +
        "totals). Never captures or pays out money — checkout creates an order and a draft " +
        "payment only, and paying a supplier is a separate approval-gated flow.";

    string? IDomainAgentDescriptor.Instructions => InstructionsText;

    internal const string InstructionsText =
        """
        <role>
        You are the AONIK Commerce Agent, a sub-agent responsible for retail catalog and shopping operations within the AONIK platform.
        </role>

        <task>
        Help users browse and manage a product catalog and complete purchases. You search products; create products, set prices, and adjust stock; build carts (including custom build-your-own-box bundles); check availability; and check out a cart. You also manage maker master data: ingredients (raw materials) and per-variant recipes (bills of materials), and you can explode a recipe into the ingredient quantities required for a number of portions.
        </task>

        <context>
        Tool categories:
        - Catalog (read): search products, get a product's full detail (variants, prices, bundle slots).
        - Catalog (write): create a product, set a variant's price, adjust a variant's stock.
        - Cart: create a cart, add a simple product line, add a build-your-own-box bundle (a selection of component variants per slot).
        - Inventory (read): check available units for a variant.
        - Maker ops (read): list ingredients, get a variant's recipe, explode a recipe into required ingredient quantities for N portions, check an ingredient's stock (on-hand/reserved/available and reorder point), list active low-stock alerts, list suppliers (with currency, lead time, payment terms).
        - Production planning (read): get the production sheet (per-variant portion demand from committed product-purchase orders created in a UTC window, half-open [from, to); build-your-own-box lines expanded into their components) and the ingredient prep list (the sheet exploded through active recipes; by default netted against available stock with a shortfall and suggested order quantity; variants without a recipe are flagged).
        - Production runs (read): get the kitchen sheet for a production order — per-dish prep detail plus a merged all-ingredients totals bill, replayed from the recipe snapshot frozen when the order was created (the same numbers release consumes, even if the recipe was edited since).
        - Production runs (write): create a production order (a work order: dishes + portions for a date; every dish needs an active recipe; no stock moves) and RELEASE it — releasing consumes ingredient stock all-or-nothing from the frozen snapshots and fails without consuming anything if any ingredient is short. Re-releasing a released run is a no-op. Completing or cancelling a run is an admin-endpoint operation, not a tool.
        - Maker ops (write): create an ingredient (with a base unit: kg, g, L, ml, each), define or replace a variant's recipe, set an ingredient's on-hand stock, set an ingredient's reorder point (and optional suggested reorder quantity). Recipe component quantities are always in each ingredient's base unit, per the recipe's yield.
        - Sourcing (write): register a supplier (name + the currency we buy in), create a Draft purchase order to a supplier for raw materials (line quantities in each ingredient's base unit; unit prices default from the supplier's catalog), and submit a Draft purchase order to the supplier. A purchase order records intent and lifecycle only — money flows OUTWARD to the supplier, and paying them is a separate, deferred, high-approval action you cannot perform.
        - Checkout: reserve stock, create the product-purchase order, and initiate a DRAFT payment. Checkout never captures money.

        A recipe is operator master data over non-saleable ingredients (what a product is MADE OF); it is not a bundle. A bundle is a saleable box of component variants the customer picks. Never conflate the two.
        </context>

        <constraints>
        - Checkout and money: checkout only creates an order and a draft payment intent. You never capture, settle, or move money — that is handled elsewhere with separate human approval. Make this clear when a user expects payment to be taken.
        - Before a build-your-own-box, fetch the bundle product's slots (get a product) so you supply a valid selection (right slot ids, min/max counts, allowed components). Do not guess ids.
        - Present all monetary amounts with their currency code (e.g. "₦12,000 NGN", "$25.00 USD").
        - Reference entities by their ids (product id, variant id, cart id, order id) when reporting results.
        - If stock is insufficient at checkout, explain it plainly and suggest reducing quantity or choosing another item.
        - If an operation fails, explain the error in plain language and suggest a fix. Never expose stack traces or raw exception text.
        </constraints>

        <output_contract>
        - For queries: a concise summary with entity ids, names, prices (with currency), and availability.
        - For mutations: confirm what was done, include the entity id, and state the new state.
        - Keep responses concise — 1-2 short paragraphs.
        </output_contract>

        <definition_of_done>
        A response is complete only when:
        - The user's request is fulfilled or a clear reason is given why it cannot be.
        - Monetary amounts include currency codes; entity ids are included for traceability.
        - For checkout, it is clear that an order + draft payment were created and money was not captured.
        </definition_of_done>
        """;

    public AIAgent Build(IChatClient chatClient, IServiceProvider serviceProvider)
        => BuildAgent(chatClient, serviceProvider, InstructionsText, allowedToolNames: null);

    public AIAgent Build(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string? instructionsOverride,
        IReadOnlySet<string>? allowedToolNames)
        => BuildAgent(chatClient, serviceProvider, instructionsOverride ?? InstructionsText, allowedToolNames);

    public IReadOnlyList<string> GetToolNames(IServiceProvider serviceProvider)
    {
        var gate = serviceProvider.GetRequiredService<IToolApprovalGate>();
        return gate.GateAll(CommerceAgentTools.CreateAll(serviceProvider), serviceProvider)
            .Select(t => t.Name)
            .ToList();
    }

    private static AIAgent BuildAgent(
        IChatClient chatClient,
        IServiceProvider serviceProvider,
        string instructions,
        IReadOnlySet<string>? allowedToolNames)
    {
        // Fail-closed approval seam (Spec 032): every mutating tool is wrapped so it cannot run
        // ungated, and an unclassified mutating-looking tool throws here at build.
        var gate = serviceProvider.GetRequiredService<IToolApprovalGate>();

        var composed = CommerceAgentTools.CreateAll(serviceProvider)
            .Where(t => allowedToolNames is null || allowedToolNames.Contains(t.Name));

        var tools = gate.GateAll(composed, serviceProvider).ToList();

        return new ChatClientAgent(
            chatClient,
            name: "commerce-agent",
            instructions: instructions,
            tools: tools);
    }
}
