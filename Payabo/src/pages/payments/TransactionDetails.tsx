import { Link, useSearchParams } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";
import { getPaymentHistoryItemForUser } from "./paymentHistory";

export const TransactionDetails = () => {
  const { user } = useAuth();
  const [searchParams] = useSearchParams();
  const transactionId = searchParams.get("id") ?? "";

  const transaction = user?.id && transactionId ? getPaymentHistoryItemForUser(user.id, transactionId) : null;

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Transaction details</h3>

        {!transaction && <div className="alert alert-warning">Transaction not found.</div>}

        {transaction && (
          <div className="card card-tbox">
            <div className="card-body">
              <p><strong>Order ID:</strong> {transaction.orderId}</p>
              <p><strong>Payment Intent ID:</strong> {transaction.paymentIntentId}</p>
              <p><strong>Provider Reference:</strong> {transaction.providerReference}</p>
              <p><strong>Status:</strong> {transaction.status}</p>
              <p><strong>Order Status:</strong> {transaction.orderStatus}</p>
              <p><strong>Service:</strong> {transaction.serviceName}</p>
              <p><strong>Biller:</strong> {transaction.billerName ?? "Provider"}</p>
              <p><strong>Amount:</strong> {transaction.amount != null ? `${transaction.currency} ${transaction.amount.toFixed(2)}` : "-"}</p>
              <p className="mb-0"><strong>Created:</strong> {new Date(transaction.createdAt).toLocaleString()}</p>
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
