import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";

import { getPublicBillPaymentDraft, type GuestBillPaymentDraftDetail } from "../../api/orders";
import { draftOrderIdStorageKey } from "./draftIntent";
import { loadFriendMessage, loadFriendSelection, saveFriendMessage } from "./friendFlowStorage";

export const FriendMessage = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const orderIdFromQuery = searchParams.get("orderId") ?? "";

  const [orderId, setOrderId] = useState(orderIdFromQuery);
  const [draft, setDraft] = useState<GuestBillPaymentDraftDetail | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const savedMessage = useMemo(() => loadFriendMessage(), []);
  const [message, setMessage] = useState(() => savedMessage?.message ?? "");

  const selectedFriend = useMemo(() => loadFriendSelection(), []);

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

  const serviceDetailsLines = useMemo(() => {
    if (!draft) {
      return [] as string[];
    }

    const entries = Object.entries(draft.serviceFieldValues).map(([key, value]) => `${key}: ${value}`);
    return [draft.serviceName, draft.accountHolderName, ...entries.slice(0, 2), amountLabel].filter(Boolean) as string[];
  }, [amountLabel, draft]);

  const handleSkip = () => {
    saveFriendMessage({ message: "", skipped: true });
    const params = new URLSearchParams({ orderId });
    navigate(`/payments/friend-checkout?${params.toString()}`);
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    saveFriendMessage({ message, skipped: false });
    const params = new URLSearchParams({ orderId });
    navigate(`/payments/friend-checkout?${params.toString()}`);
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
                        <span className="d-block" key={line}>
                          {line}
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
                  <Link className="back-left-arrow" to={`/payments/select-friend?orderId=${encodeURIComponent(orderId)}`}>
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z" fill="currentColor" />
                    </svg>{" "}
                    Back to Friend details
                  </Link>
                  <h3 className="alt mt-4 mb-3 pt-lg-3">
                    Send message to {selectedFriend ? `${selectedFriend.firstName} ${selectedFriend.lastName}` : "your friend"}
                  </h3>
                  <p>You can send a message to your friend before requesting help with the payment.</p>
                  {errorMessage && <div className="alert alert-warning mt-3">{errorMessage}</div>}
                  {isLoading && <div className="alert alert-info mt-3">Loading your order summary...</div>}
                  <div className="form-tbox">
                    <form onSubmit={handleSubmit}>
                      <div className="form-group">
                        <label htmlFor="message">Message</label>
                        <textarea
                          className="form-control"
                          id="message"
                          rows={5}
                          placeholder="Enter the message to send to your friend"
                          value={message}
                          onChange={(event) => setMessage(event.target.value)}
                        />
                      </div>
                      <div className="row align-items-end pt-3">
                        <div className="col">
                          <button type="button" className="text-underline small btn btn-link p-0" onClick={handleSkip}>
                            Skip message
                          </button>
                        </div>
                        <div className="col-auto">
                          <button className="btn btn-primary btn-md" type="submit" disabled={!orderId}>
                            SEND MESSAGE
                          </button>
                        </div>
                      </div>
                    </form>
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
