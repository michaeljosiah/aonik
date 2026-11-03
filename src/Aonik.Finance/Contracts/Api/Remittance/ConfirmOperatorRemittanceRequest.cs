namespace Aonik.Finance.Contracts.Api.Remittance;

public record ConfirmOperatorRemittanceRequest(ConfirmRemittanceRequest Transfer, bool ConfirmExecution);
