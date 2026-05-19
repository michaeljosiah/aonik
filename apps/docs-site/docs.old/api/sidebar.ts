import type { SidebarsConfig } from "@docusaurus/plugin-content-docs";

const sidebar: SidebarsConfig = {
  apisidebar: [
    {
      type: "doc",
      id: "api/aonik-api",
    },
    {
      type: "category",
      label: "Orders",
      link: {
        type: "doc",
        id: "api/orders",
      },
      items: [
        {
          type: "doc",
          id: "api/list-orders",
          label: "List orders",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/create-bill-payment-order",
          label: "Create a bill payment order",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-order",
          label: "Retrieve an order",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/submit-order",
          label: "Submit an order",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/cancel-order",
          label: "Cancel an order",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/add-order-item",
          label: "Add an item to an order",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/update-order-item",
          label: "Update an order item",
          className: "api-method put",
        },
        {
          type: "doc",
          id: "api/remove-order-item",
          label: "Remove an order item",
          className: "api-method delete",
        },
      ],
    },
    {
      type: "category",
      label: "Payments",
      link: {
        type: "doc",
        id: "api/payments",
      },
      items: [
        {
          type: "doc",
          id: "api/create-payment-intent",
          label: "Create a payment intent",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-payment-intent",
          label: "Retrieve a payment intent",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/capture-payment",
          label: "Capture a payment",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/cancel-payment",
          label: "Cancel a payment",
          className: "api-method post",
        },
      ],
    },
    {
      type: "category",
      label: "Billing",
      link: {
        type: "doc",
        id: "api/billing",
      },
      items: [
        {
          type: "doc",
          id: "api/create-invoice",
          label: "Create an invoice",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-invoice",
          label: "Retrieve an invoice",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "Ledger",
      link: {
        type: "doc",
        id: "api/ledger",
      },
      items: [
        {
          type: "doc",
          id: "api/list-ledgers",
          label: "List ledgers",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/create-ledger",
          label: "Create a ledger",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/list-ledger-accounts",
          label: "List ledger accounts",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/create-ledger-account",
          label: "Create a ledger account",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/list-journal-entries",
          label: "List journal entries",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/add-journal-entry",
          label: "Add a journal entry",
          className: "api-method post",
        },
      ],
    },
    {
      type: "category",
      label: "Catalog",
      link: {
        type: "doc",
        id: "api/catalog",
      },
      items: [
        {
          type: "doc",
          id: "api/list-countries",
          label: "List countries",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/list-currencies",
          label: "List currencies",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/list-biller-categories",
          label: "List biller categories",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/list-billers",
          label: "List billers",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/get-biller",
          label: "Retrieve a biller",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/list-biller-services",
          label: "List biller services",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/get-biller-service",
          label: "Retrieve a biller service",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/validate-service-fields",
          label: "Validate service fields",
          className: "api-method post",
        },
      ],
    },
    {
      type: "category",
      label: "Pricing",
      link: {
        type: "doc",
        id: "api/pricing",
      },
      items: [
        {
          type: "doc",
          id: "api/get-pricing-quote",
          label: "Get a pricing quote",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/list-fx-quotes",
          label: "List FX quotes",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/create-fx-quote",
          label: "Create an FX quote",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-fx-quote",
          label: "Retrieve an FX quote",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/update-fx-quote",
          label: "Update an FX quote",
          className: "api-method put",
        },
        {
          type: "doc",
          id: "api/delete-fx-quote",
          label: "Delete an FX quote",
          className: "api-method delete",
        },
      ],
    },
    {
      type: "category",
      label: "Partners",
      link: {
        type: "doc",
        id: "api/partners",
      },
      items: [
        {
          type: "doc",
          id: "api/list-partners",
          label: "List partners",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/create-partner",
          label: "Create a partner",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-partner",
          label: "Retrieve a partner",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/update-partner",
          label: "Update a partner",
          className: "api-method patch",
        },
        {
          type: "doc",
          id: "api/delete-partner",
          label: "Delete a partner",
          className: "api-method delete",
        },
      ],
    },
    {
      type: "category",
      label: "Identity",
      link: {
        type: "doc",
        id: "api/identity",
      },
      items: [
        {
          type: "doc",
          id: "api/get-token",
          label: "Get token",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-current-user",
          label: "Get current user",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/get-user-info",
          label: "Get user info",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/get-my-profile",
          label: "Get my profile",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/update-my-profile",
          label: "Update my profile",
          className: "api-method put",
        },
        {
          type: "doc",
          id: "api/forgot-password",
          label: "Request password reset",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/start-email-verification",
          label: "Start email verification",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/confirm-email-verification",
          label: "Confirm email verification",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/health-check",
          label: "Health check",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "Registration",
      link: {
        type: "doc",
        id: "api/registration",
      },
      items: [
        {
          type: "doc",
          id: "api/register-individual",
          label: "Register an individual",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-onboarding-status",
          label: "Get onboarding status",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "Tenants",
      link: {
        type: "doc",
        id: "api/tenants",
      },
      items: [
        {
          type: "doc",
          id: "api/list-tenants",
          label: "List tenants",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/create-tenant",
          label: "Create a tenant",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-tenant",
          label: "Retrieve a tenant",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/update-tenant",
          label: "Update a tenant",
          className: "api-method patch",
        },
        {
          type: "doc",
          id: "api/activate-tenant",
          label: "Activate a tenant",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/deactivate-tenant",
          label: "Deactivate a tenant",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/provision-tenant",
          label: "Provision a tenant",
          className: "api-method post",
        },
      ],
    },
    {
      type: "category",
      label: "Users",
      link: {
        type: "doc",
        id: "api/users",
      },
      items: [
        {
          type: "doc",
          id: "api/list-users",
          label: "List users",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/invite-user",
          label: "Invite a user",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-user",
          label: "Retrieve a user",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/activate-user",
          label: "Activate a user",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/deactivate-user",
          label: "Deactivate a user",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/update-user-roles",
          label: "Update user roles",
          className: "api-method put",
        },
      ],
    },
    {
      type: "category",
      label: "Roles",
      link: {
        type: "doc",
        id: "api/roles",
      },
      items: [
        {
          type: "doc",
          id: "api/list-roles",
          label: "List roles",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/create-role",
          label: "Create a role",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-role",
          label: "Retrieve a role",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/update-role",
          label: "Update a role",
          className: "api-method put",
        },
        {
          type: "doc",
          id: "api/delete-role",
          label: "Delete a role",
          className: "api-method delete",
        },
        {
          type: "doc",
          id: "api/update-role-permissions",
          label: "Update role permissions",
          className: "api-method put",
        },
        {
          type: "doc",
          id: "api/list-permissions",
          label: "List permissions",
          className: "api-method get",
        },
      ],
    },
    {
      type: "category",
      label: "Compliance",
      link: {
        type: "doc",
        id: "api/compliance",
      },
      items: [
        {
          type: "doc",
          id: "api/list-documents",
          label: "List compliance documents",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/create-document",
          label: "Create a compliance document",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-document",
          label: "Retrieve a compliance document",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/upload-document-file",
          label: "Upload a document file",
          className: "api-method post",
        },
      ],
    },
    {
      type: "category",
      label: "AI",
      link: {
        type: "doc",
        id: "api/ai",
      },
      items: [
        {
          type: "doc",
          id: "api/chat",
          label: "Chat with an agent",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/list-chat-threads",
          label: "List chat threads",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/get-chat-thread",
          label: "Retrieve a chat thread",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/archive-chat-thread",
          label: "Archive a chat thread",
          className: "api-method delete",
        },
        {
          type: "doc",
          id: "api/list-agents",
          label: "List agents",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/get-agent-configuration",
          label: "Get agent configuration",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/upsert-agent-configuration",
          label: "Update agent configuration",
          className: "api-method put",
        },
        {
          type: "doc",
          id: "api/delete-agent-configuration",
          label: "Delete agent configuration",
          className: "api-method delete",
        },
        {
          type: "doc",
          id: "api/run-workflow",
          label: "Run a workflow",
          className: "api-method post",
        },
      ],
    },
    {
      type: "category",
      label: "CMS",
      link: {
        type: "doc",
        id: "api/cms",
      },
      items: [
        {
          type: "doc",
          id: "api/list-content-blocks",
          label: "List content blocks",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/create-content-block",
          label: "Create a content block",
          className: "api-method post",
        },
        {
          type: "doc",
          id: "api/get-content-block",
          label: "Retrieve a content block",
          className: "api-method get",
        },
        {
          type: "doc",
          id: "api/update-content-block",
          label: "Update a content block",
          className: "api-method put",
        },
        {
          type: "doc",
          id: "api/delete-content-block",
          label: "Delete a content block",
          className: "api-method delete",
        },
        {
          type: "doc",
          id: "api/get-active-content",
          label: "Get active content",
          className: "api-method get",
        },
      ],
    },
  ],
};

export default sidebar.apisidebar;
