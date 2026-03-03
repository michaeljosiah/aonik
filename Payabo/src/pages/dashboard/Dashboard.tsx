import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";

import { getPublicCatalogCountries, type CatalogCountry } from "../../api/catalog";
import { useAuth } from "../../app/auth/AuthContext";
import { SidebarNav } from "../../components/navigation/SidebarNav";
import { useDashboardData } from "../../hooks/useDashboardData";

type BillTab = "search" | "invoice";

const showOptionalPanels = (import.meta.env.VITE_PAYABO_SHOW_OPTIONAL_DASHBOARD_PANELS ?? "false") === "true";

const buildProvidersPath = (countryCode: string) => {
  const normalizedCountryCode = countryCode.trim().toUpperCase();
  if (!normalizedCountryCode) {
    return "/payments/providers";
  }

  const params = new URLSearchParams({ countryCode: normalizedCountryCode });
  return `/payments/providers?${params.toString()}`;
};

export const Dashboard = () => {
  const navigate = useNavigate();
  const countriesSelectRef = useRef<HTMLSelectElement | null>(null);
  const [activeTab, setActiveTab] = useState<BillTab>("invoice");
  const [invoiceNumber, setInvoiceNumber] = useState("");
  const [countries, setCountries] = useState<CatalogCountry[]>([]);
  const [selectedCountry, setSelectedCountry] = useState<string>("");
  const [countriesError, setCountriesError] = useState<string | null>(null);
  const [isLoadingCountries, setIsLoadingCountries] = useState<boolean>(true);

  const { user } = useAuth();
  const { upcomingBills, recentTransactions, isLoading, errorMessage, refresh } = useDashboardData(user?.id);

  useEffect(() => {
    let cancelled = false;

    const loadCountries = async () => {
      setIsLoadingCountries(true);
      setCountriesError(null);

      try {
        const result = await getPublicCatalogCountries();
        if (cancelled) {
          return;
        }

        setCountries(result);
        setSelectedCountry((current) => current || result[0]?.code || "");
        window.requestAnimationFrame(() => {
          window.dispatchEvent(new Event("payabo:refresh-selects"));
        });
      } catch {
        if (cancelled) {
          return;
        }

        setCountries([]);
        setSelectedCountry("");
        setCountriesError("We couldn't load countries right now. Please try again.");
      } finally {
        if (!cancelled) {
          setIsLoadingCountries(false);
        }
      }
    };

    void loadCountries();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (activeTab !== "search") {
      return;
    }

    window.requestAnimationFrame(() => {
      window.dispatchEvent(new Event("payabo:refresh-selects"));
    });
  }, [activeTab]);

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
                                <th></th>
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
                      <div className="d-flex justify-content-between align-items-center mb-4">
                        <h4 className="mb-0">Pay a bill</h4>
                      </div>
                      <nav>
                        <div className="nav-tabs nav nav-fill">
                          <a
                            className={`nav-link ${activeTab === "search" ? "active" : ""}`}
                            data-bs-toggle="tab"
                            href="#tab-1"
                            onClick={(event) => {
                              event.preventDefault();
                              setActiveTab("search");
                            }}
                          >
                            SEARCH BILL
                          </a>
                          <a
                            className={`nav-link ${activeTab === "invoice" ? "active" : ""}`}
                            data-bs-toggle="tab"
                            href="#tab-2"
                            onClick={(event) => {
                              event.preventDefault();
                              setActiveTab("invoice");
                            }}
                          >
                            PAY INVOICE
                          </a>
                        </div>
                      </nav>
                      <div className="tab-content">
                        <div className={`tab-pane fade ${activeTab === "search" ? "show active" : ""}`} id="tab-1">
                          <form
                            onSubmit={(event) => {
                              event.preventDefault();
                              const selectedCountryCode = (countriesSelectRef.current?.value ?? selectedCountry)
                                .trim()
                                .toUpperCase();
                              setSelectedCountry(selectedCountryCode);
                              navigate(buildProvidersPath(selectedCountryCode));
                            }}
                          >
                            <label htmlFor="dashboard-countries" className="form-label">Destination country</label>
                            <div className="select mb-3">
                              <select
                                ref={countriesSelectRef}
                                className="form-control countries"
                                id="dashboard-countries"
                                value={selectedCountry}
                                onChange={(event) => setSelectedCountry(event.target.value)}
                                disabled={isLoadingCountries || countries.length === 0}
                              >
                                {isLoadingCountries && <option value="">Loading countries...</option>}
                                {!isLoadingCountries && countries.length === 0 && <option value="">No countries available</option>}
                                {countries.map((country) => (
                                  <option key={country.code} value={country.code}>
                                    {country.name}
                                  </option>
                                ))}
                              </select>
                            </div>
                            {countriesError && <p className="text-danger small mb-3">{countriesError}</p>}
                            <p className="text-md mb-4">Note: Start by selecting the country you wish to pay a bill from.</p>
                            <div className="text-center">
                              <button
                                type="submit"
                                className="btn btn-primary btn-sm"
                                disabled={isLoadingCountries || countries.length === 0}
                              >
                                GET STARTED
                              </button>
                            </div>
                          </form>
                        </div>
                        <div className={`tab-pane fade ${activeTab === "invoice" ? "show active" : ""}`} id="tab-2">
                          <form
                            onSubmit={(event) => {
                              event.preventDefault();
                              navigate("/payments/providers");
                            }}
                          >
                            <label htmlFor="invoice" className="form-label">Invoice number</label>
                            <div className="mb-3">
                              <input
                                type="text"
                                className="form-control"
                                name="InvoiceNumber"
                                id="invoice"
                                placeholder="Enter MBA invoice number"
                                value={invoiceNumber}
                                onChange={(event) => setInvoiceNumber(event.target.value)}
                              />
                            </div>
                            <p className="text-md mb-3">Note: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore.</p>
                            <div className="text-center">
                              <button type="submit" className="btn btn-primary btn-sm">GET STARTED</button>
                            </div>
                          </form>
                        </div>
                      </div>
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
                                  <td>
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
