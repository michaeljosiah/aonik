import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { getPublicBillPaymentDraft, type GuestBillPaymentDraftDetail } from "../../api/orders";
import { draftOrderIdStorageKey } from "./draftIntent";
import { loadFriendMessage, loadFriendSelection } from "./friendFlowStorage";

export const FriendCheckout = () => {
  const [searchParams] = useSearchParams();
  const orderIdFromQuery = searchParams.get("orderId") ?? "";

  const [orderId, setOrderId] = useState(orderIdFromQuery);
  const [draft, setDraft] = useState<GuestBillPaymentDraftDetail | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const selectedFriend = useMemo(() => loadFriendSelection(), []);
  const friendMessage = useMemo(() => loadFriendMessage(), []);

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
    return [draft.serviceName, draft.accountHolderName, ...entries.slice(0, 2)].filter(Boolean) as string[];
  }, [draft]);

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
                      <span className="d-block">{amountLabel}</span>
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
                  <Link className="back-left-arrow" to={`/payments/friend-message?orderId=${encodeURIComponent(orderId)}`}>
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z" fill="currentColor" />
                    </svg>{" "}
                    Back to Friend message
                  </Link>
                  <h3 className="alt mt-4 mb-4 pt-lg-3">Review your order</h3>
                  {errorMessage && <div className="alert alert-warning mt-3">{errorMessage}</div>}
                  {isLoading && <div className="alert alert-info mt-3">Loading your order summary...</div>}
                  <div className="form-tbox">
                    <div className="d-flex align-items-center mb-3">
                      <h4 className="text-gray mb-0 me-3">Service details</h4>
                      <Link className="text-underline small" to="/payments/selection">
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
                                  <img src="/images/product-img-04.png" alt={draft?.billerName ?? "Biller"} />
                                </div>
                                <div className="pt-1">
                                  <strong className="heading-td">{draft?.billerName ?? "Selected provider"}</strong>
                                </div>
                              </div>
                            </td>
                            <td>
                              <strong className="heading-td">{draft?.serviceName ?? "Service"}</strong>
                              <span className="info-td d-block">{serviceDetailsLines[0]}</span>
                            </td>
                            <td className="text-end">
                              <strong className="heading-td">{amountLabel}</strong>
                              <span className="info-td d-block">{draft?.currency ?? "Currency"}</span>
                            </td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                    <div className="d-flex align-items-center mb-3">
                      <h4 className="text-gray mb-0 me-3">Request help with payment</h4>
                      <Link className="text-underline small" to={`/payments/select-friend?orderId=${encodeURIComponent(orderId)}`}>
                        Edit
                      </Link>
                    </div>
                    <div className="table-responsive mb-4">
                      <table className="table table-card">
                        <thead>
                          <tr>
                            <th className="col pe-4 py-2">TO</th>
                            <th className="col py-2">MESSAGE SENT</th>
                          </tr>
                        </thead>
                        <tbody>
                          <tr>
                            <td>
                              <div className="d-flex align-items-center">
                                <div className="img-td">
                                  <img src="/images/profile-pic.png" alt="Friend" />
                                </div>
                                <div className="pt-1">
                                  <strong className="heading-td">
                                    {selectedFriend ? `${selectedFriend.firstName} ${selectedFriend.lastName}` : "Friend"}
                                  </strong>
                                  <span className="info-td d-block">{selectedFriend?.email ?? "No email provided"}</span>
                                </div>
                              </div>
                            </td>
                            <td>
                              <span className="info-td">
                                {friendMessage?.skipped
                                  ? "Skipped"
                                  : friendMessage?.message
                                  ? friendMessage.message
                                  : "No message yet"}
                              </span>
                            </td>
                          </tr>
                        </tbody>
                      </table>
                    </div>
                    <div className="d-flex justify-content-end">
                      <button className="btn btn-primary btn-md" type="button" disabled={!orderId || !selectedFriend}>
                        SEND REQUEST
                      </button>
                    </div>
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
