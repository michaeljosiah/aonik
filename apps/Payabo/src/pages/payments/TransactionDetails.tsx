import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";
import { getRecentTransactionById, type DashboardRecentTransaction } from "../../api/dashboard";
import { getPaymentHistoryItemForUser } from "./paymentHistory";

export const TransactionDetails = () => {
  const { user } = useAuth();
  const [searchParams] = useSearchParams();
  const transactionId = searchParams.get("id") ?? "";
  const [transaction, setTransaction] = useState<DashboardRecentTransaction | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      if (!user?.id || !transactionId) {
        setTransaction(null);
        setIsLoading(false);
        return;
      }

      setIsLoading(true);

      try {
        const response = await getRecentTransactionById(transactionId);
        if (!cancelled) {
          setTransaction(response);
        }
      } catch {
        if (!cancelled) {
          setTransaction(null);
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, [transactionId, user?.id]);

  const localHistoryTransaction = user?.id && transactionId ? getPaymentHistoryItemForUser(user.id, transactionId) : null;

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Transaction details</h3>

        {isLoading && <div className="alert alert-secondary">Loading transaction details...</div>}

        {!isLoading && !transaction && <div className="alert alert-warning">Transaction not found.</div>}

        {!isLoading && transaction && (
          <div className="card card-tbox">
            <div className="card-body">
              <p><strong>Transaction ID:</strong> {transaction.id}</p>
              <p><strong>Status:</strong> {transaction.status}</p>
              <p><strong>Service:</strong> {transaction.serviceName}</p>
              <p><strong>Biller:</strong> {transaction.billerName}</p>
              <p><strong>Amount:</strong> {transaction.amountLabel}</p>
              <p><strong>Date:</strong> {transaction.dateLabel}</p>
              {localHistoryTransaction && (
                <>
                  <hr />
                  <p><strong>Order ID:</strong> {localHistoryTransaction.orderId}</p>
                  <p><strong>Payment Intent ID:</strong> {localHistoryTransaction.paymentIntentId}</p>
                  <p className="mb-0"><strong>Provider Reference:</strong> {localHistoryTransaction.providerReference}</p>
                </>
              )}
            </div>
          </div>
        )}

        <div className="mt-3">
          <Link className="btn btn-secondary" to="/transactions">Back to transactions</Link>
        </div>
      </div>
    </main>
  );
};
