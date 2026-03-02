import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";
import {
  listPersonalAccounts,
  listPersonalTransactions,
  type PersonalAccount,
  type PersonalTransaction
} from "../../api/personalFinance";

const formatDateLabel = (value: string) => {
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return "N/A";
  }

  return new Intl.DateTimeFormat("en-GB", {
    day: "2-digit",
    month: "short",
    year: "numeric"
  }).format(parsed);
};

const formatAmountLabel = (amount: number, currency: string) => {
  try {
    return new Intl.NumberFormat("en-GB", {
      style: "currency",
      currency: currency.toUpperCase(),
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    }).format(amount);
  } catch {
    return `${amount.toFixed(2)} ${currency.toUpperCase()}`;
  }
};

export const Transactions = () => {
  const { user } = useAuth();
  const [transactions, setTransactions] = useState<PersonalTransaction[]>([]);
  const [accounts, setAccounts] = useState<Record<string, PersonalAccount>>({});
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
        const [transactionResult, accountResult] = await Promise.all([
          listPersonalTransactions(),
          listPersonalAccounts()
        ]);

        if (cancelled) {
          return;
        }

        setTransactions(transactionResult);
        setAccounts(
          accountResult.reduce<Record<string, PersonalAccount>>((acc, account) => {
            acc[account.personalAccountId] = account;
            return acc;
          }, {})
        );
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
          <div className="alert alert-info">No transactions yet. Add one manually or import a statement to get started.</div>
        ) : (
          <div className="table-responsive">
            <table className="table table-card">
              <thead>
                <tr>
                  <th>Date</th>
                  <th>Merchant</th>
                  <th>Description</th>
                  <th>Category</th>
                  <th>Account</th>
                  <th>Logged</th>
                  <th className="text-end">Amount</th>
                  <th></th>
                </tr>
              </thead>
              <tbody>
                {transactions.map((item) => (
                  <tr key={item.personalTransactionId}>
                    <td>{formatDateLabel(item.occurredAt)}</td>
                    <td>{item.merchant ?? "-"}</td>
                    <td>{item.description ?? "-"}</td>
                    <td>{item.category ?? "Pending"}</td>
                    <td>{item.personalAccountId ? accounts[item.personalAccountId]?.name ?? "Account" : "Unassigned"}</td>
                    <td>{formatDateLabel(item.createdAt)}</td>
                    <td className="text-end">{formatAmountLabel(item.amount, item.currency)}</td>
                    <td>
                      <Link to="/transactions/review">Review</Link>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="d-flex gap-2 mt-3">
          <Link className="btn btn-primary" to="/transactions/manual/new">Add transaction</Link>
          <Link className="btn btn-outline-primary" to="/transactions/import">Import statement</Link>
          <Link className="btn btn-outline-secondary" to="/insights/spending">View insights</Link>
        </div>
      </div>
    </main>
  );
};
