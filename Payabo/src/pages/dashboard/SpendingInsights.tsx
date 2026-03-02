import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";

import {
  getAccountBreakdown,
  getCategoryBreakdown,
  getMerchantBreakdown,
  getSpendingSummary,
  type AccountSpendingItem,
  type CategorySpendingItem,
  type MerchantSpendingItem,
  type SpendingSummary
} from "../../api/personalFinance";

const toIsoDate = (value: Date) => value.toISOString();

export const SpendingInsights = () => {
  const today = useMemo(() => new Date(), []);
  const monthStart = useMemo(() => new Date(today.getFullYear(), today.getMonth(), 1), [today]);

  const [periodStart, setPeriodStart] = useState<string>(monthStart.toISOString().slice(0, 10));
  const [periodEnd, setPeriodEnd] = useState<string>(today.toISOString().slice(0, 10));
  const [summary, setSummary] = useState<SpendingSummary | null>(null);
  const [categories, setCategories] = useState<CategorySpendingItem[]>([]);
  const [merchants, setMerchants] = useState<MerchantSpendingItem[]>([]);
  const [accounts, setAccounts] = useState<AccountSpendingItem[]>([]);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const loadInsights = async () => {
    setErrorMessage(null);

    try {
      const start = toIsoDate(new Date(`${periodStart}T00:00:00Z`));
      const end = toIsoDate(new Date(`${periodEnd}T23:59:59Z`));

      const [summaryResult, categoryResult, merchantResult, accountResult] = await Promise.all([
        getSpendingSummary(start, end),
        getCategoryBreakdown(start, end),
        getMerchantBreakdown(start, end),
        getAccountBreakdown(start, end)
      ]);

      setSummary(summaryResult);
      setCategories(categoryResult);
      setMerchants(merchantResult);
      setAccounts(accountResult);
    } catch {
      setErrorMessage("Unable to load insights.");
      setSummary(null);
      setCategories([]);
      setMerchants([]);
      setAccounts([]);
    }
  };

  useEffect(() => {
    void loadInsights();
  }, []);

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <div className="d-flex justify-content-between align-items-center mb-3">
          <h3 className="alt mb-0">Spending insights</h3>
          <Link className="btn btn-outline-secondary" to="/transactions">Back to transactions</Link>
        </div>

        {errorMessage ? <div className="alert alert-warning">{errorMessage}</div> : null}

        <div className="card card-tbox mb-3">
          <div className="card-body row g-3 align-items-end">
            <div className="col-md-4">
              <label className="form-label">From</label>
              <input className="form-control" type="date" value={periodStart} onChange={(event) => setPeriodStart(event.target.value)} />
            </div>
            <div className="col-md-4">
              <label className="form-label">To</label>
              <input className="form-control" type="date" value={periodEnd} onChange={(event) => setPeriodEnd(event.target.value)} />
            </div>
            <div className="col-md-4 d-grid">
              <button type="button" className="btn btn-primary" onClick={() => void loadInsights()}>Refresh insights</button>
            </div>
          </div>
        </div>

        {summary ? (
          <div className="row g-3 mb-3">
            <div className="col-md-3"><div className="card card-tbox"><div className="card-body"><small className="text-muted">Income</small><h5>{summary.totalIncome.toFixed(2)} {summary.currency}</h5></div></div></div>
            <div className="col-md-3"><div className="card card-tbox"><div className="card-body"><small className="text-muted">Expenses</small><h5>{summary.totalExpense.toFixed(2)} {summary.currency}</h5></div></div></div>
            <div className="col-md-3"><div className="card card-tbox"><div className="card-body"><small className="text-muted">Net</small><h5>{summary.netAmount.toFixed(2)} {summary.currency}</h5></div></div></div>
            <div className="col-md-3"><div className="card card-tbox"><div className="card-body"><small className="text-muted">Transactions</small><h5>{summary.transactionCount}</h5></div></div></div>
          </div>
        ) : null}

        <div className="row g-3">
          <div className="col-lg-4">
            <div className="card card-tbox h-100"><div className="card-body"><h6>Category breakdown</h6>
              <ul className="list-group list-group-flush">
                {categories.map((item) => (
                  <li className="list-group-item d-flex justify-content-between px-0" key={item.category}>
                    <span>{item.category}</span>
                    <strong>{item.totalAmount.toFixed(2)} ({item.percentage.toFixed(1)}%)</strong>
                  </li>
                ))}
              </ul>
            </div></div>
          </div>

          <div className="col-lg-4">
            <div className="card card-tbox h-100"><div className="card-body"><h6>Top merchants</h6>
              <ul className="list-group list-group-flush">
                {merchants.map((item) => (
                  <li className="list-group-item d-flex justify-content-between px-0" key={item.merchant}>
                    <span>{item.merchant}</span>
                    <strong>{item.totalAmount.toFixed(2)}</strong>
                  </li>
                ))}
              </ul>
            </div></div>
          </div>

          <div className="col-lg-4">
            <div className="card card-tbox h-100"><div className="card-body"><h6>Account split</h6>
              <ul className="list-group list-group-flush">
                {accounts.map((item) => (
                  <li className="list-group-item d-flex justify-content-between px-0" key={item.personalAccountId ?? "unassigned"}>
                    <span>{item.accountName}</span>
                    <strong>{item.totalAmount.toFixed(2)}</strong>
                  </li>
                ))}
              </ul>
            </div></div>
          </div>
        </div>
      </div>
    </main>
  );
};
