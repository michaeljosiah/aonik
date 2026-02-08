import { useState } from "react";

import { SidebarNav } from "../../components/navigation/SidebarNav";
import {
  billCategories,
  destinationCountries,
  organisations,
  payAssistRequests,
  recentTransactions,
  upcomingBills
} from "../../data/mockData";

type BillTab = "search" | "invoice";

export const Dashboard = () => {
  const [activeTab, setActiveTab] = useState<BillTab>("invoice");

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
                <div className="col-md-6">
                  <h3 className="alt mt-4">Bill management area</h3>
                  <p>
                    This is your bill management area. Get insights into all your bills, check upcoming bills and pay new
                    bills all in this area.
                  </p>
                </div>
                <div className="col-md-6">
                  <div className="alert-dark-box deep-dark">
                    <h4 className="alart-title text-primary d-flex align-items-center mb-2">
                      <svg
                        className="me-3"
                        width="24"
                        height="24"
                        viewBox="0 0 24 24"
                        fill="none"
                        xmlns="http://www.w3.org/2000/svg"
                      >
                        <path
                          d="M12 24C18.6274 24 24 18.6274 24 12C24 5.37258 18.6274 0 12 0C5.37258 0 0 5.37258 0 12C0.00717187 18.6245 5.37553 23.9928 12 24ZM11 6C11 5.44772 11.4477 5.00002 12 5.00002C12.5523 5.00002 13 5.44772 13 6V14C13 14.5523 12.5523 15 12 15C11.4477 15 11 14.5523 11 14V6V6ZM12 18C12.5523 18 13 18.4477 13 19C13 19.5523 12.5523 20 12 20C11.4477 20 11 19.5523 11 19C11 18.4477 11.4477 18 12 18Z"
                          fill="currentColor"
                        />
                      </svg>
                      Billing payment error
                    </h4>
                    <p className="mb-3">
                      There is a problem with your order #2524, we need that you review all details.{" "}
                      <a href="/transactions">Review your order</a>
                    </p>
                  </div>
                </div>
              </div>
              <div className="row">
                <div className="col-xl-8 mb-4">
                  <div className="card card-tbox h-100">
                    <div className="card-body">
                      <div className="d-flex justify-content-between align-items-center mb-3">
                        <h4 className="mb-0">My upcoming bills</h4>
                        <a className="btn btn-link" href="/payments/providers">
                          View all
                        </a>
                      </div>
                      <div className="table-responsive">
                        <table className="table table-card table-hover">
                          <thead>
                            <tr>
                              <th className="col py-2">BILLER</th>
                              <th className="col py-2">DUE DATE</th>
                              <th className="col py-2 text-end">AMOUNT</th>
                              <th className="col-icon py-2 text-center">&nbsp;</th>
                            </tr>
                          </thead>
                          <tbody>
                            {upcomingBills.map((bill) => (
                              <tr key={bill.id}>
                                <td>
                                  <a className="row-link" href="/payments/providers"></a>
                                  <div className="d-flex align-items-center">
                                    <div className="img-td">
                                      <img src={bill.image} alt={bill.name} />
                                    </div>
                                    <div>
                                      <strong className="heading-td">{bill.name}</strong>
                                      <span className="info-td text-gray d-block">{bill.description}</span>
                                    </div>
                                  </div>
                                </td>
                                <td>{bill.dueDate}</td>
                                <td className="text-end">
                                  <strong>{bill.amount}</strong>
                                </td>
                                <td className="text-icon">
                                  <svg width="9" height="14" viewBox="0 0 9 14" fill="none" xmlns="http://www.w3.org/2000/svg">
                                    <path
                                      d="M8.12097 6.707L1.41397 0L-2.86102e-05 1.414L5.29297 6.707L-2.86102e-05 12L1.41397 13.415L8.12097 6.707Z"
                                      fill="currentColor"
                                    />
                                  </svg>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
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
                          <button
                            type="button"
                            className={`nav-link ${activeTab === "search" ? "active" : ""}`}
                            onClick={() => setActiveTab("search")}
                          >
                            SEARCH BILL
                          </button>
                          <button
                            type="button"
                            className={`nav-link ${activeTab === "invoice" ? "active" : ""}`}
                            onClick={() => setActiveTab("invoice")}
                          >
                            PAY INVOICE
                          </button>
                        </div>
                      </nav>
                      <div className="tab-content">
                        <div className={`tab-pane fade ${activeTab === "search" ? "show active" : ""}`} id="tab-1">
                          <form action="#" method="post">
                            <label htmlFor="countries" className="form-label">
                              Destination country
                            </label>
                            <div className="select mb-3">
                              <select className="form-control countries" id="countries" defaultValue={destinationCountries[0]?.code}>
                                {destinationCountries.map((country) => (
                                  <option key={country.code} value={country.code} data-capital={country.capital}>
                                    {country.name}
                                  </option>
                                ))}
                              </select>
                            </div>
                            <p className="text-md mb-4">
                              Note: Start by selecting the country you wish to pay a bill from.
                            </p>
                            <div className="text-center">
                              <button type="submit" className="btn btn-primary btn-sm">
                                GET STARTED
                              </button>
                            </div>
                          </form>
                        </div>
                        <div className={`tab-pane fade ${activeTab === "invoice" ? "show active" : ""}`} id="tab-2">
                          <form action="#" method="post">
                            <label htmlFor="invoice" className="form-label">
                              Invoice number
                            </label>
                            <div className="mb-3">
                              <input
                                type="text"
                                className="form-control"
                                name="InvoiceNumber"
                                id="invoice"
                                placeholder="Enter MBA invoice number"
                              />
                            </div>
                            <p className="text-md mb-3">
                              Note: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor
                              incididunt ut labore.
                            </p>
                            <div className="text-center">
                              <button type="submit" className="btn btn-primary btn-sm">
                                GET STARTED
                              </button>
                            </div>
                          </form>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
              <div className="row">
                <div className="col-xl-8 mb-4">
                  <div className="card card-tbox h-100">
                    <div className="card-body">
                      <div className="d-flex justify-content-between align-items-center mb-3">
                        <h4 className="mb-0">My recent transactions</h4>
                        <a className="btn btn-link" href="/transactions">
                          View all
                        </a>
                      </div>
                      <div className="table-responsive">
                        <table className="table table-card table-hover">
                          <thead>
                            <tr>
                              <th className="col py-2">BILLER</th>
                              <th className="col py-2">DATE</th>
                              <th className="col py-2 text-end">AMOUNT</th>
                              <th className="col-icon py-2 text-center">&nbsp;</th>
                            </tr>
                          </thead>
                          <tbody>
                            {recentTransactions.map((transaction) => (
                              <tr key={transaction.id}>
                                <td>
                                  <a className="row-link" href="/transactions"></a>
                                  <div className="d-flex align-items-center">
                                    <div className="img-td">
                                      <img src={transaction.image} alt={transaction.name} />
                                    </div>
                                    <div>
                                      <strong className="heading-td">{transaction.name}</strong>
                                      <span className="info-td text-gray d-block">{transaction.description}</span>
                                    </div>
                                  </div>
                                </td>
                                <td>{transaction.date}</td>
                                <td className="text-end">
                                  <strong>{transaction.amount}</strong>
                                  <span className="info-td text-primary d-block">{transaction.points}</span>
                                </td>
                                <td className="text-icon">
                                  <svg width="9" height="14" viewBox="0 0 9 14" fill="none" xmlns="http://www.w3.org/2000/svg">
                                    <path
                                      d="M8.12097 6.707L1.41397 0L-2.86102e-05 1.414L5.29297 6.707L-2.86102e-05 12L1.41397 13.415L8.12097 6.707Z"
                                      fill="currentColor"
                                    />
                                  </svg>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  </div>
                </div>
                <div className="col-xl-4 mb-4">
                  <div className="card card-tbox h-100">
                    <div className="card-body">
                      <div className="d-flex justify-content-between align-items-center mb-4">
                        <h4 className="mb-0">August budget</h4>
                        <a className="btn btn-link" href="#">
                          Reports
                        </a>
                      </div>
                      <div className="select round-select">
                        <select className="form-select" data-placeholder="All categories" id="categories" defaultValue="">
                          <option value=""></option>
                          {billCategories.map((category) => (
                            <option key={category.id} value={category.name} data-img={category.icon}>
                              {category.name}
                            </option>
                          ))}
                        </select>
                      </div>
                      <div className="py-3 text-center">
                        <h5 className="heding-sm text-gray">SPENT</h5>
                        <span className="h3 text-success fw-bold d-block mb-2">₦ 3,500.00</span>
                        <h5 className="heding-sm text-gray mb-1">OF</h5>
                        <span className="h4 mb-1 d-block">₦ 10,000.00</span>
                        <p className="mb-2">You are on track.</p>
                      </div>
                      <div className="text-center">
                        <button type="button" className="btn btn-primary btn-sm">
                          MANAGE BUDGET
                        </button>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
              <div className="row">
                <div className="col-xl-6 mb-4">
                  <div className="card card-tbox h-100">
                    <div className="card-body">
                      <div className="d-flex justify-content-between align-items-center mb-4">
                        <h4 className="mb-0">Organisations</h4>
                        <a className="btn btn-link" href="#">
                          View all
                        </a>
                      </div>
                      <div className="scroll-box">
                        {organisations.map((organisation) => (
                          <div className="post-box" key={organisation.id}>
                            <div className="row g-0">
                              <div className="col-md-4">
                                <img src={organisation.image} className="img-cover rounded-start" alt={organisation.title} />
                              </div>
                              <div className="col-md-8">
                                <div className="card-body position-relative">
                                  {organisation.badge ? <span className="badge bg-success">{organisation.badge}</span> : null}
                                  <h4 className="alt mb-0">{organisation.title}</h4>
                                  <p className="text-gray">{organisation.updatedOn}</p>
                                  <p>{organisation.description}</p>
                                </div>
                                <div className="card-footer">
                                  <a href="#">Find out more</a>
                                </div>
                              </div>
                            </div>
                          </div>
                        ))}
                      </div>
                    </div>
                  </div>
                </div>
                <div className="col-xl-6 mb-4">
                  <div className="card card-tbox h-100">
                    <div className="card-body">
                      <div className="d-flex justify-content-between align-items-center mb-3">
                        <h4 className="mb-0">Bill pay assist request</h4>
                        <a className="btn btn-link" href="#">
                          View all
                        </a>
                      </div>
                      <div className="table-responsive">
                        <table className="table table-card table-hover">
                          <thead>
                            <tr>
                              <th className="col py-2">FROM</th>
                              <th className="col py-2 text-end">AMOUNT</th>
                              <th className="col-icon py-2 text-center">&nbsp;</th>
                            </tr>
                          </thead>
                          <tbody>
                            {payAssistRequests.map((request) => (
                              <tr key={request.id}>
                                <td>
                                  <a className="row-link" href="#"></a>
                                  <div className="d-flex align-items-center">
                                    <div className="img-td">
                                      <img className="h-hidden" src="/images/product-img-03.png" alt="" />
                                      <img className="h-show rounded-circle" src={request.image} alt={request.requester} />
                                    </div>
                                    <div className="h-hidden">
                                      <strong className="heading-td">{request.requester}</strong>{" "}
                                      <span className="dot-info text-gray info-td">{request.timeAgo}</span>
                                      <span className="info-td d-block">{request.purpose}</span>
                                    </div>
                                    <div className="h-show">
                                      <strong className="heading-td">{request.biller}</strong>
                                      <span className="info-td text-gray d-block">{request.description}</span>
                                    </div>
                                  </div>
                                </td>
                                <td className="text-end">
                                  <strong>{request.amount}</strong>
                                  <span className="info-td text-gray d-block">{request.dueLabel}</span>
                                </td>
                                <td className="text-icon">
                                  <svg width="9" height="14" viewBox="0 0 9 14" fill="none" xmlns="http://www.w3.org/2000/svg">
                                    <path
                                      d="M8.12097 6.707L1.41397 0L-2.86102e-05 1.414L5.29297 6.707L-2.86102e-05 12L1.41397 13.415L8.12097 6.707Z"
                                      fill="currentColor"
                                    />
                                  </svg>
                                </td>
                              </tr>
                            ))}
                          </tbody>
                        </table>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
