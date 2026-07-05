using Aonik.PersonalFinance.Contracts.Models;

namespace Aonik.PersonalFinance.Contracts.Services;

public interface IFinancialLifeGraphSchemaService
{
    GraphSchemaResponse GetFullSchema();

    GraphSchemaNodeTypeResponse? GetNodeTypeSchema(string nodeType);

    string GetCompactSchemaPrompt();
}
