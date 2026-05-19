using System.Reflection;
using Aonik.Agents;
using Aonik.Ai;
using Aonik.Finance;
using Aonik.Platform;
using Aonik.SharedKernel.Validation;
using FastEndpoints;
using FluentAssertions;
using FluentValidation;

namespace Aonik.Api.Tests.Architecture;

/// <summary>
/// Architecture rule: every FastEndpoints request DTO MUST have a matching
/// FluentValidation Validator&lt;TRequest&gt; registered in one of the module
/// assemblies. Opt-out is permitted only via <see cref="NoValidationAttribute"/>
/// and requires a written justification.
/// </summary>
/// <remarks>
/// Rationale: bad input must be rejected at the API boundary with 400, not
/// permitted to flow into services or EF where it surfaces as 500. This test
/// is the forcing function that keeps coverage at 100% as new endpoints land.
/// </remarks>
public class RequestDtoValidatorCoverageTests
{
    private static readonly Assembly[] ModuleAssemblies =
    [
        typeof(PlatformModule).Assembly,
        typeof(FinanceModule).Assembly,
        typeof(Aonik.PersonalFinance.PersonalFinanceModule).Assembly,
        typeof(AiModule).Assembly,
        typeof(AgentsModule).Assembly,
    ];

    [Fact]
    public void EveryRequestDtoOnAnEndpoint_Should_HaveAValidator_OrBeExplicitlyOptedOut()
    {
        // Discover every TRequest used by a FastEndpoints endpoint across all modules.
        var requestTypes = DiscoverRequestDtos();

        // Discover every TRequest that has a Validator<TRequest> registered.
        var validatedTypes = DiscoverValidatedTypes();

        // A DTO is "covered" if it has a validator OR is explicitly opted out.
        var missing = new List<string>();
        foreach (var requestType in requestTypes)
        {
            if (validatedTypes.Contains(requestType))
            {
                continue;
            }

            if (requestType.GetCustomAttribute<NoValidationAttribute>() is not null)
            {
                continue;
            }

            missing.Add(requestType.FullName ?? requestType.Name);
        }

        if (missing.Count > 0)
        {
            var listing = string.Join(Environment.NewLine, missing.Select(n => "  - " + n));
            var message =
                $"The following request DTOs have no Validator<T> and no [NoValidation] opt-out:{Environment.NewLine}{listing}{Environment.NewLine}{Environment.NewLine}" +
                "Either add a `class FooValidator : Validator<FooRequest>` next to the endpoint, " +
                "or annotate the DTO with [NoValidation(\"why\")] when input is structurally trusted.";

            missing.Should().BeEmpty(message);
        }
    }

    /// <summary>
    /// FastEndpoints base classes whose FIRST generic parameter is the
    /// request DTO. <c>EndpointWithoutRequest&lt;TResponse&gt;</c> is
    /// deliberately excluded — its first arg is the response, not a request.
    /// </summary>
    private static readonly string[] EndpointWithRequestBaseNames =
    [
        "Endpoint`1",            // Endpoint<TRequest>
        "Endpoint`2",            // Endpoint<TRequest, TResponse>
        "Endpoint`3",            // Endpoint<TRequest, TResponse, TMapper>
        "EndpointWithMapper`2",  // EndpointWithMapper<TRequest, TMapper>
    ];

    private static HashSet<Type> DiscoverRequestDtos()
    {
        var requestTypes = new HashSet<Type>();

        foreach (var assembly in ModuleAssemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray(); }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface || !typeof(BaseEndpoint).IsAssignableFrom(type))
                {
                    continue;
                }

                // Walk up the chain looking for the FIRST FastEndpoints base
                // class, and route to the appropriate detection.
                for (var current = type.BaseType; current is not null && current != typeof(object); current = current.BaseType)
                {
                    if (!current.IsGenericType || current.Namespace != "FastEndpoints")
                    {
                        // Could be a user-defined intermediate base class — keep walking.
                        if (!current.IsGenericType || current.GetGenericTypeDefinition()?.Namespace != "FastEndpoints")
                        {
                            continue;
                        }
                    }

                    var def = current.GetGenericTypeDefinition();
                    if (def.Namespace != "FastEndpoints")
                    {
                        continue;
                    }

                    // EndpointWithoutRequest<*> takes the response as its
                    // first argument — those endpoints have no request DTO,
                    // so skip them entirely.
                    if (def.Name.StartsWith("EndpointWithoutRequest", StringComparison.Ordinal))
                    {
                        break;
                    }

                    if (!EndpointWithRequestBaseNames.Contains(def.Name))
                    {
                        continue;
                    }

                    var requestType = current.GetGenericArguments()[0];
                    if (requestType.IsClass && !requestType.IsAbstract)
                    {
                        requestTypes.Add(requestType);
                    }
                    break;
                }
            }
        }

        return requestTypes;
    }

    private static HashSet<Type> DiscoverValidatedTypes()
    {
        var validatedTypes = new HashSet<Type>();

        foreach (var assembly in ModuleAssemblies)
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray(); }

            foreach (var type in types)
            {
                if (type.IsAbstract || type.IsInterface)
                {
                    continue;
                }

                // Look for any base type matching IValidator<TRequest> — covers
                // both FastEndpoints' Validator<T> and FluentValidation's AbstractValidator<T>.
                var validatorInterface = type.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IValidator<>));

                if (validatorInterface is not null)
                {
                    validatedTypes.Add(validatorInterface.GetGenericArguments()[0]);
                }
            }
        }

        return validatedTypes;
    }

    [Fact]
    public void ArchitectureTest_Should_DiscoverAtLeastOneRequestDto()
    {
        // Sanity: if the discovery itself is broken, the main test would
        // pass vacuously. Guard that by asserting we found a reasonable
        // number of request types across the module assemblies.
        var requestTypes = DiscoverRequestDtos();
        requestTypes.Should().HaveCountGreaterThan(50,
            "module assemblies are expected to contain a substantial number of FastEndpoints request DTOs.");
    }
}
