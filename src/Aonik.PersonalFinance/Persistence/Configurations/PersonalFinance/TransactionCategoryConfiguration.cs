using System.Security.Cryptography;
using System.Text;

using Aonik.PersonalFinance.Entities;
using Aonik.PersonalFinance.Services;
using Aonik.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Aonik.PersonalFinance.Persistence.Configurations;

internal sealed class TransactionCategoryConfiguration : IEntityTypeConfiguration<TransactionCategory>
{
    /// <summary>
    /// Fixed namespace GUID used to derive deterministic IDs for each category code.
    /// This ensures the same category always gets the same GUID across migration runs.
    /// </summary>
    private static readonly Guid SeedNamespace = new("A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D");

    public void Configure(EntityTypeBuilder<TransactionCategory> builder)
    {
        builder.ToTable("TransactionCategories", SchemaNames.Default);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.GroupName)
            .HasMaxLength(100);

        builder.Property(x => x.IconName)
            .HasMaxLength(100);

        builder.HasIndex(x => x.Code)
            .IsUnique();

        builder.HasIndex(x => x.SortOrder);

        SeedCategories(builder);
    }

    private static void SeedCategories(EntityTypeBuilder<TransactionCategory> builder)
    {
        var categories = TransactionCategoryReference.GetAllCategories();
        var seedDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var seedEntities = categories.Select(cat => new TransactionCategory
        {
            Id = CreateDeterministicGuid(cat.Code),
            Code = cat.Code,
            DisplayName = cat.DisplayName,
            GroupName = cat.GroupName,
            IconName = cat.IconName,
            SortOrder = cat.SortOrder,
            IsActive = true,
            CreatedAt = seedDate,
            IsDeleted = false,
            RowVersion = [],
        }).ToArray();

        builder.HasData(seedEntities);
    }

    /// <summary>
    /// Creates a deterministic GUID from a category code by hashing the namespace GUID
    /// concatenated with the UTF-8 bytes of the code using SHA-256, then taking the
    /// first 16 bytes as a GUID.
    /// </summary>
    private static Guid CreateDeterministicGuid(string code)
    {
        var namespaceBytes = SeedNamespace.ToByteArray();
        var codeBytes = Encoding.UTF8.GetBytes(code);

        var combined = new byte[namespaceBytes.Length + codeBytes.Length];
        Buffer.BlockCopy(namespaceBytes, 0, combined, 0, namespaceBytes.Length);
        Buffer.BlockCopy(codeBytes, 0, combined, namespaceBytes.Length, codeBytes.Length);

        var hash = SHA256.HashData(combined);

        var guidBytes = new byte[16];
        Array.Copy(hash, guidBytes, 16);

        return new Guid(guidBytes);
    }
}
