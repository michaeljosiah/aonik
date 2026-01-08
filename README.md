# AONIK

**AONIK** is an **AI-native financial infrastructure platform** designed to power modern payments, remittances, billing, and financial intelligence. Built from the ground up with AI in mind, AONIK provides core financial primitives alongside intelligent agents that assist with reconciliation, forecasting, anomaly detection, and insights.

The project serves as a foundational layer for both consumer and business financial products, with an initial focus on Africa and the global diaspora, while remaining flexible enough for global use cases.

---

## 🚧 Project Status

⚠️ **Early Development**

AONIK is currently in **active early development**. APIs, data models, and architectural components are evolving, and **breaking changes should be expected**.

This is the ideal stage for contributors who want to help shape the core design of an AI-first financial platform.

---

## ✨ Key Principles

AONIK is built around the following design principles:

- **AI-native by design**  
  Financial primitives are structured to support intelligence, automation, and explainability from day one.

- **Composable primitives**  
  Transactions, ledgers, invoices, customers, and accounts are first-class building blocks.

- **Agent-oriented architecture**  
  AI agents operate directly on core primitives rather than being bolted on as chatbots.

- **Explainable & auditable**  
  AI assists and recommends, while humans remain in control — critical for financial trust.

- **Open by default**  
  Open-source core with a strong focus on transparency, extensibility, and community collaboration.

---

## 🧠 AI-Native Capabilities

AONIK is designed to support intelligent behaviour across financial systems, including:

- Transaction classification and enrichment
- Automated reconciliation
- Anomaly and fraud signal detection
- Cash flow forecasting
- Budgeting and spend insights
- Financial data summarisation and explanation

These capabilities are implemented through **built-in agents** that work directly with AONIK’s core data models.

---

## 🧩 What Can Be Built on AONIK?

AONIK is intended to power a wide range of products and services, including:

- Personal finance assistants
- Cross-border remittance platforms
- Billing and invoicing systems
- Subscription and recurring payment services
- SME and enterprise finance tools
- AI-driven financial insights and analytics

---

## 🛠️ Tech Direction (Subject to Change)

The project is expected to evolve around:

- Modular, domain-driven design
- Clear separation between core primitives and agents
- API-first architecture
- Pluggable AI model providers
- Strong testability and observability

Concrete implementation details will mature as the project evolves.

---

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- SQL Server LocalDB (included with Visual Studio or SQL Server Express)
- Git for version control

### Running the API

```bash
# Clone the repository
git clone https://github.com/yourusername/aonik.git
cd aonik

# Restore dependencies and build
dotnet build Aonik.sln

# Apply database migrations (when available)
dotnet ef database update --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# Run the API
dotnet run --project src/Aonik.Api
```

The API will start on `https://localhost:5001` with Swagger UI available at `https://localhost:5001/swagger`

### Build Status

The solution currently builds successfully with:
- ✅ All projects compile without errors
- ⚠️ Some tests may fail due to ongoing development
- 📦 All NuGet packages resolved correctly

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests with detailed output
dotnet test --logger "console;verbosity=detailed"

# Run tests for a specific project
dotnet test tests/Aonik.Application.Tests
```

### Quick Commands

```bash
# Build the entire solution
dotnet build Aonik.sln

# Clean and rebuild
dotnet clean Aonik.sln && dotnet build Aonik.sln

# Run specific test by filter
dotnet test --filter "DisplayName~CreateInvoice"

# Create a new migration
dotnet ef migrations add <MigrationName> --project src/Aonik.Infrastructure --startup-project src/Aonik.Api

# Remove last migration
dotnet ef migrations remove --project src/Aonik.Infrastructure --startup-project src/Aonik.Api
```

### Documentation

For detailed technical information, see:
- **[AGENTS.md](AGENTS.md)** - Coding standards, build commands, and architectural patterns for AI agents
- **[docs/Troubleshooting.md](docs/Troubleshooting.md)** - Common issues and solutions
- **[docs/Testing.md](docs/Testing.md)** - Testing guidelines and patterns
- **[CHANGELOG.md](CHANGELOG.md)** - Version history and recent changes

---

## 🤝 Contributing

Contributions are welcome and encouraged.

If you are interested in:
- Financial systems
- AI agents
- Open-source infrastructure
- Building for emerging markets

…this project is for you.

Please note:
- The project is evolving rapidly
- Expect refactors and breaking changes
- Discussions and proposals are encouraged early

(Contribution guidelines will be added as the project stabilises.)

---

## 📜 License

AONIK is licensed under the **Apache License, Version 2.0**.

You are free to use, modify, and distribute this software in compliance with the license.  
See the `LICENSE` file for full details.

---

## 🌍 Vision

AONIK aims to become a **trusted, intelligent foundation for financial systems**, enabling developers and businesses to build adaptive, transparent, and AI-assisted finance products — starting with Africa and the global diaspora, and scaling globally over time.

---

*This project is just getting started. The foundations you help build today will shape what AONIK becomes tomorrow.*


