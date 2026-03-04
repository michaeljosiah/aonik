import { Link } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";
import { SidebarNav } from "../../components/navigation/SidebarNav";
import { useDashboardData } from "../../hooks/useDashboardData";

export const BillTransactions = () => {
  const { user } = useAuth();
  const { recentTransactions, isLoading, errorMessage, refresh } = useDashboardData(user?.id);

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
                  <Link className="back-left-arrow" to="/dashboard">
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z" fill="currentColor" />
                    </svg>
                    Back to dashboard
                  </Link>
                  <h3 className="alt mt-4">Bill payment transactions</h3>
                  <p>Review your recent bill payment activity and open transaction details for any completed checkout.</p>
                </div>
                <div className="col-xl-4 text-xl-end">
                  <button type="button" className="btn btn-link" onClick={() => void refresh()}>
                    Refresh transactions
                  </button>
                </div>
              </div>

              {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

              <div className="card card-tbox h-100">
                <div className="card-body">
                  {isLoading ? (
                    <div className="alert alert-secondary mb-0">Loading transactions...</div>
                  ) : recentTransactions.length === 0 ? (
                    <div className="alert alert-info mb-0">No bill payment transactions yet.</div>
                  ) : (
                    <div className="table-responsive">
                      <table className="table table-card table-hover mb-0">
                        <thead>
                          <tr>
                            <th>BILLER</th>
                            <th>SERVICE</th>
                            <th>DATE</th>
                            <th>STATUS</th>
                            <th className="text-end">AMOUNT</th>
                            <th></th>
                          </tr>
                        </thead>
                        <tbody>
                          {recentTransactions.map((transaction) => (
                            <tr key={transaction.id}>
                              <td>{transaction.billerName}</td>
                              <td>{transaction.serviceName}</td>
                              <td>{transaction.dateLabel}</td>
                              <td>{transaction.status}</td>
                              <td className="text-end">{transaction.amountLabel}</td>
                              <td className="text-end">
                                <Link to={`/payments/transaction-details?id=${encodeURIComponent(transaction.id)}`}>Details</Link>
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
