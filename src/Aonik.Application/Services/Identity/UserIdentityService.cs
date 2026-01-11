using System.Text.Json;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using Aonik.Application.Abstractions.Observability;
using Aonik.Application.Abstractions.Persistence;
using Aonik.Application.Services.Compliance;
using Aonik.Domain.Identity.Entities;

namespace Aonik.Application.Services.Identity;

public class UserIdentityService : IUserIdentityService
{
    private readonly IAonikDbContext _dbContext;
    private readonly ILogger<UserIdentityService> _logger;
    private readonly IAuditLogWriter _auditLogWriter;
    private readonly ICorrelationContext _correlationContext;
    
    public UserIdentityService(
        IAonikDbContext dbContext,
        ILogger<UserIdentityService> logger,
        IAuditLogWriter auditLogWriter,
        ICorrelationContext correlationContext)
    {
        _dbContext = dbContext;
        _logger = logger;
        _auditLogWriter = auditLogWriter;
        _correlationContext = correlationContext;
    }
    
    public async Task<User> ResolveOrCreateUserAsync(
        string externalIssuer,
        string externalSubject,
        string? externalTenantId,
        string? email,
        Guid aonikTenantId,
        CancellationToken ct = default)
    {
        // Lookup existing user by external identity
        var existingUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => 
                u.TenantId == aonikTenantId &&
                u.ExternalIssuer == externalIssuer &&
                u.ExternalSubject == externalSubject,
                ct);
        
        if (existingUser != null)
        {
            // Update email if changed
            if (!string.IsNullOrEmpty(email) && existingUser.Email != email)
            {
                existingUser.Email = email;
                await _dbContext.SaveChangesAsync(ct);
                _logger.LogInformation("Updated email for user {UserId}", existingUser.Id);
            }
            
            return existingUser;
        }
        
        // Verify tenant exists and is active
        var tenant = await _dbContext.Tenants
            .FirstOrDefaultAsync(t => t.TenantId == aonikTenantId, ct);
        
        if (tenant == null)
        {
            _logger.LogError("Attempted to create user in non-existent tenant {TenantId}", aonikTenantId);
            throw new InvalidOperationException($"Tenant {aonikTenantId} does not exist");
        }
        
        if (tenant.Status != "Active")
        {
            _logger.LogWarning("Attempted to create user in non-active tenant {TenantId} (Status: {Status})", 
                aonikTenantId, tenant.Status);
            throw new InvalidOperationException($"Tenant {aonikTenantId} is not active (Status: {tenant.Status})");
        }
        
        // Create new user (JIT provisioning)
        var newUser = new User
        {
            Id = Guid.NewGuid(),
            TenantId = aonikTenantId,
            ExternalIssuer = externalIssuer,
            ExternalSubject = externalSubject,
            ExternalTenantId = externalTenantId,
            Email = email, // Nullable - only if present
            Status = "Active"
        };
        
        _dbContext.Users.Add(newUser);
        await _dbContext.SaveChangesAsync(ct);
        
        _logger.LogInformation("Created new user {UserId} via JIT provisioning (Issuer: {Issuer}, Subject: {Subject})",
            newUser.Id, externalIssuer, externalSubject);

        await _auditLogWriter.LogAsync(
            AuditEventNames.UserProvisioned,
            "User",
            newUser.Id,
            aonikTenantId,
            newUser.Id,
            _correlationContext.CorrelationId,
            JsonSerializer.Serialize(new
            {
                newUser.Id,
                Email = AuditLogMasking.MaskEmail(newUser.Email),
                newUser.ExternalIssuer,
                newUser.ExternalSubject,
                newUser.ExternalTenantId
            }),
            ct);
        
        return newUser;
    }
}
