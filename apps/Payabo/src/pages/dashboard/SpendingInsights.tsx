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
import { SidebarNav } from "../../components/navigation/SidebarNav";

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
                  <h3 className="alt mt-4">Spending insights</h3>
                </div>
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
          </div>
        </div>
      </div>
    </main>
  );
};
