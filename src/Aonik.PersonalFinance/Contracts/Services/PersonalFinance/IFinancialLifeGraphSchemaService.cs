using Aonik.Finance.Contracts.Models.PersonalFinance;

namespace Aonik.Finance.Contracts.Services.PersonalFinance;

public interface IFinancialLifeGraphSchemaService
{
    GraphSchemaResponse GetFullSchema();

    GraphSchemaNodeTypeResponse? GetNodeTypeSchema(string nodeType);

    string GetCompactSchemaPrompt();
}
