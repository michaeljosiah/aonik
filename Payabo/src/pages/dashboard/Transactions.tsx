import { useMemo } from "react";
import { Link } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";
import { listPaymentHistoryForUser } from "../payments/paymentHistory";

export const Transactions = () => {
  const { user } = useAuth();

  const transactions = useMemo(() => {
    if (!user?.id) {
      return [];
    }

    return listPaymentHistoryForUser(user.id);
  }, [user?.id]);

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Transactions</h3>

        {transactions.length === 0 ? (
          <div className="alert alert-info">No transactions yet. Complete a checkout to populate your history.</div>
        ) : (
          <div className="table-responsive">
            <table className="table table-card">
              <thead>
                <tr>
                  <th>Created</th>
                  <th>Service</th>
                  <th>Biller</th>
                  <th>Status</th>
                  <th className="text-end">Amount</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {transactions.map((item) => (
                  <tr key={item.id}>
                    <td>{new Date(item.createdAt).toLocaleString()}</td>
                    <td>{item.serviceName}</td>
                    <td>{item.billerName ?? "Provider"}</td>
                    <td>{item.status}</td>
                    <td className="text-end">
                      {item.amount != null ? `${item.currency} ${item.amount.toFixed(2)}` : "-"}
                    </td>
                    <td>
                      <Link to={`/payments/transaction-details?id=${encodeURIComponent(item.id)}`}>Details</Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </main>
  );
};
