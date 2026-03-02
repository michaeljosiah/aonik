import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import {
  acceptClassification,
  getReviewQueue,
  overrideClassification,
  type ClassificationReviewItem
} from "../../api/personalFinance";

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
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <div className="d-flex justify-content-between align-items-center mb-3">
          <h3 className="alt mb-0">Classification review</h3>
          <Link className="btn btn-outline-secondary" to="/transactions">Back to transactions</Link>
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
    </main>
  );
};
