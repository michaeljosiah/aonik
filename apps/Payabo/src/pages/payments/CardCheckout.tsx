import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { getSelectedOriginCountry } from "../../app/originCountry";
import { getPublicBillPaymentDraft, type GuestBillPaymentDraftDetail } from "../../api/orders";
import { createPublicPaymentIntent, type PublicPaymentIntent } from "../../api/payments";
import { useAuth } from "../../app/auth/AuthContext";
import { getPaymentInstrumentsForUser, type PaymentInstrument } from "../../api/paymentInstruments";
import { draftOrderIdStorageKey } from "./draftIntent";
import { saveCheckoutAttemptState } from "./paymentFlowState";
import { upsertPaymentHistory } from "./paymentHistory";

export const CardCheckout = () => {
  const [searchParams] = useSearchParams();
  const { user } = useAuth();
  const orderIdFromQuery = searchParams.get("orderId") ?? "";
  const savedCardIdFromQuery = searchParams.get("savedCardId") ?? "";
  const [orderId, setOrderId] = useState<string>(orderIdFromQuery);
  const savedCardId = savedCardIdFromQuery;
  const [draft, setDraft] = useState<GuestBillPaymentDraftDetail | null>(null);
  const [originCountry, setOriginCountry] = useState(() => getSelectedOriginCountry());
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isCreatingIntent, setIsCreatingIntent] = useState<boolean>(false);
  const [paymentIntent, setPaymentIntent] = useState<PublicPaymentIntent | null>(null);
  const [savedCards, setSavedCards] = useState<PaymentInstrument[]>([]);

  useEffect(() => {
    const syncOriginCountry = () => {
      setOriginCountry(getSelectedOriginCountry());
    };

    window.addEventListener("payabo:origin-country-changed", syncOriginCountry as EventListener);
    window.addEventListener("storage", syncOriginCountry);

    return () => {
      window.removeEventListener("payabo:origin-country-changed", syncOriginCountry as EventListener);
      window.removeEventListener("storage", syncOriginCountry);
    };
  }, []);

  useEffect(() => {
    if (orderId) {
      return;
    }

    const storedOrderId = sessionStorage.getItem(draftOrderIdStorageKey);
    if (storedOrderId) {
      setOrderId(storedOrderId);
    }
  }, [orderId]);

  useEffect(() => {
    let cancelled = false;

    const loadDraft = async () => {
      if (!orderId) {
        return;
      }

      setIsLoading(true);
      setErrorMessage(null);
      setPaymentIntent(null);

      try {
        const response = await getPublicBillPaymentDraft(orderId);
        if (!cancelled) {
          setDraft(response);
        }
      } catch {
        if (!cancelled) {
          setDraft(null);
          setErrorMessage("We could not load your draft order. Please return to payment selection.");
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    };

    void loadDraft();

    return () => {
      cancelled = true;
    };
  }, [orderId]);

  const fieldPairs = useMemo(() => {
    if (!draft) {
      return [] as Array<{ key: string; value: string }>;
    }

    return Object.entries(draft.serviceFieldValues).map(([key, value]) => ({ key, value }));
  }, [draft]);

  useEffect(() => {
    if (!user?.id) {
      setSavedCards([]);
      return;
    }

    let cancelled = false;
    const loadCards = async () => {
      const cards = await getPaymentInstrumentsForUser(user.id);
      if (!cancelled) {
        setSavedCards(cards);
      }
    };

    void loadCards();
    return () => {
      cancelled = true;
    };
  }, [user?.id]);

  const selectedCard = useMemo(() => {
    return savedCards.find((card) => card.id === savedCardId) ?? null;
  }, [savedCardId, savedCards]);

  const amount = draft?.requestedAmount ?? 0;
  const fees = amount > 0 ? 1.99 : 0;
  const otherTaxes = 0;
  const total = amount + fees + otherTaxes;

  const formattedAmount = useMemo(() => {
    if (!draft) {
      return "0.00";
    }

    try {
      return new Intl.NumberFormat("en-GB", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      }).format(amount);
    } catch {
      return amount.toFixed(2);
    }
  }, [amount, draft]);

  const formatMoneyWithCurrency = (value: number, currency: string) => {
    try {
      const formatted = new Intl.NumberFormat("en-GB", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      }).format(value);
      return `${currency} ${formatted}`;
    } catch {
      return `${currency} ${value.toFixed(2)}`;
    }
  };

  const serviceDetailsText = useMemo(() => {
    if (!draft) {
      return "Service details";
    }

    const topField = fieldPairs[0];
    if (!topField) {
      return draft.serviceCode;
    }

    return `${topField.key} #${topField.value}`;
  }, [draft, fieldPairs]);

  const backToPaymentDetailsPath = useMemo(() => {
    if (!orderId) {
      return "/payments/selection";
    }

    const params = new URLSearchParams({ orderId });
    if (savedCardId) {
      params.set("savedCardId", savedCardId);
    }

    return `/payments/select-card?${params.toString()}`;
  }, [orderId, savedCardId]);

  const backToServiceDetailsPath = useMemo(() => {
    if (!draft) {
      return "/payments/selection";
    }

    const params = new URLSearchParams({
      countryCode: draft.countryCode,
      serviceId: draft.serviceId
    });

    if (draft.billerName) {
      params.set("billerName", draft.billerName);
    }

    return `/payments/service/${draft.billerId}?${params.toString()}`;
  }, [draft]);

  const handleConfirmPayment = async () => {
    if (!draft) {
      return;
    }

    setErrorMessage(null);
    setIsCreatingIntent(true);

    try {
      const result = await createPublicPaymentIntent({
        orderId: draft.orderId,
        provider: "Stripe",
        paymentMethodType: "Card",
        returnUrl: `${window.location.origin}/payments/return?orderId=${encodeURIComponent(draft.orderId)}&result=success`,
        cancelUrl: `${window.location.origin}/payments/return?orderId=${encodeURIComponent(draft.orderId)}&result=cancelled`
      });

      setPaymentIntent(result);
      saveCheckoutAttemptState({
        orderId: result.orderId,
        paymentIntentId: result.paymentIntentId,
        providerReference: result.providerReference,
        createdAt: result.createdAt
      });

      if (user?.id) {
        upsertPaymentHistory({
          userId: user.id,
          orderId: result.orderId,
          paymentIntentId: result.paymentIntentId,
          providerReference: result.providerReference,
          status: result.status,
          orderStatus: draft.status,
          amount: result.amount,
          currency: result.currency,
          serviceName: draft.serviceName,
          billerName: draft.billerName,
          createdAt: result.createdAt
        });
      }

      if (result.checkoutUrl) {
        window.location.assign(result.checkoutUrl);
        return;
      }

      window.location.assign(
        `/payments/return?orderId=${encodeURIComponent(result.orderId)}&paymentIntentId=${encodeURIComponent(result.paymentIntentId)}&providerReference=${encodeURIComponent(result.providerReference)}&result=pending`
      );
    } catch {
      setErrorMessage("Unable to initialize payment provider. Please try again.");
    } finally {
      setIsCreatingIntent(false);
    }
  };

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container">
        <div className="row">
          <div className="col-lg-4 col-xl-3">
            <div className="main-sidebar pt-5">
              <h3 className="alt mt-4">Contact us</h3>
              <div className="contact-info">
                <img className="contact-info-img" src="/images/illustration-contactus.png" alt="Contact support" />
                <p>Need help with checkout? Our support team is available to assist.</p>
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
                  <Link className="back-left-arrow" to={backToPaymentDetailsPath}>
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path
                        d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z"
                        fill="currentColor"
                      />
                    </svg>{" "}
                    Back to Payment details
                  </Link>
                  <h3 className="alt mt-4 mb-3 pt-lg-3">Review your order</h3>
                  <p>Please review your service and card details before confirming payment.</p>
                  <p className="mb-3 text-gray">
                    Paying from: <strong>{originCountry.name}</strong> ({originCountry.currency})
                  </p>

                  {!orderId && (
                    <div className="alert alert-warning">No draft order was found. Please complete service details first.</div>
                  )}
                  {isLoading && <p>Loading draft order...</p>}
                  {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}

                  {draft && (
                    <div className="form-tbox">
                      <div className="d-flex align-items-center mb-3">
                        <h4 className="text-gray mb-0 me-3">Service details</h4>
                        <Link className="text-underline small" to={backToServiceDetailsPath}>
                          Edit
                        </Link>
                      </div>
                      <div className="table-responsive mb-5">
                        <table className="table table-card">
                          <thead>
                            <tr>
                              <th className="col pe-4 py-2">BILLER</th>
                              <th className="col py-2">SERVICE</th>
                              <th className="col py-2 text-end">AMOUNT</th>
                            </tr>
                          </thead>
                          <tbody>
                            <tr>
                              <td>
                                <div className="d-flex">
                                  <div className="img-td">
                                    <img src="/images/product-img-04.png" alt={draft.billerName ?? "Biller"} />
                                  </div>
                                  <div className="pt-1">
                                    <strong className="heading-td">{draft.billerName ?? "Biller"}</strong>
                                  </div>
                                </div>
                              </td>
                              <td>
                                <strong className="heading-td">{draft.serviceName}</strong>
                                <span className="info-td d-block">{serviceDetailsText}</span>
                              </td>
                              <td className="text-end">
                                <strong className="heading-td">{formattedAmount}</strong>
                                <span className="info-td d-block">{draft.currency}</span>
                              </td>
                            </tr>
                          </tbody>
                        </table>
                      </div>

                      <div className="d-flex align-items-center mb-3">
                        <h4 className="text-gray mb-0 me-3">Payment details</h4>
                        <Link className="text-underline small" to={backToPaymentDetailsPath}>
                          Edit
                        </Link>
                      </div>
                      <div className="table-responsive">
                        <table className="table table-card">
                          <thead>
                            <tr>
                              <th className="col pe-4 py-2">CARD</th>
                            </tr>
                          </thead>
                          <tbody>
                            <tr>
                              <td>
                                {selectedCard ? (
                                  <div className="d-flex align-items-center">
                                    <div className="img-td">
                                      <img src="/images/credit-card-logo.jpg" alt={selectedCard.brand} />
                                    </div>
                                    <div>
                                      <strong className="heading-td">{selectedCard.type === "credit" ? "Credit card" : "Debit card"}</strong>{" "}
                                      <span className="dot-info">Ending in {selectedCard.last4}</span>
                                      <span className="info-td text-gray d-block">
                                        Valid until {String(selectedCard.expiryMonth).padStart(2, "0")}/{selectedCard.expiryYear}
                                      </span>
                                    </div>
                                  </div>
                                ) : (
                                  <span className="text-muted">No saved card selected. A new card will be used at provider checkout.</span>
                                )}
                              </td>
                            </tr>
                          </tbody>
                        </table>
                      </div>

                      <div className="row align-items-end mt-2">
                        <div className="col-md-6 order-md-1">
                          <div className="payment-box">
                            <div className="payment-body">
                              <div className="d-flex justify-content-between py-1">
                                <span className="text-gray">Rate {originCountry.currency}:{draft.currency}</span>
                                <span className="text-gray">1.0000</span>
                              </div>
                              <div className="d-flex justify-content-between py-1">
                                <strong>Sub-total</strong>
                                <strong>{formatMoneyWithCurrency(amount, originCountry.currency)}</strong>
                              </div>
                              <div className="d-flex justify-content-between py-1">
                                <span className="text-gray">Fees</span>
                                <span className="text-gray">{formatMoneyWithCurrency(fees, originCountry.currency)}</span>
                              </div>
                              <div className="d-flex justify-content-between py-1">
                                <span className="text-gray">Other taxes</span>
                                <span className="text-gray">{formatMoneyWithCurrency(otherTaxes, originCountry.currency)}</span>
                              </div>
                            </div>
                            <div className="payment-footer d-flex justify-content-between">
                              <strong>Total</strong>
                              <strong className="text-primary">{formatMoneyWithCurrency(total, originCountry.currency)}</strong>
                            </div>
                            <button className="w-100 btn btn-primary btn-lg" type="button" disabled={isCreatingIntent} onClick={handleConfirmPayment}>
                              {isCreatingIntent ? "INITIALIZING..." : "CONFIRM PAYMENT"}
                            </button>
                          </div>
                        </div>
                        <div className="col-md-6 text-center text-md-start">
                          <img className="mt-4" src="/images/powered-by-stripe.png" alt="Powered by Stripe" />
                        </div>
                      </div>

                      {paymentIntent && !paymentIntent.checkoutUrl && (
                        <div className="alert alert-success mt-3 mb-0">
                          Payment intent initialized with {paymentIntent.provider}. Reference: {paymentIntent.providerReference}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
