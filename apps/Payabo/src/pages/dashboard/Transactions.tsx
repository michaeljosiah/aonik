import { useEffect, useMemo, useState } from "react";
import { Link } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";
import {
  listPersonalAccounts,
  listPersonalTransactions,
  type PersonalAccount,
  type PersonalTransaction
} from "../../api/personalFinance";
import { SidebarNav } from "../../components/navigation/SidebarNav";

type StatusFilter = "all" | "paid" | "pending" | "failed";
type TransactionStatus = "PAID" | "PENDING" | "FAILED";

type DisplayTransaction = {
  transaction: PersonalTransaction;
  account: PersonalAccount | null;
  biller: string;
  detail: string;
  reference: string;
  paymentMethod: string;
  status: TransactionStatus;
  imagePath: string;
  occurredAtDate: Date | null;
};

const pageSizeOptions = [10, 25, 50, 100] as const;

const toDate = (value: string) => {
  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? null : parsed;
};

const formatTableDate = (value: string) => {
  const parsed = toDate(value);
  if (!parsed) {
    return "N/A";
  }

  const day = String(parsed.getDate()).padStart(2, "0");
  const month = String(parsed.getMonth() + 1).padStart(2, "0");
  const year = parsed.getFullYear();
  return `${day}.${month}.${year}`;
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

const normalizeToken = (value: string | null | undefined) => (value ?? "").trim().toLowerCase();

const formatReference = (transactionId: string) => transactionId.replace(/-/g, "").toUpperCase().slice(0, 8);

const statusClassNames: Record<TransactionStatus, string> = {
  PAID: "btn-success-sm",
  PENDING: "btn-light-sm",
  FAILED: "btn-danger-sm"
};

const resolveStatusFromTags = (tags: string[]): TransactionStatus | null => {
  const normalizedTags = tags.map((tag) => normalizeToken(tag));

  if (normalizedTags.includes("failed")) {
    return "FAILED";
  }

  if (normalizedTags.includes("pending")) {
    return "PENDING";
  }

  if (normalizedTags.includes("paid") || normalizedTags.includes("completed") || normalizedTags.includes("success")) {
    return "PAID";
  }

  return null;
};

const resolveStatus = (transaction: PersonalTransaction): TransactionStatus => {
  const statusFromTags = resolveStatusFromTags(transaction.tags);
  if (statusFromTags) {
    return statusFromTags;
  }

  return transaction.category ? "PAID" : "PENDING";
};

const resolvePaymentMethod = (transaction: PersonalTransaction, account: PersonalAccount | null) => {
  const normalizedTags = transaction.tags.map((tag) => normalizeToken(tag));

  if (normalizedTags.includes("payment-assist") || normalizedTags.includes("assist")) {
    return "Payment assist";
  }

  if (normalizedTags.includes("card")) {
    return "Card";
  }

  if (account?.accountType) {
    return account.accountType;
  }

  return "Wallet";
};

const resolveImagePath = (seed: string) => {
  let hash = 0;
  for (const char of seed) {
    hash = (hash * 31 + char.charCodeAt(0)) | 0;
  }

  const imageIndex = (Math.abs(hash) % 5) + 1;
  return `/images/product-img-0${imageIndex}.png`;
};

export const Transactions = () => {
  const { user } = useAuth();

  const [transactions, setTransactions] = useState<PersonalTransaction[]>([]);
  const [accounts, setAccounts] = useState<Record<string, PersonalAccount>>({});
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [searchQuery, setSearchQuery] = useState("");
  const [startDate, setStartDate] = useState("");
  const [endDate, setEndDate] = useState("");
  const [statusFilter, setStatusFilter] = useState<StatusFilter>("all");
  const [pageSize, setPageSize] = useState<number>(10);
  const [currentPage, setCurrentPage] = useState<number>(1);

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

  const filteredTransactions = useMemo<DisplayTransaction[]>(() => {
    const normalizedQuery = normalizeToken(searchQuery);
    const startBoundary = startDate ? new Date(`${startDate}T00:00:00`) : null;
    const endBoundary = endDate ? new Date(`${endDate}T23:59:59.999`) : null;

    return transactions
      .map((transaction) => {
        const account = transaction.personalAccountId ? (accounts[transaction.personalAccountId] ?? null) : null;
        const biller = transaction.merchant ?? account?.institutionName ?? account?.name ?? "Unknown biller";
        const detail = transaction.description ?? "Subscription payment";
        const reference = formatReference(transaction.personalTransactionId);
        const paymentMethod = resolvePaymentMethod(transaction, account);
        const status = resolveStatus(transaction);
        const imagePath = resolveImagePath(`${biller}-${transaction.personalTransactionId}`);
        const occurredAtDate = toDate(transaction.occurredAt);

        return {
          transaction,
          account,
          biller,
          detail,
          reference,
          paymentMethod,
          status,
          imagePath,
          occurredAtDate
        };
      })
      .filter((item) => {
        if (normalizedQuery) {
          const searchableText = [
            item.biller,
            item.detail,
            item.reference,
            item.paymentMethod,
            item.account?.name ?? ""
          ]
            .join(" ")
            .toLowerCase();

          if (!searchableText.includes(normalizedQuery)) {
            return false;
          }
        }

        if (startBoundary) {
          if (!item.occurredAtDate || item.occurredAtDate < startBoundary) {
            return false;
          }
        }

        if (endBoundary) {
          if (!item.occurredAtDate || item.occurredAtDate > endBoundary) {
            return false;
          }
        }

        if (statusFilter === "paid" && item.status !== "PAID") {
          return false;
        }

        if (statusFilter === "pending" && item.status !== "PENDING") {
          return false;
        }

        if (statusFilter === "failed" && item.status !== "FAILED") {
          return false;
        }

        return true;
      })
      .sort((left, right) => {
        const leftTime = left.occurredAtDate?.getTime() ?? 0;
        const rightTime = right.occurredAtDate?.getTime() ?? 0;
        return rightTime - leftTime;
      });
  }, [accounts, endDate, searchQuery, startDate, statusFilter, transactions]);

  useEffect(() => {
    setCurrentPage(1);
  }, [searchQuery, startDate, endDate, statusFilter, pageSize]);

  useEffect(() => {
    const maxPage = Math.max(1, Math.ceil(filteredTransactions.length / pageSize));
    setCurrentPage((value) => (value > maxPage ? maxPage : value));
  }, [filteredTransactions.length, pageSize]);

  const totalPages = Math.max(1, Math.ceil(filteredTransactions.length / pageSize));
  const safeCurrentPage = Math.min(currentPage, totalPages);
  const pagedTransactions = filteredTransactions.slice((safeCurrentPage - 1) * pageSize, safeCurrentPage * pageSize);

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
                <div className="col-xl-7">
                  <Link className="back-left-arrow" to="/dashboard">
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z" fill="currentColor" />
                    </svg>
                    Back to dashboard
                  </Link>
                  <h3 className="alt mt-4">Personal finance transactions</h3>
                  <p>Search, filter, and review your personal finance activity across imported and manual entries.</p>
                </div>
              </div>

              {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

              <div className="row align-items-end mb-3">
                <div className="col-xl-5">
                  <form onSubmit={(event) => event.preventDefault()}>
                    <div className="form-group">
                      <label className="mb-2">Search by merchant, account or reference</label>
                      <div className="input-group search-box">
                        <span className="input-group-text">
                          <svg width="25" height="25" viewBox="0 0 25 25" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M10.1499 10.1484L24.0015 24.002" stroke="#B4BFC3" strokeWidth="2" />
                            <path d="M10 20C15.5228 20 20 15.5228 20 10C20 4.47715 15.5228 0 10 0C4.47715 0 0 4.47715 0 10C0 15.5228 4.47715 20 10 20Z" fill="white" />
                            <path d="M10 19C14.9706 19 19 14.9706 19 10C19 5.02944 14.9706 1 10 1C5.02944 1 1 5.02944 1 10C1 14.9706 5.02944 19 10 19Z" stroke="#B4BFC3" strokeWidth="2" />
                          </svg>
                        </span>
                        <input
                          type="text"
                          className="form-control"
                          placeholder="Search for a transaction (merchant, account or reference)"
                          value={searchQuery}
                          onChange={(event) => setSearchQuery(event.target.value)}
                        />
                        <input
                          type="reset"
                          className="search-close"
                          value="X"
                          onClick={() => setSearchQuery("")}
                        />
                      </div>
                    </div>
                  </form>
                </div>

                <div className="col-xl-4">
                  <form className="date-group" onSubmit={(event) => event.preventDefault()}>
                    <div className="form-group">
                      <div className="d-flex justify-content-between align-items-center mb-2">
                        <label className="mb-0">Set date range</label>
                        <button
                          className="btn btn-recet me-4"
                          type="button"
                          onClick={() => {
                            setStartDate("");
                            setEndDate("");
                          }}
                        >
                          Clear
                        </button>
                      </div>
                      <div className="input-group">
                        <input
                          type="date"
                          className="form-control border-end-0"
                          value={startDate}
                          onChange={(event) => setStartDate(event.target.value)}
                        />
                        <span className="input-group-text input-center-text">to</span>
                        <input
                          type="date"
                          className="form-control border-start-0"
                          value={endDate}
                          onChange={(event) => setEndDate(event.target.value)}
                        />
                      </div>
                    </div>
                  </form>
                </div>

                <div className="col-xl-3">
                  <form onSubmit={(event) => event.preventDefault()}>
                    <div className="form-group">
                      <div className="d-flex justify-content-between align-items-center mb-2">
                        <label className="mb-0" htmlFor="transaction-status-filter">Filter by status</label>
                        <button
                          className="btn btn-recet me-4"
                          type="button"
                          onClick={() => setStatusFilter("all")}
                        >
                          Clear
                        </button>
                      </div>
                      <select
                        id="transaction-status-filter"
                        className="form-control select-box"
                        value={statusFilter}
                        onChange={(event) => setStatusFilter(event.target.value as StatusFilter)}
                      >
                        <option value="all">All</option>
                        <option value="paid">Paid</option>
                        <option value="pending">Pending</option>
                        <option value="failed">Failed</option>
                      </select>
                    </div>
                  </form>
                </div>
              </div>

              <div className="card card-tbox h-100">
                <div className="card-body">
                  {isLoading ? (
                    <div className="alert alert-secondary mb-0">Loading transactions...</div>
                  ) : transactions.length === 0 ? (
                    <div className="alert alert-info mb-0">No transactions yet. Add one manually or import a statement to get started.</div>
                  ) : filteredTransactions.length === 0 ? (
                    <div className="alert alert-light mb-0">No transactions match your current filters.</div>
                  ) : (
                    <div className="table-responsive">
                      <table className="table table-card table-hover">
                        <thead>
                          <tr>
                            <th className="col py-2">BILLER</th>
                            <th className="col py-2">DATE</th>
                            <th className="col py-2">REFERENCE</th>
                            <th className="col py-2">PAYMENT METHOD</th>
                            <th className="col py-2 text-end">AMOUNT</th>
                            <th className="col py-2 text-end">STATUS</th>
                            <th className="col-icon py-2 text-center">&nbsp;</th>
                          </tr>
                        </thead>
                        <tbody>
                          {pagedTransactions.map((item) => {
                            const detailsUrl = `/payments/transaction-details?id=${encodeURIComponent(item.transaction.personalTransactionId)}`;

                            return (
                              <tr key={item.transaction.personalTransactionId}>
                                <td>
                                  <Link className="row-link" to={detailsUrl}></Link>
                                  <div className="d-flex align-items-center">
                                    <div className="img-td">
                                      <img src={item.imagePath} alt="" />
                                    </div>
                                    <div>
                                      <strong className="heading-td">{item.biller}</strong>
                                      <span className="info-td text-gray d-block">{item.detail}</span>
                                    </div>
                                  </div>
                                </td>
                                <td>{formatTableDate(item.transaction.occurredAt)}</td>
                                <td>{item.reference}</td>
                                <td>
                                  {item.paymentMethod}
                                  {item.account?.name ? <span className="info-td text-gray d-block">{item.account.name}</span> : null}
                                </td>
                                <td className="text-end">
                                  <strong>{formatAmountLabel(item.transaction.amount, item.transaction.currency)}</strong>
                                  <span className="info-td d-block">
                                    {item.transaction.category ? item.transaction.category : "Uncategorised"}
                                  </span>
                                </td>
                                <td className="text-end">
                                  <button type="button" className={`btn ${statusClassNames[item.status]}`}>
                                    {item.status}
                                  </button>
                                </td>
                                <td className="text-icon">
                                  <Link to={detailsUrl} className="d-inline-flex align-items-center" aria-label="Open transaction details">
                                    <svg width="9" height="14" viewBox="0 0 9 14" fill="none" xmlns="http://www.w3.org/2000/svg">
                                      <path d="M8.12097 6.707L1.41397 0L-2.86102e-05 1.414L5.29297 6.707L-2.86102e-05 12L1.41397 13.415L8.12097 6.707Z" fill="currentColor" />
                                    </svg>
                                  </Link>
                                </td>
                              </tr>
                            );
                          })}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              </div>

              {!isLoading && filteredTransactions.length > 0 ? (
                <div className="row">
                  <div className="col-md-6">
                    <ul className="pagination tbox-pagination mt-4">
                      <li className="page-item disabled ms-0">
                        <span className="page-link ps-0">Show</span>
                      </li>
                      <li className="page-item">
                        <select
                          className="form-control select-box"
                          value={pageSize}
                          onChange={(event) => setPageSize(Number(event.target.value))}
                        >
                          {pageSizeOptions.map((option) => (
                            <option key={option} value={option}>
                              {option}
                            </option>
                          ))}
                        </select>
                      </li>
                      <li className="page-item disabled">
                        <span className="page-link pe-0">items per page</span>
                      </li>
                    </ul>
                  </div>
                  <div className="col-md-6">
                    <ul className="pagination tbox-pagination mt-4 justify-content-end">
                      <li className="page-item me-4">
                        <button
                          type="button"
                          className="btn btn-secondary btn-sm"
                          onClick={() => setCurrentPage(1)}
                          disabled={safeCurrentPage <= 1}
                        >
                          FIRST PAGE
                        </button>
                      </li>
                      <li className="page-item">
                        <span className="page-link">{safeCurrentPage}</span>
                      </li>
                      <li className="page-item disabled">
                        <span className="page-link">of {totalPages}</span>
                      </li>
                      <li className="page-item">
                        <button
                          type="button"
                          className="btn btn-primary btn-sm"
                          onClick={() => setCurrentPage((value) => Math.min(value + 1, totalPages))}
                          disabled={safeCurrentPage >= totalPages}
                        >
                          NEXT
                        </button>
                      </li>
                    </ul>
                  </div>
                </div>
              ) : null}
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
