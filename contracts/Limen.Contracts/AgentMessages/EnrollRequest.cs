namespace Limen.Contracts.AgentMessages;

public sealed record EnrollRequest(
    string ProvisioningKey,
    string Hostname,
    string[] Roles,
    string Platform,
    string AgentVersion);

public sealed record EnrollResponse(
    Guid AgentId,
    string PermanentSecret,
    string TunnelSubnet);    // reserved for Plan 3; empty string in Plan 2
