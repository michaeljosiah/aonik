import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";

import { getPublicBillPaymentDraft, type GuestBillPaymentDraftDetail } from "../../api/orders";
import { draftOrderIdStorageKey } from "./draftIntent";
import {
  loadFriendSelection,
  saveFriendSelection,
  type FriendProfile
} from "./friendFlowStorage";

const savedFriends: FriendProfile[] = [
  {
    id: "friend_1",
    firstName: "Amaka",
    lastName: "Okoro",
    email: "amaka.okoro@example.com",
    relationship: "Sister"
  },
  {
    id: "friend_2",
    firstName: "Kwame",
    lastName: "Mensah",
    email: "kwame.mensah@example.com",
    relationship: "Friend"
  },
  {
    id: "friend_3",
    firstName: "Zainab",
    lastName: "Yusuf",
    email: "zainab.yusuf@example.com",
    relationship: "Cousin"
  }
];

export const SelectFriend = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const orderIdFromQuery = searchParams.get("orderId") ?? "";
  const storedSelection = useMemo(() => loadFriendSelection(), []);

  const [orderId, setOrderId] = useState<string>(orderIdFromQuery);
  const [draft, setDraft] = useState<GuestBillPaymentDraftDetail | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [selectedFriendId, setSelectedFriendId] = useState<string>(() => storedSelection?.id ?? savedFriends[0].id);
  const [searchTerm, setSearchTerm] = useState<string>("");

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

  const availableFriends = useMemo(() => {
    if (!storedSelection || savedFriends.some((friend) => friend.id === storedSelection.id)) {
      return savedFriends;
    }

    return [storedSelection, ...savedFriends];
  }, [storedSelection]);

  const filteredFriends = useMemo(() => {
    if (!searchTerm) {
      return availableFriends;
    }

    const normalized = searchTerm.toLowerCase();
    return availableFriends.filter((friend) => {
      return (
        friend.firstName.toLowerCase().includes(normalized) ||
        friend.lastName.toLowerCase().includes(normalized) ||
        friend.email.toLowerCase().includes(normalized)
      );
    });
  }, [availableFriends, searchTerm]);

  const selectedFriend = useMemo(() => {
    return availableFriends.find((friend) => friend.id === selectedFriendId) ?? availableFriends[0];
  }, [availableFriends, selectedFriendId]);

  const handleContinue = () => {
    saveFriendSelection(selectedFriend);
    const params = new URLSearchParams({ orderId });
    navigate(`/payments/friend-message?${params.toString()}`);
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
                  <Link className="back-left-arrow" to="/payments/selection">
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z" fill="currentColor" />
                    </svg>{" "}
                    Back to Select payment method
                  </Link>
                  <h3 className="alt mt-4 mb-3 pt-lg-3">Request help with payment</h3>
                  <p>Please select the friend or family member that will be helping to pay this bill.</p>
                  {errorMessage && <div className="alert alert-warning mt-3">{errorMessage}</div>}
                  {isLoading && <div className="alert alert-info mt-3">Loading your order summary...</div>}
                  <div className="d-sm-flex align-items-end justify-content-between">
                    <div className="d-flex align-items-center mb-4">
                      <h4 className="mb-0 me-2">My friends</h4>
                      <Link className="text-underline small ms-2" to="#">
                        Manage
                      </Link>
                    </div>
                    <Link className="btn btn-secondary btn-md mb-4" to={`/payments/friend-details?orderId=${encodeURIComponent(orderId)}`}>
                      ADD NEW FRIEND
                    </Link>
                  </div>
                  <div className="form-tbox">
                    <div className="mb-3">
                      <div className="input-group search-box">
                        <span className="input-group-text">
                          <svg width="25" height="25" viewBox="0 0 25 25" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M10.1499 10.1484L24.0015 24.002" stroke="#B4BFC3" strokeWidth="2" />
                            <path d="M10 20C15.5228 20 20 15.5228 20 10C20 4.47715 15.5228 0 10 0C4.47715 0 0 4.47715 0 10C0 15.5228 4.47715 20 10 20Z" fill="white" />
                            <path d="M10 19C14.9706 19 19 14.9706 19 10C19 5.02944 14.9706 1 10 1C5.02944 1 1 5.02944 1 10C1 14.9706 5.02944 19 10 19Z" stroke="#B4BFC3" strokeWidth="2" />
                          </svg>
                        </span>
                        <input
                          className="form-control"
                          placeholder="Search friends"
                          value={searchTerm}
                          onChange={(event) => setSearchTerm(event.target.value)}
                        />
                      </div>
                    </div>
                    <div className="table-responsive mb-4">
                      <table className="table table-card">
                        <thead>
                          <tr>
                            <th className="col pe-4 py-2">NAME</th>
                            <th className="col py-2">EMAIL</th>
                            <th className="col py-2">RELATIONSHIP</th>
                          </tr>
                        </thead>
                        <tbody>
                          {filteredFriends.map((friend) => {
                            const isSelected = friend.id === selectedFriendId;
                            return (
                              <tr key={friend.id} className={isSelected ? "table-active" : undefined}>
                                <td>
                                  <label className="d-flex align-items-center gap-2 mb-0">
                                    <input
                                      type="radio"
                                      name="friend"
                                      checked={isSelected}
                                      onChange={() => setSelectedFriendId(friend.id)}
                                    />
                                    <div>
                                      <strong className="heading-td">
                                        {friend.firstName} {friend.lastName}
                                      </strong>
                                    </div>
                                  </label>
                                </td>
                                <td>
                                  <span className="info-td">{friend.email}</span>
                                </td>
                                <td>
                                  <span className="info-td">{friend.relationship ?? "Friend"}</span>
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                    <div className="d-flex justify-content-end">
                      <button className="btn btn-primary btn-md" type="button" onClick={handleContinue} disabled={!orderId}>
                        CONTINUE
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
