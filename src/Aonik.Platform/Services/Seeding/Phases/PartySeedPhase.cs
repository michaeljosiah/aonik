using Microsoft.EntityFrameworkCore;

using Aonik.SharedKernel.Abstractions;
using Aonik.SharedKernel.Persistence;
using Aonik.Platform.Persistence;
using Aonik.Platform.Entities.Party;
using PartyEntity = Aonik.Platform.Entities.Party.Party;

namespace Aonik.Platform.Services.Seeding.Phases;

/// <summary>
/// Seeds the bill-collection party pair (Kwame / Ama) and the full
/// cross-border persona roster (Tunde, Adwoa, Peter, Naledi, Aisha, Kofi,
/// Olivia, Liam, Acme Imports, Safari Freight) with contacts, addresses,
/// profiles, role assignments, and relationships.
/// Called at Phase 7 (bill-collection parties) and Phase 14 (cross-border
/// parties) of the demo seed pipeline.
/// </summary>
internal sealed class PartySeedPhase
{
    private const string PersonPartyType = "Person";
    private const string BusinessPartyType = "Business";

    private static readonly PlatformDemoSeedIds SeedIds = PlatformDemoSeedIds.Instance;

    private static readonly Guid DemoPayerPartyId = SeedIds.DemoPair.DemoPayerPartyId;
    private static readonly Guid DemoReceiverPartyId = SeedIds.DemoPair.DemoReceiverPartyId;
    private static readonly Guid DemoRelationshipId = SeedIds.DemoPair.DemoRelationshipId;

    private static readonly Guid TundePartyId = SeedIds.Personas.TundePartyId;
    private static readonly Guid AdwoaPartyId = SeedIds.Personas.AdwoaPartyId;
    private static readonly Guid PeterPartyId = SeedIds.Personas.PeterPartyId;
    private static readonly Guid NalediPartyId = SeedIds.Personas.NalediPartyId;
    private static readonly Guid AishaPartyId = SeedIds.Personas.AishaPartyId;
    private static readonly Guid KofiPartyId = SeedIds.Personas.KofiPartyId;
    private static readonly Guid AcmeImportsPartyId = SeedIds.Personas.AcmeImportsPartyId;
    private static readonly Guid SafariFreightPartyId = SeedIds.Personas.SafariFreightPartyId;
    private static readonly Guid OliviaPartyId = SeedIds.Personas.OliviaPartyId;
    private static readonly Guid LiamPartyId = SeedIds.Personas.LiamPartyId;

    private static readonly Guid TundeAdwoaRelationshipId = SeedIds.PersonaRelationships.TundeAdwoaRelationshipId;
    private static readonly Guid TundePeterRelationshipId = SeedIds.PersonaRelationships.TundePeterRelationshipId;
    private static readonly Guid NalediAishaRelationshipId = SeedIds.PersonaRelationships.NalediAishaRelationshipId;
    private static readonly Guid KofiAmaRelationshipId = SeedIds.PersonaRelationships.KofiAmaRelationshipId;
    private static readonly Guid OliviaNalediRelationshipId = SeedIds.PersonaRelationships.OliviaNalediRelationshipId;
    private static readonly Guid LiamKwameRelationshipId = SeedIds.PersonaRelationships.LiamKwameRelationshipId;

    private static readonly Guid SeamusKeanePartyId = SeedIds.PersonalFinancePersonas.SeamusKeanePartyId;
    private static readonly Guid MarkKeanePartyId = SeedIds.PersonalFinancePersonas.MarkKeanePartyId;

    private readonly PlatformDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ICurrentUserProvider _currentUserProvider;

    public PartySeedPhase(
        PlatformDbContext dbContext,
        IClock clock,
        ICurrentUserProvider currentUserProvider)
    {
        _dbContext = dbContext;
        _clock = clock;
        _currentUserProvider = currentUserProvider;
    }

    // ── Well-known party IDs available after construction ────────────

    public static Guid[] AllDemoPartyIds => new[]
    {
        DemoPayerPartyId, DemoReceiverPartyId, TundePartyId, AdwoaPartyId, PeterPartyId,
        NalediPartyId, AishaPartyId, KofiPartyId, AcmeImportsPartyId, SafariFreightPartyId,
        OliviaPartyId, LiamPartyId,
        SeamusKeanePartyId, MarkKeanePartyId
    };

