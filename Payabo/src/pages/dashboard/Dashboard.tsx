import { useState } from "react";
import { Link } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";
import { SidebarNav } from "../../components/navigation/SidebarNav";
import { useDashboardData } from "../../hooks/useDashboardData";

type BillTab = "search" | "invoice";

const showOptionalPanels = (import.meta.env.VITE_PAYABO_SHOW_OPTIONAL_DASHBOARD_PANELS ?? "false") === "true";

export const Dashboard = () => {
  const [activeTab, setActiveTab] = useState<BillTab>("invoice");
  const { user } = useAuth();
  const { upcomingBills, recentTransactions, isLoading, errorMessage, refresh } = useDashboardData(user?.id);

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
                <div className="col-md-8">
                  <h3 className="alt mt-4">Bill management area</h3>
                  <p>
                    This is your bill management area. Get insights into all your bills, check upcoming bills and pay new
                    bills all in this area.
                  </p>
                </div>
                <div className="col-md-4 text-md-end">
                  <button type="button" className="btn btn-link" onClick={() => void refresh()}>
                    Refresh activity
                  </button>
                </div>
              </div>

              {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

              <div className="row">
                <div className="col-xl-8 mb-4">
                  <div className="card card-tbox h-100">
                    <div className="card-body">
                      <div className="d-flex justify-content-between align-items-center mb-3">
                        <h4 className="mb-0">My upcoming bills</h4>
                        <Link className="btn btn-link" to="/payments/providers">
                          View all
                        </Link>
                      </div>

                      {isLoading ? (
                        <p>Loading upcoming bills...</p>
                      ) : upcomingBills.length === 0 ? (
                        <div className="alert alert-info mb-0">No upcoming bills yet. Start a payment to see activity here.</div>
                      ) : (
                        <div className="table-responsive">
                          <table className="table table-card table-hover">
                            <thead>
                              <tr>
                                <th>BILLER</th>
                                <th>SERVICE</th>
                                <th>DUE DATE</th>
                                <th className="text-end">AMOUNT</th>
                              </tr>
                            </thead>
                            <tbody>
                              {upcomingBills.map((bill) => (
                                <tr key={bill.id}>
                                  <td>{bill.billerName}</td>
                                  <td>{bill.serviceName}</td>
                                  <td>{bill.dueDate}</td>
                                  <td className="text-end">{bill.amountLabel}</td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                <div className="col-xl-4 mb-4">
                  <div className="card card-tbox h-100">
                    <div className="card-body">
                      <h4 className="mb-3">Pay a bill</h4>
                      <div className="btn-group mb-3" role="group" aria-label="Bill mode tabs">
                        <button
                          type="button"
                          className={`btn ${activeTab === "invoice" ? "btn-primary" : "btn-outline-primary"}`}
                          onClick={() => setActiveTab("invoice")}
                        >
                          Invoice number
                        </button>
                        <button
                          type="button"
                          className={`btn ${activeTab === "search" ? "btn-primary" : "btn-outline-primary"}`}
                          onClick={() => setActiveTab("search")}
                        >
                          Search provider
                        </button>
                      </div>

                      <p className="text-gray">
                        {activeTab === "invoice"
                          ? "Use your invoice/service details to continue with payment."
                          : "Select a provider and service from the live catalog."}
                      </p>

                      <Link className="btn btn-primary" to="/payments/providers">
                        Continue to providers
                      </Link>
                    </div>
                  </div>
                </div>
              </div>

              <div className="row">
                <div className="col-xl-12 mb-4">
                  <div className="card card-tbox h-100">
                    <div className="card-body">
                      <div className="d-flex justify-content-between align-items-center mb-3">
                        <h4 className="mb-0">My recent transactions</h4>
                        <Link className="btn btn-link" to="/transactions">
                          View all
                        </Link>
                      </div>

                      {isLoading ? (
                        <p>Loading recent transactions...</p>
                      ) : recentTransactions.length === 0 ? (
                        <div className="alert alert-info mb-0">No transactions yet. Complete a checkout to populate your history.</div>
                      ) : (
                        <div className="table-responsive">
                          <table className="table table-card table-hover">
                            <thead>
                              <tr>
                                <th>BILLER</th>
                                <th>SERVICE</th>
                                <th>DATE</th>
                                <th>STATUS</th>
                                <th className="text-end">AMOUNT</th>
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

              {!showOptionalPanels && (
                <div className="alert alert-secondary">
                  Optional dashboard panels are hidden for MVP focus. Set
                  <code className="ms-1">VITE_PAYABO_SHOW_OPTIONAL_DASHBOARD_PANELS=true</code> to enable them.
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
