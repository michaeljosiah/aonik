namespace Aonik.Finance.Contracts.Models.Remittance;

/// <summary>A rejected operator request for which the service verified that no remittance order exists.</summary>
public sealed class RemittanceNotStartedException(string message, Exception innerException) : Exception(message, innerException);