    // ── Phase 7: Bill-collection party pair ──────────────────────────

    public async Task<(Guid PayerPartyId, Guid ReceiverPartyId, Guid RelationshipId)> SeedPartiesAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        var payerEmail = "kwame.mensah@mailinator.com";
        var receiverEmail = "ama.boateng@mailinator.com";

        var payerParty = await _dbContext.Parties
            .Include(party => party.Contacts)
            .FirstOrDefaultAsync(party => party.TenantId == tenantId
                                          && party.Id == DemoPayerPartyId,
                cancellationToken);

        if (payerParty == null)
        {
            payerParty = await _dbContext.Parties
                .Include(party => party.Contacts)
                .FirstOrDefaultAsync(party => party.TenantId == tenantId
                                              && party.Contacts.Any(contact => contact.Type == "Email" && contact.Value == payerEmail),
                    cancellationToken);
        }

        if (payerParty == null)
        {
            payerParty = new PartyEntity
            {
                Id = DemoPayerPartyId,
                TenantId = tenantId,
                PartyType = PersonPartyType,
                DisplayName = "Kwame Mensah",
                Status = "Active",
                CustomerTierCode = "Retail",
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Parties.Add(payerParty);
            operations.Add("Seeded payer party");
        }

        payerParty.PartyType = PersonPartyType;
        payerParty.DisplayName = "Kwame Mensah";
        payerParty.Status = "Active";
        payerParty.CustomerTierCode = "Retail";
        payerParty.UpdatedAt = now;
        payerParty.UpdatedBy = userId;

        await UpsertPartyContactsAsync(payerParty, now, payerEmail, "+234800000000");
        await UpsertPersonProfileAsync(payerParty.Id, "Kwame", "Mensah", "NG", now, userId, cancellationToken);
        await EnsureCustomerRoleAssignmentAsync(tenantId, payerParty.Id, now, userId, cancellationToken);

        var receiverParty = await _dbContext.Parties
            .Include(party => party.Contacts)
            .FirstOrDefaultAsync(party => party.TenantId == tenantId
                                          && party.Id == DemoReceiverPartyId,
                cancellationToken);

        if (receiverParty == null)
        {
            receiverParty = await _dbContext.Parties
                .Include(party => party.Contacts)
                .FirstOrDefaultAsync(party => party.TenantId == tenantId
                                              && party.Contacts.Any(contact => contact.Type == "Email" && contact.Value == receiverEmail),
                    cancellationToken);
        }

        if (receiverParty == null)
        {
            receiverParty = new PartyEntity
            {
                Id = DemoReceiverPartyId,
                TenantId = tenantId,
                PartyType = PersonPartyType,
                DisplayName = "Ama Boateng",
                Status = "Active",
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Parties.Add(receiverParty);
            operations.Add("Seeded receiver party");
        }

        receiverParty.PartyType = PersonPartyType;
        receiverParty.DisplayName = "Ama Boateng";
        receiverParty.Status = "Active";
        receiverParty.CustomerTierCode = "Retail";
        receiverParty.UpdatedAt = now;
        receiverParty.UpdatedBy = userId;

        await UpsertPartyContactsAsync(receiverParty, now, receiverEmail, "+233200000000");
        await UpsertPersonProfileAsync(receiverParty.Id, "Ama", "Boateng", "GH", now, userId, cancellationToken);
        await EnsureCustomerRoleAssignmentAsync(tenantId, receiverParty.Id, now, userId, cancellationToken);

        var relationship = await _dbContext.PartyRelationships
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.FromPartyId == payerParty.Id
                                         && item.ToPartyId == receiverParty.Id
                                         && item.RelationshipTypeCode == "Friend",
                cancellationToken);

        if (relationship == null)
        {
            _dbContext.PartyRelationships.Add(new PartyRelationship
            {
                Id = DemoRelationshipId,
                TenantId = tenantId,
                FromPartyId = payerParty.Id,
                ToPartyId = receiverParty.Id,
                RelationshipTypeCode = "Friend",
                IsActive = true,
                Notes = "Demo relationship",
                CreatedAt = now,
                CreatedBy = userId
            });
            operations.Add("Seeded party relationship");
        }

        var relationshipId = relationship?.Id ?? DemoRelationshipId;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return (payerParty.Id, receiverParty.Id, relationshipId);
    }

