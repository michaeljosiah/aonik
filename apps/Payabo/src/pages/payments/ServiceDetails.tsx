import { useEffect, useMemo, useState, type ChangeEvent, type FormEvent } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";

import {
  getPublicCatalogBillerServiceDetail,
  getPublicCatalogBillerServices,
  validatePublicCatalogServiceFields,
  type CatalogBillerService,
  type CatalogBillerServiceDetail,
  type CatalogServiceField
} from "../../api/catalog";
import { draftIntentStorageKey, draftOrderIdStorageKey, type BillPaymentDraftIntent } from "./draftIntent";

const renderFieldInput = (
  field: CatalogServiceField,
  value: string,
  onChange: (nextValue: string) => void
) => {
  const baseProps = {
    id: field.key,
    name: field.key,
    className: "form-control",
    value,
    placeholder: field.placeholder ?? field.label,
    required: field.required,
    minLength: field.minLength ?? undefined,
    maxLength: field.maxLength ?? undefined,
    onChange: (event: ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) =>
      onChange(event.target.value)
  };

  if (field.options.length > 0) {
    return (
      <select {...baseProps}>
        <option value="">Select {field.label}</option>
        {field.options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    );
  }

  if (field.fieldType.toLowerCase() === "textarea") {
    return <textarea {...baseProps} rows={3} />;
  }

  const normalizedType = field.fieldType.toLowerCase();
  const inputType = ["email", "number", "tel"].includes(normalizedType) ? normalizedType : "text";

  return <input {...baseProps} type={inputType} pattern={field.mask ?? undefined} />;
};

export const ServiceDetails = () => {
  const navigate = useNavigate();
  const { id: billerId } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();

  const requestedServiceId = searchParams.get("serviceId") ?? "";
  const requestedCountryCode = (searchParams.get("countryCode") ?? "").trim().toUpperCase();
  const billerName = searchParams.get("billerName");

  const [services, setServices] = useState<CatalogBillerService[]>([]);
  const [selectedServiceId, setSelectedServiceId] = useState<string>(requestedServiceId);
  const [serviceDetail, setServiceDetail] = useState<CatalogBillerServiceDetail | null>(null);
  const [fieldValues, setFieldValues] = useState<Record<string, string>>({});
  const [requestedAmount, setRequestedAmount] = useState<string>("");

  const [isLoadingServices, setIsLoadingServices] = useState<boolean>(true);
  const [isLoadingDetail, setIsLoadingDetail] = useState<boolean>(false);
  const [isValidating, setIsValidating] = useState<boolean>(false);

  const [servicesError, setServicesError] = useState<string | null>(null);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [validationMessage, setValidationMessage] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const loadServices = async () => {
      if (!billerId) {
        setServicesError("Invalid provider selected.");
        setIsLoadingServices(false);
        return;
      }

      setIsLoadingServices(true);
      setServicesError(null);

      try {
        const result = await getPublicCatalogBillerServices(billerId);
        if (cancelled) {
          return;
        }

        setServices(result);
        setSelectedServiceId((current) => {
          if (current && result.some((service) => service.id === current)) {
            return current;
          }

          return result[0]?.id ?? "";
        });
      } catch {
        if (!cancelled) {
          setServices([]);
          setSelectedServiceId("");
          setServicesError("We couldn't load service options right now.");
        }
      } finally {
        if (!cancelled) {
          setIsLoadingServices(false);
        }
      }
    };

    void loadServices();

    return () => {
      cancelled = true;
    };
  }, [billerId]);

  useEffect(() => {
    let cancelled = false;

    const loadDetail = async () => {
      if (!billerId || !selectedServiceId) {
        setServiceDetail(null);
        setFieldValues({});
        return;
      }

      setIsLoadingDetail(true);
      setDetailError(null);
      setValidationMessage(null);
      setFieldErrors({});

      try {
        const result = await getPublicCatalogBillerServiceDetail(billerId, selectedServiceId);
        if (cancelled) {
          return;
        }

        setServiceDetail(result);
        setFieldValues((current) => {
          const nextValues: Record<string, string> = {};
          result.fields.forEach((field) => {
            nextValues[field.key] = current[field.key] ?? "";
          });
          return nextValues;
        });
      } catch {
        if (!cancelled) {
          setServiceDetail(null);
          setFieldValues({});
          setDetailError("We couldn't load the selected service details.");
        }
      } finally {
        if (!cancelled) {
          setIsLoadingDetail(false);
        }
      }
    };

    void loadDetail();

    return () => {
      cancelled = true;
    };
  }, [billerId, selectedServiceId]);

  const selectedService = useMemo(
    () => services.find((service) => service.id === selectedServiceId) ?? null,
    [services, selectedServiceId]
  );

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!billerId || !serviceDetail) {
      return;
    }

    const nextFieldErrors: Record<string, string> = {};
    serviceDetail.fields.forEach((field) => {
      const value = (fieldValues[field.key] ?? "").trim();

      if (field.required && !value) {
        nextFieldErrors[field.key] = `${field.label} is required.`;
        return;
      }

      if (field.minLength !== null && value.length > 0 && value.length < field.minLength) {
        nextFieldErrors[field.key] = `${field.label} must be at least ${field.minLength} characters.`;
        return;
      }

      if (field.maxLength !== null && value.length > field.maxLength) {
        nextFieldErrors[field.key] = `${field.label} must be at most ${field.maxLength} characters.`;
      }
    });

    setFieldErrors(nextFieldErrors);
    setValidationMessage(null);

    if (Object.keys(nextFieldErrors).length > 0) {
      return;
    }

    let accountHolderName: string | null = null;

    if (serviceDetail.requiresValidation) {
      setIsValidating(true);

      try {
        const result = await validatePublicCatalogServiceFields(billerId, serviceDetail.id, fieldValues);
        if (!result.isValid) {
          setValidationMessage(result.errorMessage ?? "We couldn't validate these details. Please review your values.");
          return;
        }

        accountHolderName = result.accountHolderName;
      } catch {
        setValidationMessage("Validation is temporarily unavailable. Please try again.");
        return;
      } finally {
        setIsValidating(false);
      }
    }

    if (!requestedCountryCode) {
      setValidationMessage("Country context is missing. Please select your provider again.");
      return;
    }

    const normalizedAmount = requestedAmount.trim().length > 0 ? Number(requestedAmount) : null;
    if (normalizedAmount == null || !Number.isFinite(normalizedAmount) || normalizedAmount <= 0) {
      setValidationMessage("Please enter a valid amount before continuing.");
      return;
    }

    if (serviceDetail.minAmount !== null && normalizedAmount < serviceDetail.minAmount) {
      setValidationMessage(`Amount must be at least ${serviceDetail.minAmount}.`);
      return;
    }

    if (serviceDetail.maxAmount !== null && normalizedAmount > serviceDetail.maxAmount) {
      setValidationMessage(`Amount must be at most ${serviceDetail.maxAmount}.`);
      return;
    }

    const intent: BillPaymentDraftIntent = {
      billerId,
      serviceId: serviceDetail.id,
      billerName,
      serviceCode: serviceDetail.code,
      serviceName: serviceDetail.name,
      countryCode: requestedCountryCode,
      currency: serviceDetail.currency,
      serviceFieldValues: fieldValues,
      isValidated: true,
      capturedAt: new Date().toISOString(),
      validationMode: serviceDetail.validation?.validationMode ?? null,
      accountHolderName,
      requestedAmount: normalizedAmount,
      channel: "Payabo"
    };

    sessionStorage.setItem(draftIntentStorageKey, JSON.stringify(intent));
    sessionStorage.removeItem(draftOrderIdStorageKey);
    navigate("/payments/selection", { state: { draftIntent: intent } });
  };

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container">
        <div className="row">
          <div className="col-lg-4 col-xl-3">
            <div className="main-sidebar pt-5">
              <img className="mb-5 mt-2 w-100" src="/images/promo-banner.png" alt="Promo Banner" />
              <h3 className="alt mt-4">Need help?</h3>
              <div className="contact-info">
                <img className="contact-info-img" src="/images/illustration-contactus.png" alt="Contact support" />
                <p>If you are unsure about the fields, contact support before proceeding.</p>
                <ul>
                  <li>
                    <a href="tel:+44123456789">+44 123 456 789</a>
                  </li>
                  <li>
                    <a href="mailto:mail@mybillafrica.com">mail@mybillafrica.com</a>
                  </li>
                </ul>
              </div>
            </div>
          </div>

          <div className="col-lg-8 col-xl-9">
            <div className="wrapper-content">
              <div className="row align-items-end mb-3">
                <div className="col-md-10 col-xl-8">
                  <Link className="back-left-arrow" to="/payments/providers">
                    Back to providers
                  </Link>
                  <h3 className="alt mt-4 mb-3">Service details</h3>
                  <p>Complete the required details for the selected bill service.</p>
                </div>
              </div>

              <div className="form-group mb-4">
                <label htmlFor="serviceType">Service type</label>
                <select
                  id="serviceType"
                  className="form-control"
                  value={selectedServiceId}
                  onChange={(event) => setSelectedServiceId(event.target.value)}
                  disabled={isLoadingServices || services.length === 0}
                >
                  {isLoadingServices && <option value="">Loading services...</option>}
                  {!isLoadingServices && services.length === 0 && <option value="">No services available</option>}
                  {services.map((service) => (
                    <option key={service.id} value={service.id}>
                      {service.name}
                    </option>
                  ))}
                </select>
                {servicesError && <p className="text-danger small mt-2 mb-0">{servicesError}</p>}
              </div>

              {selectedService && (
                <div className="card p-3 mb-4">
                  <h5 className="mb-2">{selectedService.name}</h5>
                  <p className="mb-1">Code: {selectedService.code}</p>
                  <p className="mb-1">Currency: {selectedService.currency}</p>
                  <p className="mb-0">
                    Amount range: {selectedService.minAmount ?? 0} - {selectedService.maxAmount ?? "No limit"}
                  </p>
                </div>
              )}

              {isLoadingDetail && <p>Loading service form...</p>}
              {detailError && <p className="text-danger mb-3">{detailError}</p>}

              {serviceDetail && !isLoadingDetail && (
                <form onSubmit={handleSubmit}>
                  <div className="row">
                    <div className="col-md-6 mb-3">
                      <label htmlFor="requestedAmount">Amount ({serviceDetail.currency})</label>
                      <input
                        id="requestedAmount"
                        type="number"
                        className="form-control"
                        min={serviceDetail.minAmount ?? undefined}
                        max={serviceDetail.maxAmount ?? undefined}
                        value={requestedAmount}
                        onChange={(event) => setRequestedAmount(event.target.value)}
                        placeholder="Enter amount"
                      />
                    </div>
                  </div>

                  <div className="row">
                    {serviceDetail.fields.map((field) => (
                      <div key={field.key} className="col-md-6 mb-3">
                        <label htmlFor={field.key}>{field.label}</label>
                        {renderFieldInput(field, fieldValues[field.key] ?? "", (nextValue) => {
                          setFieldValues((current) => ({
                            ...current,
                            [field.key]: nextValue
                          }));
                        })}
                        {fieldErrors[field.key] && <p className="text-danger small mt-1 mb-0">{fieldErrors[field.key]}</p>}
                      </div>
                    ))}
                  </div>

                  {validationMessage && <p className="text-danger mb-3">{validationMessage}</p>}

                  <div className="d-flex gap-2 mt-3">
                    <button type="submit" className="btn btn-primary" disabled={isValidating}>
                      {isValidating ? "VALIDATING..." : "CONTINUE"}
                    </button>
                    <Link className="btn btn-secondary" to="/payments/providers">
                      CANCEL
                    </Link>
                  </div>
                </form>
              )}
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
