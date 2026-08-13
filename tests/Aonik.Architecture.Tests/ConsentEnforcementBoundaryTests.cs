using System.Reflection;

using Aonik.SharedKernel.Abstractions.Consent;

using FluentAssertions;

namespace Aonik.Architecture.Tests;

/// <summary>
/// Spec 095 §12.1 — keeps the consent enforcement point singular.
///
/// <para>
/// <see cref="IConsentReader"/> answers a question and returns a boolean, which is fine for
/// presentation and dangerous for authorisation: a caller writing
/// <c>if (await reader.HasConsentAsync(…))</c> can simply omit the <c>else</c>, and the failure is
/// silent. <see cref="IConsentGate"/> exists so there is no shape of call site that accidentally
/// continues — it throws or it proceeds.
/// </para>
///
/// <para>
/// These tests are the standing guarantee that the distinction survives the next person who needs a
/// consent check in a hurry. They are the analogue of Spec 032's build-time failure for an
/// unclassified mutating tool: the point is not to catch today's code, which is correct, but to make
/// tomorrow's mistake impossible to land quietly.
/// </para>
/// </summary>
public class ConsentEnforcementBoundaryTests
{
    /// <summary>
    /// The only types permitted to consume <see cref="IConsentReader"/> directly. The gate composes
    /// it; the service reads its own writes. Everything else must go through the gate.
    /// </summary>
    private static readonly string[] PermittedReaderConsumers =
    [
        "Aonik.Platform.Services.Consent.ConsentGate",
        "Aonik.Platform.Services.Consent.ConsentService",
    ];

    private static readonly string[] ModuleAssemblies =
    [
        "Aonik.Platform",
        "Aonik.Finance",
        "Aonik.PersonalFinance",
        "Aonik.Subscriptions",
        "Aonik.Commerce",
        "Aonik.Ai",
        "Aonik.Agents",
    ];

    [Fact]
    public void IConsentReader_Should_OnlyBeConsumedByTheGate()
    {
        var offenders = new List<string>();

        foreach (var assemblyName in ModuleAssemblies)
        {
            var assembly = Assembly.Load(assemblyName);

            foreach (var type in assembly.GetTypes())
            {
                if (PermittedReaderConsumers.Contains(type.FullName))
                {
                    continue;
                }

                var takesReader = type
                    .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .SelectMany(c => c.GetParameters())
                    .Any(p => p.ParameterType == typeof(IConsentReader));

                if (takesReader)
                {
                    offenders.Add(type.FullName!);
                }
            }
        }

        offenders.Should().BeEmpty(
            "IConsentReader returns a boolean a caller can ignore. Authorisation must go through "
            + "IConsentGate, which throws — a consent check the caller can forget to act on is "
            + "decoration, which is the finding Spec 089 §8.1 and Spec 032 both landed on.");
    }

    [Fact]
    public void ConsentGate_Should_NeverReturnABoolean()
    {
        // If a method here ever returns Task<bool>, the gate has quietly become a reader and every
        // guarantee above evaporates: callers would be free to ignore the answer again.
        var returning = typeof(IConsentGate)
            .GetMethods()
            .Where(m => m.ReturnType == typeof(Task<bool>) || m.ReturnType == typeof(bool))
            .Select(m => m.Name)
            .ToList();

        returning.Should().BeEmpty(
            "the gate must throw or proceed; returning a verdict makes it ignorable");
    }

    [Fact]
    public void EveryConsentPurpose_Should_BeRefusableExceptServiceCore()
    {
        // The Children's Code high-privacy-by-default standard: a parent who has not been asked has
        // not agreed. Only service-core is non-refusable, because withdrawing it closes the account.
        var refusable = ConsentPurposes.All
            .Where(p => !ConsentPurposes.NonRefusable.Contains(p))
            .ToList();

        ConsentPurposes.NonRefusable.Should().ContainSingle()
            .And.Contain(ConsentPurposes.ServiceCore);

        refusable.Should().Contain([
            ConsentPurposes.GenerationDisclosure,
            ConsentPurposes.SafetyClassification,
            ConsentPurposes.SharingExternal,
            ConsentPurposes.Voice,
            ConsentPurposes.Improvement,
            ConsentPurposes.Marketing,
        ]);
    }

    [Fact]
    public void LegacyUnverified_Should_NotBeAGrantableMethod()
    {
        // A grant carrying it would authorise on the basis of consent obtained before any
        // verification existed — which is precisely why the legacy rows live in a separate archive
        // the reader never consults.
        ConsentVerificationMethods.Grantable
            .Should().NotContain(ConsentVerificationMethods.LegacyUnverified);
    }

    [Fact]
    public void SelfAuthenticated_Should_NotBeAParentalMethod()
    {
        // It is a real verification, but only of the subject BY the subject. Admitting it as a
        // parental method would let anyone consent for a child by signing in as themselves.
        ConsentVerificationMethods.Parental
            .Should().NotContain(ConsentVerificationMethods.SelfAuthenticated);

        ConsentVerificationMethods.Grantable
            .Should().Contain(ConsentVerificationMethods.SelfAuthenticated,
                "it remains valid for a self-grant");
    }
}
