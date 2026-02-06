import { useEffect, useMemo, useState, type MouseEvent } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";

import {
  createPublicBillPaymentDraft,
  getPublicBillPaymentDraft,
  type GuestBillPaymentDraftDetail
} from "../../api/orders";
import { draftIntentStorageKey, draftOrderIdStorageKey, type BillPaymentDraftIntent } from "./draftIntent";

type SelectionLocationState = {
  draftIntent?: BillPaymentDraftIntent;
};

export const PaymentSelection = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const state = location.state as SelectionLocationState | null;

  const [draftIntent, setDraftIntent] = useState<BillPaymentDraftIntent | null>(state?.draftIntent ?? null);
  const [savedDraft, setSavedDraft] = useState<GuestBillPaymentDraftDetail | null>(null);
  const [isLoadingSavedDraft, setIsLoadingSavedDraft] = useState<boolean>(false);
  const [isSaving, setIsSaving] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [savedOrderId, setSavedOrderId] = useState<string | null>(null);
  const [pendingMethod, setPendingMethod] = useState<"card" | "friend" | null>(null);

  useEffect(() => {
    const storedOrderId = sessionStorage.getItem(draftOrderIdStorageKey);
    if (storedOrderId) {
      setSavedOrderId(storedOrderId);
    }
  }, []);

  useEffect(() => {
    let cancelled = false;

    const loadSavedDraft = async () => {
      if (!savedOrderId || draftIntent) {
        setSavedDraft(null);
        return;
      }

      setIsLoadingSavedDraft(true);
      setErrorMessage(null);

      try {
        const response = await getPublicBillPaymentDraft(savedOrderId);
        if (!cancelled) {
          setSavedDraft(response);
        }
      } catch {
        if (!cancelled) {
          setSavedDraft(null);
        }
      } finally {
        if (!cancelled) {
          setIsLoadingSavedDraft(false);
        }
      }
    };

    void loadSavedDraft();

    return () => {
      cancelled = true;
    };
  }, [draftIntent, savedOrderId]);

  useEffect(() => {
    if (draftIntent) {
      return;
    }

    const raw = sessionStorage.getItem(draftIntentStorageKey);
    if (!raw) {
      return;
    }

    try {
      setDraftIntent(JSON.parse(raw) as BillPaymentDraftIntent);
    } catch {
      setDraftIntent(null);
    }
  }, [draftIntent]);

  const activeDraft = draftIntent ?? savedDraft;

  const orderedFieldPairs = useMemo(() => {
    if (!activeDraft) {
      return [] as Array<{ key: string; value: string }>;
    }

    return Object.entries(activeDraft.serviceFieldValues).map(([key, value]) => ({ key, value }));
  }, [activeDraft]);

  const formattedAmount = useMemo(() => {
    if (!activeDraft || activeDraft.requestedAmount == null) {
      return "Not set";
    }

    try {
      return new Intl.NumberFormat("en-GB", {
        style: "currency",
        currency: activeDraft.currency,
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      }).format(activeDraft.requestedAmount);
    } catch {
      return `${activeDraft.requestedAmount} ${activeDraft.currency}`;
    }
  }, [activeDraft]);

  const countryName = useMemo(() => {
    if (!activeDraft?.countryCode) {
      return "Not selected";
    }

    try {
      const displayNames = new Intl.DisplayNames(["en"], { type: "region" });
      return displayNames.of(activeDraft.countryCode.toUpperCase()) ?? activeDraft.countryCode;
    } catch {
      return activeDraft.countryCode;
    }
  }, [activeDraft?.countryCode]);

  const serviceDetailsLines = useMemo(() => {
    if (!activeDraft) {
      return [] as string[];
    }

    const lines = [activeDraft.serviceName];
    if (activeDraft.accountHolderName) {
      lines.push(activeDraft.accountHolderName);
    }

    orderedFieldPairs.slice(0, 2).forEach((pair) => {
      lines.push(`${pair.key}: ${pair.value}`);
    });

    lines.push(formattedAmount);
    return lines;
  }, [activeDraft, formattedAmount, orderedFieldPairs]);

  const backToServiceDetailsPath = useMemo(() => {
    if (!activeDraft) {
      return "/payments/providers";
    }

    const params = new URLSearchParams({
      countryCode: activeDraft.countryCode,
      serviceId: activeDraft.serviceId
    });

    if (activeDraft.billerName) {
      params.set("billerName", activeDraft.billerName);
    }

    return `/payments/service/${activeDraft.billerId}?${params.toString()}`;
  }, [activeDraft]);

  const ensureSavedDraft = async () => {
    if (savedOrderId) {
      return savedOrderId;
    }

    if (!draftIntent) {
      setErrorMessage("No validated service data found. Please complete service details first.");
      return null;
    }

    setIsSaving(true);
    setErrorMessage(null);

    try {
      const result = await createPublicBillPaymentDraft(draftIntent);
      setSavedOrderId(result.orderId);
      sessionStorage.setItem(draftOrderIdStorageKey, result.orderId);
      return result.orderId;
    } catch {
      setErrorMessage("Unable to save draft right now. Please try again.");
      return null;
    } finally {
      setIsSaving(false);
    }
  };

  const handleSelectPaymentMethod = (method: "card" | "friend") => {
    return async (event: MouseEvent<HTMLAnchorElement>) => {
      event.preventDefault();

      if (isSaving) {
        return;
      }

      setPendingMethod(method);

      const orderId = await ensureSavedDraft();
      if (!orderId) {
        setPendingMethod(null);
        return;
      }

      if (method === "card") {
        navigate(`/payments/select-card?orderId=${encodeURIComponent(orderId)}`);
        return;
      }

      navigate(`/payments/select-friend?orderId=${encodeURIComponent(orderId)}`);
    };
  };

  if (!activeDraft && !isLoadingSavedDraft) {
    return (
      <main className="main-wrapper overflow-hidden">
        <div className="container">
          <div className="row">
            <div className="col-lg-8 col-xl-9">
              <div className="wrapper-content pt-5">
                <h3 className="alt mb-3">Payment details</h3>
                <div className="alert alert-warning mt-3">
                  We could not find validated service details. Please return to service details.
                </div>
                <Link className="btn btn-secondary" to="/payments/providers">
                  BACK TO PROVIDERS
                </Link>
              </div>
            </div>
          </div>
        </div>
      </main>
    );
  }

  if (!activeDraft) {
    return (
      <main className="main-wrapper overflow-hidden">
        <div className="container">
          <div className="row">
            <div className="col-lg-8 col-xl-9">
              <div className="wrapper-content pt-5">
                <h3 className="alt mb-3">Payment details</h3>
                <p>Loading your draft details...</p>
              </div>
            </div>
          </div>
        </div>
      </main>
    );
  }

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container">
        <div className="row">
          <div className="col-lg-4 col-xl-3">
            <div className="main-sidebar pt-5">
              <h4>Order summary</h4>
              <div className="list-group summery-sidebar pb-4">
                <div className="list-group-item">
                  <div className="d-flex justify-content-between mb-2">
                    <h4 className="alt mb-0">Destination country</h4>
                    <Link className="text-underline small" to={`/payments/providers?countryCode=${encodeURIComponent(activeDraft.countryCode)}`}>
                      Edit
                    </Link>
                  </div>
                  <div className="d-flex align-items-center">
                    <img
                      className="rounded me-3"
                      src={`/images/flags/${activeDraft.countryCode.toLowerCase()}.svg`}
                      alt={activeDraft.countryCode}
                    />
                    <h4 className="alt fw-normal mb-0">{countryName}</h4>
                  </div>
                </div>
                <div className="list-group-item">
                  <div className="d-flex justify-content-between mb-2">
                    <h4 className="alt mb-0">Biller</h4>
                    <Link className="text-underline small" to={`/payments/providers?countryCode=${encodeURIComponent(activeDraft.countryCode)}`}>
                      Edit
                    </Link>
                  </div>
                  <div className="d-flex align-items-center">
                    <img className="me-3" src="/images/product-img-04.png" alt={activeDraft.billerName ?? "Biller"} />
                    <h4 className="alt fw-normal mb-0">{activeDraft.billerName ?? "Selected provider"}</h4>
                  </div>
                </div>
                <div className="list-group-item">
                  <div className="d-flex justify-content-between mb-2">
                    <h4 className="alt mb-0">Service details</h4>
                    <Link className="text-underline small" to={backToServiceDetailsPath}>
                      Edit
                    </Link>
                  </div>
                  <div className="d-flex align-items-center">
                    <p className="mb-0">
                      {serviceDetailsLines.map((line) => (
                        <span key={line}>
                          {line}
                          <br />
                        </span>
                      ))}
                    </p>
                  </div>
                </div>
              </div>
              <h3 className="alt mt-4">Contact us</h3>
              <div className="contact-info">
                <img className="contact-info-img" src="/images/illustration-contactus.png" alt="Contact support" />
                <p>Need help with payment options? Our support team is ready to assist.</p>
                <h6>Support channels</h6>
                <ul>
                  <li>
                    <a href="tel:+44123456789">+44 123 456 789</a>
                  </li>
                  <li>
                    <a href="mailto:mail@mybillafrica.com">mail@mybillafrica.com</a>
                  </li>
                  <li>All days: 8AM - 5PM</li>
                </ul>
              </div>
            </div>
          </div>
          <div className="col-lg-8 col-xl-9">
            <div className="wrapper-content">
              <div className="row justify-content-center mb-md-2">
                <div className="col-md-11 col-xl-10 col-xxl-8">
                  <Link className="back-left-arrow" to={backToServiceDetailsPath}>
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path
                        d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z"
                        fill="currentColor"
                      />
                    </svg>
                    Back to Enter details
                  </Link>
                  <h3 className="alt mt-4 mb-3 pt-lg-3">Service details</h3>
                  <p>Select your payment method.</p>

                  <ul className="group-list">
                    <li>
                      <a href="#" onClick={handleSelectPaymentMethod("card")} aria-disabled={isSaving}>
                        <div className="d-flex align-items-center">
                          <div className="me-3">
                            <img src="/images/credit-card-logo.jpg" alt="Card" width={36} height={27} />
                          </div>
                          <div>
                            <h4 className="alt">Pay with debit or credit card</h4>
                            <p className="mb-0">Use your saved card or enter a new card at checkout.</p>
                          </div>
                        </div>
                      </a>
                    </li>
                    <li>
                      <a href="#" onClick={handleSelectPaymentMethod("friend")} aria-disabled={isSaving}>
                        <div className="d-flex align-items-center">
                          <div className="me-3">
                            <img src="/images/profile-pic.png" alt="Friend payment" width={36} height={34} />
                          </div>
                          <div>
                            <h4 className="alt">Request help with payment</h4>
                            <p className="mb-0">Ask your friends and family for help with paying a bill.</p>
                          </div>
                        </div>
                      </a>
                    </li>
                  </ul>

                  {savedOrderId && (
                    <div className="alert alert-success mt-3 mb-0">
                      Draft ready. Order ID: <strong>{savedOrderId}</strong>
                    </div>
                  )}

                  {isSaving && (
                    <div className="alert alert-info mt-3 mb-0">
                      Preparing your payment method{pendingMethod === "card" ? " (card)" : pendingMethod === "friend" ? " (friend)" : ""}...
                    </div>
                  )}

                  {errorMessage && <div className="alert alert-danger mt-3 mb-0">{errorMessage}</div>}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
