import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";
import { getPaymentInstrumentsForUser, type PaymentInstrument } from "../../api/paymentInstruments";
import { getPublicBillPaymentDraft, type GuestBillPaymentDraftDetail } from "../../api/orders";
import { draftOrderIdStorageKey } from "./draftIntent";

const cardTypeLabel = (type: PaymentInstrument["type"]) => (type === "credit" ? "CREDIT CARD" : "DEBIT CARD");

export const SelectCard = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const { user } = useAuth();
  const orderIdFromQuery = searchParams.get("orderId") ?? "";

  const [orderId, setOrderId] = useState<string>(orderIdFromQuery);
  const [draft, setDraft] = useState<GuestBillPaymentDraftDetail | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [savedCards, setSavedCards] = useState<PaymentInstrument[]>([]);
  const [selectedCardId, setSelectedCardId] = useState<string>("");

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
        setSelectedCardId(cards[0]?.id ?? "");
      }
    };

    void loadCards();
    return () => {
      cancelled = true;
    };
  }, [user?.id]);

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

  const amountLabel = useMemo(() => {
    if (!draft || draft.requestedAmount == null) {
      return "Not set";
    }

    try {
      return new Intl.NumberFormat("en-GB", {
        style: "currency",
        currency: draft.currency,
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
      }).format(draft.requestedAmount);
    } catch {
      return `${draft.requestedAmount} ${draft.currency}`;
    }
  }, [draft]);

  const countryName = useMemo(() => {
    if (!draft?.countryCode) {
      return "Not selected";
    }

    try {
      const displayNames = new Intl.DisplayNames(["en"], { type: "region" });
      return displayNames.of(draft.countryCode.toUpperCase()) ?? draft.countryCode;
    } catch {
      return draft.countryCode;
    }
  }, [draft?.countryCode]);

  const serviceDetailsLines = useMemo(() => {
    if (!draft) {
      return [] as string[];
    }

    const lines = [draft.serviceName];
    if (draft.accountHolderName) {
      lines.push(draft.accountHolderName);
    }

    fieldPairs.slice(0, 2).forEach((pair) => {
      lines.push(`${pair.key}: ${pair.value}`);
    });

    lines.push(amountLabel);
    return lines;
  }, [amountLabel, draft, fieldPairs]);

  const openCardCheckout = (savedCardId?: string) => {
    if (!orderId) {
      return;
    }

    const params = new URLSearchParams({ orderId });
    if (savedCardId) {
      params.set("savedCardId", savedCardId);
    }

    navigate(`/payments/card-checkout?${params.toString()}`);
  };

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
                    <Link className="text-underline small" to="/payments/selection">
                      Edit
                    </Link>
                  </div>
                  <div className="d-flex align-items-center">
                    <img
                      className="rounded me-3"
                      src={draft ? `/images/flags/${draft.countryCode.toLowerCase()}.svg` : "/images/flags/gb.svg"}
                      alt={draft?.countryCode ?? "Country"}
                    />
                    <h4 className="alt fw-normal mb-0">{countryName}</h4>
                  </div>
                </div>
                <div className="list-group-item">
                  <div className="d-flex justify-content-between mb-2">
                    <h4 className="alt mb-0">Biller</h4>
                    <Link className="text-underline small" to="/payments/selection">
                      Edit
                    </Link>
                  </div>
                  <div className="d-flex align-items-center">
                    <img className="me-3" src="/images/product-img-04.png" alt={draft?.billerName ?? "Biller"} />
                    <h4 className="alt fw-normal mb-0">{draft?.billerName ?? "Selected provider"}</h4>
                  </div>
                </div>
                <div className="list-group-item">
                  <div className="d-flex justify-content-between mb-2">
                    <h4 className="alt mb-0">Service details</h4>
                    <Link className="text-underline small" to="/payments/selection">
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
            </div>
          </div>
          <div className="col-lg-8 col-xl-9">
            <div className="wrapper-content">
              <div className="row justify-content-center mb-md-2">
                <div className="col-md-11 col-xl-10 col-xxl-8">
                  <Link className="back-left-arrow" to="/payments/selection">Back to Select payment method</Link>
                  <h3 className="alt mt-4 mb-3 pt-lg-3">Select card</h3>
                  <p>Choose a saved card or continue with a different card.</p>

                  {!orderId && <div className="alert alert-warning">No draft order was found. Please return to payment selection.</div>}
                  {isLoading && <p>Loading draft order...</p>}
                  {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}

                  <div className="d-flex align-items-center mb-2">
                    <h4 className="alt mb-0 me-2">My cards</h4>
                    <Link className="text-underline small" to="/manage-cards">
                      Manage
                    </Link>
                  </div>

                  {savedCards.length > 0 ? (
                    <div className="slider card-slider">
                      {savedCards.map((card) => (
                        <div className="item" key={card.id}>
                          <div
                            className="payment-card"
                            role="button"
                            tabIndex={0}
                            style={selectedCardId === card.id ? { boxShadow: "0 0 0 3px #f37920" } : undefined}
                            onClick={() => {
                              setSelectedCardId(card.id);
                              openCardCheckout(card.id);
                            }}
                            onKeyDown={(event) => {
                              if (event.key === "Enter" || event.key === " ") {
                                event.preventDefault();
                                setSelectedCardId(card.id);
                                openCardCheckout(card.id);
                              }
                            }}
                          >
                            <div className="d-flex align-items-center">
                              <img src="/images/credit-card-logo.jpg" alt={card.brand} />
                              <h5 className="card-title">{cardTypeLabel(card.type)}</h5>
                            </div>
                            <ul className="card-number">
                              <li>XXXX</li>
                              <li>XXXX</li>
                              <li>XXXX</li>
                              <li>{card.last4}</li>
                            </ul>
                            <span className="valid-info">Valid until {String(card.expiryMonth).padStart(2, "0")}/{card.expiryYear}</span>
                          </div>
                        </div>
                      ))}
                    </div>
                  ) : (
                    <div className="alert alert-info">No saved cards found. Continue with a new card.</div>
                  )}

                  <div className="pt-5">
                    <button type="button" className="btn btn-secondary btn-md" onClick={() => openCardCheckout()}>
                      USE ANOTHER CARD
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