    // ── Phase 7.5: Personal-finance personas (Seamus + Mark Keane) ────
    //
    // Runs after the bill-collection pair so the Keane parties are always
    // present (regardless of seedType), giving the Finance module a stable
    // pair of UK personas to attach a year of PersonalTransaction / Bill /
    // Subscription / PersonalAccount rows to. The synthetic UserIds for the
    // Finance side live in finance-demo-ids.json#personalFinancePersonas.

    public async Task<IReadOnlyList<Guid>> SeedPersonalFinancePersonasAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        var personas = new List<DemoPersonSeed>
        {
            new(SeamusKeanePartyId, "Seamus Keane", "seamus.keane@mailinator.com", "+447700900301", "GB", "Retail", "Seamus", "Keane", "IE", "Warehouse Operative", "42 Hollybush Lane", "Manchester", "England", "M14 6JP"),
            new(MarkKeanePartyId,   "Mark Keane",   "mark.keane@mailinator.com",   "+447700900302", "GB", "Premium", "Mark",  "Keane", "IE", "Senior Software Engineer", "118 Crouch End Hill", "London", "England", "N8 8DH")
        };

        var partyIds = new List<Guid>();

        foreach (var persona in personas)
        {
            var partyId = await UpsertPersonPartyAsync(tenantId, persona, now, userId, cancellationToken);
            partyIds.Add(partyId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Seeded personal-finance personas (Seamus + Mark Keane)");

        return partyIds;
    }

    // ── Phase 14: Cross-border persona roster ────────────────────────

    public async Task<(IReadOnlyList<Guid> PartyIds, IReadOnlyList<Guid> RelationshipIds)> SeedCrossBorderPartiesAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            try
            {
                return await SeedCrossBorderPartiesCoreAsync(tenantId, operations, cancellationToken);
            }
            catch (DbUpdateConcurrencyException ex) when (attempt == 1)
            {
                if (_dbContext is DbContext dbContext)
                {
                    dbContext.ChangeTracker.Clear();
                    continue;
                }

                foreach (var entry in ex.Entries)
                {
                    if (entry.State == EntityState.Added)
                    {
                        entry.State = EntityState.Detached;
                        continue;
                    }

                    if (entry.State is EntityState.Modified or EntityState.Deleted)
                    {
                        await entry.ReloadAsync(cancellationToken);
                    }
                }
            }
        }

