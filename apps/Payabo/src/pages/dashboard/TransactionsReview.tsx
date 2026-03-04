import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import {
  acceptClassification,
  getReviewQueue,
  overrideClassification,
  type ClassificationReviewItem
} from "../../api/personalFinance";
import { SidebarNav } from "../../components/navigation/SidebarNav";

export const TransactionsReview = () => {
  const [items, setItems] = useState<ClassificationReviewItem[]>([]);
  const [categoryDrafts, setCategoryDrafts] = useState<Record<string, string>>({});
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const loadQueue = async () => {
    const queue = await getReviewQueue();
    setItems(queue);
    setCategoryDrafts((current) => {
      const next = { ...current };
      for (const item of queue) {
        if (!next[item.personalTransactionId]) {
          next[item.personalTransactionId] = item.category ?? "";
        }
      }
      return next;
    });
  };

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        if (!cancelled) {
          await loadQueue();
        }
      } catch {
        if (!cancelled) {
          setErrorMessage("Unable to load review queue.");
        }
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, []);

  const handleAccept = async (transactionId: string) => {
    try {
      await acceptClassification(transactionId);
      await loadQueue();
    } catch {
      setErrorMessage("Failed to accept classification.");
    }
  };

  const handleOverride = async (transactionId: string) => {
    const category = categoryDrafts[transactionId]?.trim();
    if (!category) {
      setErrorMessage("Category is required to override classification.");
      return;
    }

    try {
      await overrideClassification(transactionId, category, true);
      await loadQueue();
    } catch {
      setErrorMessage("Failed to override classification.");
    }
  };

  return (
    <main className="bg-secondary overflow-hidden">
      <div className="container">
        <div className="row">
          <div className="col-lg-4 col-xl-3">
            <SidebarNav />
          </div>
          <div className="col-lg-8 col-xl-9">
            <div className="wrapper-content">
              <div className="row align-items-end mb-md-2">
                <div className="col-xl-8">
                  <Link className="back-left-arrow" to="/personal-finance/transactions">
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z" fill="currentColor" />
                    </svg>
                    Back to transactions
                  </Link>
                  <h3 className="alt mt-4">Classification review</h3>
                </div>
              </div>

              {errorMessage ? <div className="alert alert-warning">{errorMessage}</div> : null}

              {items.length === 0 ? (
                <div className="alert alert-success">Review queue is empty. Nice work.</div>
              ) : (
                <div className="card card-tbox">
                  <div className="card-body">
                    <div className="table-responsive">
                      <table className="table table-card mb-0">
                        <thead>
                          <tr>
                            <th>Merchant</th>
                            <th>Description</th>
                            <th>Amount</th>
                            <th>Confidence</th>
                            <th>Category</th>
                            <th></th>
                          </tr>
                        </thead>
                        <tbody>
                          {items.map((item) => (
                            <tr key={item.personalTransactionId}>
                              <td>{item.merchant ?? "-"}</td>
                              <td>{item.description ?? "-"}</td>
                              <td>{item.amount.toFixed(2)} {item.currency}</td>
                              <td>{Math.round(item.confidence * 100)}%</td>
                              <td style={{ minWidth: 200 }}>
                                <input
                                  className="form-control form-control-sm"
                                  value={categoryDrafts[item.personalTransactionId] ?? ""}
                                  onChange={(event) => {
                                    const value = event.target.value;
                                    setCategoryDrafts((current) => ({ ...current, [item.personalTransactionId]: value }));
                                  }}
                                  placeholder="Category"
                                />
                              </td>
                              <td className="text-end">
                                <div className="d-flex gap-2 justify-content-end">
                                  <button
                                    type="button"
                                    className="btn btn-sm btn-outline-secondary"
                                    onClick={() => void handleAccept(item.personalTransactionId)}
                                  >
                                    Accept
                                  </button>
                                  <button
                                    type="button"
                                    className="btn btn-sm btn-primary"
                                    onClick={() => void handleOverride(item.personalTransactionId)}
                                  >
                                    Override
                                  </button>
                                </div>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
