import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";
import { getRecentTransactions, type DashboardRecentTransaction } from "../../api/dashboard";

export const Transactions = () => {
  const { user } = useAuth();
  const [transactions, setTransactions] = useState<DashboardRecentTransaction[]>([]);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      if (!user?.id) {
        setTransactions([]);
        setIsLoading(false);
        return;
      }

      setIsLoading(true);
      setErrorMessage(null);

      try {
        const result = await getRecentTransactions(user.id);
        if (cancelled) {
          return;
        }

        setTransactions(result);
      } catch {
        if (cancelled) {
          return;
        }

        setTransactions([]);
        setErrorMessage("Unable to load transactions right now.");
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
  }, [user?.id]);

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Transactions</h3>

        {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

        {isLoading ? (
          <div className="alert alert-secondary">Loading transactions...</div>
        ) : transactions.length === 0 ? (
          <div className="alert alert-info">No transactions yet. Complete a checkout to populate your history.</div>
        ) : (
          <div className="table-responsive">
            <table className="table table-card">
              <thead>
                <tr>
                  <th>Service</th>
                  <th>Biller</th>
                  <th>Date</th>
                  <th>Status</th>
                  <th className="text-end">Amount</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {transactions.map((item) => (
                  <tr key={item.id}>
                    <td>{item.serviceName}</td>
                    <td>{item.billerName}</td>
                    <td>{item.dateLabel}</td>
                    <td>{item.status}</td>
                    <td className="text-end">{item.amountLabel}</td>
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
