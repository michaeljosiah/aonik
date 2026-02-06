import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";

import { getPublicBillPaymentDraft, type GuestBillPaymentDraftDetail } from "../../api/orders";
import { draftOrderIdStorageKey } from "./draftIntent";

type SavedCardOption = {
  id: string;
  cardType: "CREDIT CARD" | "DEBIT CARD";
  logoUrl: string;
  last4: string;
  expiry: string;
  styleClass?: string;
};

const savedCards: SavedCardOption[] = [
  {
    id: "card_1",
    cardType: "CREDIT CARD",
    logoUrl: "/images/credit-card-logo.jpg",
    last4: "7568",
    expiry: "12/24"
  },
  {
    id: "card_2",
    cardType: "DEBIT CARD",
    logoUrl: "/images/debit-card-logo.jpg",
    last4: "1982",
    expiry: "05/27",
    styleClass: "card-blue"
  },
  {
    id: "card_3",
    cardType: "DEBIT CARD",
    logoUrl: "/images/debit-card-logo.jpg",
    last4: "4721",
    expiry: "08/26"
  }
];

export const SelectCard = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const orderIdFromQuery = searchParams.get("orderId") ?? "";

  const [orderId, setOrderId] = useState<string>(orderIdFromQuery);
  const [draft, setDraft] = useState<GuestBillPaymentDraftDetail | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [selectedCardId, setSelectedCardId] = useState<string>(savedCards[0].id);

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
              <h3 className="alt mt-4">Contact us</h3>
              <div className="contact-info">
                <img className="contact-info-img" src="/images/illustration-contactus.png" alt="Contact support" />
                <p>Need help choosing a card? Contact support for assistance.</p>
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
                  <Link className="back-left-arrow" to="/payments/selection">
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path
                        d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z"
                        fill="currentColor"
                      />
                    </svg>
                    Back to Select payment method
                  </Link>
                  <h3 className="alt mt-4 mb-3 pt-lg-3">Select card</h3>
                  <p>Choose a saved card or continue with a different card.</p>

                  {!orderId && (
                    <div className="alert alert-warning">No draft order was found. Please return to payment selection.</div>
                  )}
                  {isLoading && <p>Loading draft order...</p>}
                  {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}

                  <div className="d-flex align-items-center mb-2">
                    <h4 className="alt mb-0 me-2">My cards</h4>
                    <Link className="text-underline small" to="/manage-cards">
                      Manage
                    </Link>
                  </div>
                  <div className="slider card-slider">
                    {savedCards.map((card) => (
                      <div className="item" key={card.id}>
                        <div
                          className={`payment-card ${card.styleClass ?? ""}`.trim()}
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
                            <img src={card.logoUrl} alt={card.cardType} />
                            <h5 className="card-title">{card.cardType}</h5>
                          </div>
                          <ul className="card-number">
                            <li>XXXX</li>
                            <li>XXXX</li>
                            <li>XXXX</li>
                            <li>{card.last4}</li>
                          </ul>
                          <span className="valid-info">Valid until {card.expiry}</span>
                        </div>
                      </div>
                    ))}
                  </div>
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