        throw new InvalidOperationException("Unable to seed cross-border parties after retry.");
    }

    // ── Reverse: remove all demo parties and relationships ────────────

    public async Task ReversePartiesAsync(Guid tenantId, List<string> operations, CancellationToken cancellationToken)
    {
        var partyIds = AllDemoPartyIds;

        await _dbContext.PartyRelationships
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && (partyIds.Contains(item.FromPartyId) || partyIds.Contains(item.ToPartyId)))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.PartyRoleAssignments
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && partyIds.Contains(item.PartyId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.BusinessProfiles
            .IncludeSoftDeleted()
            .Where(item => partyIds.Contains(item.PartyId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.PersonProfiles
            .IncludeSoftDeleted()
            .Where(item => partyIds.Contains(item.PartyId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.PartyAddresses
            .IncludeSoftDeleted()
            .Where(item => partyIds.Contains(item.PartyId))
            .ExecuteDeleteAsync(cancellationToken);

        await _dbContext.PartyContacts
            .IncludeSoftDeleted()
            .Where(item => partyIds.Contains(item.PartyId))
            .ExecuteDeleteAsync(cancellationToken);

        var partyCount = await _dbContext.Parties
            .IncludeSoftDeleted()
            .Where(item => item.TenantId == tenantId && partyIds.Contains(item.Id))
            .ExecuteDeleteAsync(cancellationToken);

        if (partyCount > 0)
        {
            operations.Add($"Removed {partyCount} demo parties and relationships");
        }
    }

    // ── Private helpers ───────────────────────────────────────────────

    private async Task<(IReadOnlyList<Guid> PartyIds, IReadOnlyList<Guid> RelationshipIds)> SeedCrossBorderPartiesCoreAsync(
        Guid tenantId,
        List<string> operations,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var userId = _currentUserProvider.GetCurrentUserId();

        var personSeeds = new List<DemoPersonSeed>
        {
            new(TundePartyId, "Tunde Adebayo", "tunde.adebayo@mailinator.com", "+2348011110001", "NG", "Retail", "Tunde", "Adebayo", "NG", "Software Engineer", "5 Isaac John Street", "Lagos", "Lagos", "100271"),
            new(AdwoaPartyId, "Adwoa Ofori", "adwoa.ofori@mailinator.com", "+2332011110002", "GH", "SMB", "Adwoa", "Ofori", "GH", "Pharmacist", "19 Liberation Road", "Accra", "Greater Accra", "23334"),
            new(PeterPartyId, "Peter Mwangi", "peter.mwangi@mailinator.com", "+254711110003", "KE", "Retail", "Peter", "Mwangi", "KE", "Procurement Officer", "14 Ngong Road", "Nairobi", "Nairobi", "00100"),
            new(NalediPartyId, "Naledi Dlamini", "naledi.dlamini@mailinator.com", "+27711110004", "ZA", "Enterprise", "Naledi", "Dlamini", "ZA", "Finance Analyst", "28 Rivonia Road", "Johannesburg", "Gauteng", "2196"),
            new(AishaPartyId, "Aisha Bello", "aisha.bello@mailinator.com", "+234811110005", "NG", "Retail", "Aisha", "Bello", "NG", "Medical Doctor", "9 Gana Street", "Abuja", "FCT", "900271"),
            new(KofiPartyId, "Kofi Asante", "kofi.asante@mailinator.com", "+233241110006", "GH", "SMB", "Kofi", "Asante", "GH", "Accountant", "8 Castle Road", "Kumasi", "Ashanti", "00233"),
            new(OliviaPartyId, "Olivia Bennett", "olivia.bennett@mailinator.com", "+447700900101", "GB", "Enterprise", "Olivia", "Bennett", "GB", "Investment Manager", "120 Bishopsgate", "London", "England", "EC2M 3AB"),
            new(LiamPartyId, "Liam Okoro", "liam.okoro@mailinator.com", "+447700900202", "GB", "SMB", "Liam", "Okoro", "GB", "Operations Lead", "48 Canary Wharf", "London", "England", "E14 5AB")
        };

        var businessSeeds = new List<DemoBusinessSeed>
        {
            new(AcmeImportsPartyId, "Acme Imports Ltd", "acme.imports@mailinator.com", "+2348095551001", "NG", "SMB", "RC-908771", "Logistics", "Plot 3 Wharf Road", "Apapa", "Lagos", "102272"),
            new(SafariFreightPartyId, "Safari Freight Co", "safari.freight@mailinator.com", "+2547015552002", "KE", "Enterprise", "PVT-557782", "Transportation", "31 Mombasa Road", "Nairobi", "Nairobi", "00506")
        };

        var partyIds = new List<Guid>();
        partyIds.Add(DemoPayerPartyId);
        partyIds.Add(DemoReceiverPartyId);

        foreach (var person in personSeeds)
        {
            var partyId = await UpsertPersonPartyAsync(tenantId, person, now, userId, cancellationToken);
            partyIds.Add(partyId);
        }

        foreach (var business in businessSeeds)
        {
            var partyId = await UpsertBusinessPartyAsync(tenantId, business, now, userId, cancellationToken);
            partyIds.Add(partyId);
        }

        var relationshipSeeds = new List<DemoRelationshipSeed>
        {
            new(DemoRelationshipId, DemoPayerPartyId, DemoReceiverPartyId, "Friend", "Demo relationship"),
            new(TundeAdwoaRelationshipId, TundePartyId, AdwoaPartyId, "Spouse", "Household transfer relationship"),
            new(TundePeterRelationshipId, TundePartyId, PeterPartyId, "Business", "Supplier payment relationship"),
            new(NalediAishaRelationshipId, NalediPartyId, AishaPartyId, "Sibling", "Family support relationship"),
            new(KofiAmaRelationshipId, KofiPartyId, DemoReceiverPartyId, "Child", "Family support relationship"),
            new(OliviaNalediRelationshipId, OliviaPartyId, NalediPartyId, "Business", "UK sender relationship to ZA payee"),
            new(LiamKwameRelationshipId, LiamPartyId, DemoPayerPartyId, "Sibling", "UK sender relationship to NG payer")
        };

        var relationshipIds = new List<Guid>();

        foreach (var relationship in relationshipSeeds)
        {
            var relationshipId = await UpsertRelationshipAsync(tenantId, relationship, now, userId, cancellationToken);
            relationshipIds.Add(relationshipId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        operations.Add("Seeded UK-Africa customers with mailinator contacts and relationship graph");

        return (partyIds, relationshipIds);
    }

    private async Task<Guid> UpsertPersonPartyAsync(
        Guid tenantId,
        DemoPersonSeed seed,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = seed.Email.Trim().ToLowerInvariant();

        var party = await _dbContext.Parties
            .Include(item => item.Contacts)
            .Include(item => item.Addresses)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == seed.PartyId,
                cancellationToken);

        if (party == null)
        {
            party = await _dbContext.Parties
                .Include(item => item.Contacts)
                .Include(item => item.Addresses)
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                             && item.Contacts.Any(contact => contact.Type == "Email" && contact.Value == normalizedEmail),
                    cancellationToken);
        }

        if (party == null)
        {
            party = new PartyEntity
            {
                Id = seed.PartyId,
                TenantId = tenantId,
                PartyType = PersonPartyType,
                DisplayName = seed.DisplayName,
                Status = "Active",
                CustomerTierCode = seed.CustomerTier,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Parties.Add(party);
        }
        else
        {
            party.PartyType = PersonPartyType;
            party.DisplayName = seed.DisplayName;
            party.Status = "Active";
            party.CustomerTierCode = seed.CustomerTier;
            party.UpdatedAt = now;
            party.UpdatedBy = userId;
        }

        await UpsertPartyContactsAsync(party, now, seed.Email, seed.Phone);
        await UpsertPartyAddressAsync(
            party,
            "Home",
            seed.AddressLine1,
            seed.City,
            seed.State,
            seed.Postcode,
            seed.CountryCode,
            now);
        await UpsertPersonProfileAsync(
            party.Id,
            seed.FirstName,
            seed.LastName,
            seed.CountryCode,
            now,
            userId,
            cancellationToken,
            seed.Nationality,
            seed.Occupation);
        await EnsureCustomerRoleAssignmentAsync(tenantId, party.Id, now, userId, cancellationToken);

        return party.Id;
    }

    private async Task<Guid> UpsertBusinessPartyAsync(
        Guid tenantId,
        DemoBusinessSeed seed,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = seed.Email.Trim().ToLowerInvariant();

        var party = await _dbContext.Parties
            .Include(item => item.Contacts)
            .Include(item => item.Addresses)
            .FirstOrDefaultAsync(item => item.TenantId == tenantId && item.Id == seed.PartyId,
                cancellationToken);

        if (party == null)
        {
            party = await _dbContext.Parties
                .Include(item => item.Contacts)
                .Include(item => item.Addresses)
                .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                             && item.Contacts.Any(contact => contact.Type == "Email" && contact.Value == normalizedEmail),
                    cancellationToken);
        }

        if (party == null)
        {
            party = new PartyEntity
            {
                Id = seed.PartyId,
                TenantId = tenantId,
                PartyType = BusinessPartyType,
                DisplayName = seed.DisplayName,
                Status = "Active",
                CustomerTierCode = seed.CustomerTier,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.Parties.Add(party);
        }
        else
        {
            party.PartyType = BusinessPartyType;
            party.DisplayName = seed.DisplayName;
            party.Status = "Active";
            party.CustomerTierCode = seed.CustomerTier;
            party.UpdatedAt = now;
            party.UpdatedBy = userId;
        }

        await UpsertPartyContactsAsync(party, now, seed.Email, seed.Phone);
        await UpsertPartyAddressAsync(
            party,
            "Business",
            seed.AddressLine1,
            seed.City,
            seed.State,
            seed.Postcode,
            seed.CountryCode,
            now);
        await UpsertBusinessProfileAsync(
            party.Id,
            seed.RegistrationNumber,
            seed.CountryCode,
            seed.Industry,
            now,
            userId,
            cancellationToken);
        await EnsureCustomerRoleAssignmentAsync(tenantId, party.Id, now, userId, cancellationToken);

        return party.Id;
    }

    private Task UpsertPartyContactsAsync(PartyEntity party, DateTime now, string email = "kwame.mensah@mailinator.com", string phone = "+234800000000")
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var existingEmail = party.Contacts.FirstOrDefault(contact =>
            contact.Type == "Email" &&
            string.Equals(contact.Value, normalizedEmail, StringComparison.OrdinalIgnoreCase));

        if (existingEmail == null)
        {
            existingEmail = party.Contacts.FirstOrDefault(contact => contact.Type == "Email");
            if (existingEmail == null)
            {
                existingEmail = new PartyContact
                {
                    PartyId = party.Id,
                    Type = "Email",
                    Value = normalizedEmail,
                    IsPrimary = true,
                    CreatedAt = now
                };

                _dbContext.PartyContacts.Add(existingEmail);
                party.Contacts.Add(existingEmail);
            }
            else
            {
                existingEmail.Value = normalizedEmail;
                existingEmail.UpdatedAt = now;
            }
        }
        else
        {
            existingEmail.Value = normalizedEmail;
            existingEmail.UpdatedAt = now;
        }

        existingEmail.IsPrimary = true;

        foreach (var otherEmail in party.Contacts.Where(contact => contact.Type == "Email" && !ReferenceEquals(contact, existingEmail)))
        {
            otherEmail.IsPrimary = false;
        }

        var normalizedPhone = phone.Trim();
        var existingPhone = party.Contacts.FirstOrDefault(contact =>
            contact.Type == "Phone" &&
            string.Equals(contact.Value, normalizedPhone, StringComparison.OrdinalIgnoreCase));

        if (existingPhone == null)
        {
            existingPhone = party.Contacts.FirstOrDefault(contact => contact.Type == "Phone");
            if (existingPhone == null)
            {
                var newPhone = new PartyContact
                {
                    PartyId = party.Id,
                    Type = "Phone",
                    Value = normalizedPhone,
                    IsPrimary = false,
                    CreatedAt = now
                };
                _dbContext.PartyContacts.Add(newPhone);
                party.Contacts.Add(newPhone);
            }
            else
            {
                existingPhone.Value = normalizedPhone;
                existingPhone.UpdatedAt = now;
            }
        }
        else
        {
            existingPhone.Value = normalizedPhone;
            existingPhone.UpdatedAt = now;
        }

        return Task.CompletedTask;
    }

    private async Task UpsertPersonProfileAsync(
        Guid partyId,
        string firstName,
        string lastName,
        string countryCode,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken,
        string? nationality = null,
        string? occupation = null)
    {
        var profile = await _dbContext.PersonProfiles
            .FirstOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);

        if (profile == null)
        {
            _dbContext.PersonProfiles.Add(new PersonProfile
            {
                PartyId = partyId,
                FirstName = firstName,
                LastName = lastName,
                CountryCode = countryCode,
                Nationality = nationality,
                Occupation = occupation,
                IdvStatus = "Unverified",
                CreatedAt = now,
                CreatedBy = userId
            });
        }
        else
        {
            profile.FirstName = firstName;
            profile.LastName = lastName;
            profile.CountryCode = countryCode;
            profile.Nationality = nationality;
            profile.Occupation = occupation;
            profile.IdvStatus = "Unverified";
            profile.UpdatedAt = now;
            profile.UpdatedBy = userId;
        }
    }

    private async Task UpsertBusinessProfileAsync(
        Guid partyId,
        string registrationNumber,
        string incorporationCountry,
        string industry,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.BusinessProfiles
            .FirstOrDefaultAsync(item => item.PartyId == partyId, cancellationToken);

        if (profile == null)
        {
            _dbContext.BusinessProfiles.Add(new BusinessProfile
            {
                PartyId = partyId,
                RegistrationNumber = registrationNumber,
                IncorporationCountry = incorporationCountry,
                Industry = industry,
                KybStatus = "Unverified",
                CreatedAt = now,
                CreatedBy = userId
            });
            return;
        }

        profile.RegistrationNumber = registrationNumber;
        profile.IncorporationCountry = incorporationCountry;
        profile.Industry = industry;
        profile.KybStatus = "Unverified";
        profile.UpdatedAt = now;
        profile.UpdatedBy = userId;
    }

    private async Task EnsureCustomerRoleAssignmentAsync(
        Guid tenantId,
        Guid partyId,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var assignment = await _dbContext.PartyRoleAssignments
            .FirstOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.PartyId == partyId &&
                item.Role == PartyRoles.Customer &&
                item.ContextType == "Tenant" &&
                item.ContextId == tenantId,
                cancellationToken);

        if (assignment != null)
        {
            return;
        }

        _dbContext.PartyRoleAssignments.Add(new PartyRoleAssignment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            PartyId = partyId,
            Role = PartyRoles.Customer,
            ContextType = "Tenant",
            ContextId = tenantId,
            CreatedAt = now,
            CreatedBy = userId
        });
    }

    private async Task UpsertPartyAddressAsync(
        PartyEntity party,
        string type,
        string line1,
        string city,
        string state,
        string postcode,
        string country,
        DateTime now)
    {
        var address = party.Addresses.FirstOrDefault(item => item.Type == type);
        if (address == null)
        {
            var newAddress = new PartyAddress
            {
                PartyId = party.Id,
                Type = type,
                Line1 = line1,
                City = city,
                State = state,
                Postcode = postcode,
                Country = country,
                CreatedAt = now
            };
            _dbContext.PartyAddresses.Add(newAddress);
            party.Addresses.Add(newAddress);
            return;
        }

        address.Line1 = line1;
        address.City = city;
        address.State = state;
        address.Postcode = postcode;
        address.Country = country;
        address.UpdatedAt = now;
    }

    private async Task<Guid> UpsertRelationshipAsync(
        Guid tenantId,
        DemoRelationshipSeed seed,
        DateTime now,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var relationship = await _dbContext.PartyRelationships
            .FirstOrDefaultAsync(item => item.TenantId == tenantId
                                         && item.FromPartyId == seed.FromPartyId
                                         && item.ToPartyId == seed.ToPartyId
                                         && item.RelationshipTypeCode == seed.RelationshipTypeCode,
                cancellationToken);

        if (relationship == null)
        {
            relationship = new PartyRelationship
            {
                Id = seed.RelationshipId,
                TenantId = tenantId,
                FromPartyId = seed.FromPartyId,
                ToPartyId = seed.ToPartyId,
                RelationshipTypeCode = seed.RelationshipTypeCode,
                IsActive = true,
                Notes = seed.Notes,
                CreatedAt = now,
                CreatedBy = userId
            };
            _dbContext.PartyRelationships.Add(relationship);
        }
        else
        {
            relationship.IsActive = true;
            relationship.Notes = seed.Notes;
            relationship.UpdatedAt = now;
            relationship.UpdatedBy = userId;
        }

        return relationship.Id;
    }

    // ── Seed record types ─────────────────────────────────────────────

    private sealed record DemoPersonSeed(
        Guid PartyId,
        string DisplayName,
        string Email,
        string Phone,
        string CountryCode,
        string CustomerTier,
        string FirstName,
        string LastName,
        string Nationality,
        string Occupation,
        string AddressLine1,
        string City,
        string State,
        string Postcode);

    private sealed record DemoBusinessSeed(
        Guid PartyId,
        string DisplayName,
        string Email,
        string Phone,
        string CountryCode,
        string CustomerTier,
        string RegistrationNumber,
        string Industry,
        string AddressLine1,
        string City,
        string State,
        string Postcode);

    private sealed record DemoRelationshipSeed(
        Guid RelationshipId,
        Guid FromPartyId,
        Guid ToPartyId,
        string RelationshipTypeCode,
        string Notes);
}
