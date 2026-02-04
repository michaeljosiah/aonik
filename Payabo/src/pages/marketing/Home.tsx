import { useState, type MouseEvent } from "react";

import { recentNews } from "../../data/mockData";

export const Home = () => {
  const [activeTab, setActiveTab] = useState<"search" | "invoice">("search");

  const handleTabClick = (tab: "search" | "invoice", event: MouseEvent<HTMLAnchorElement>) => {
    event.preventDefault();
    setActiveTab(tab);
  };

  return (
    <main className="main-wrapper">
      <section
        className="section-hero fullscreen-img"
        style={{ backgroundImage: "url('/images/mba_homepage_img_header.jpg')" }}
      >
        <div className="container">
          <div className="row justify-content-between">
            <div className="col-lg-6 col-xl-5 col-xxl-4 pt-xl-5 mt-xl-5 pb-4">
              <h2 className="mb-3 mt-4">
                PAY YOUR BILLS <br />
                <strong>IN ONE PLACE</strong>
              </h2>
              <p className="mb-3">
                Pay your bills or those of your loved ones in Africa from anywhere in the world, directly to the bill
                provider. We cover medical bills, utility bills, energy bills and much more.
              </p>
              <p className="h4 mb-4 pb-3">Payment is instant, easy and secure.</p>
              <ul className="list-brand-logo">
                <li>
                  <img src="/images/logo-AIRTEL.png" alt="Airtel" />
                </li>
                <li>
                  <img src="/images/logo-DSTV.png" alt="Dstv" />
                </li>
                <li>
                  <img src="/images/logo-ETISALAT.png" alt="Etisalat" />
                </li>
                <li>
                  <img src="/images/logo-PHCN.png" alt="Phcn" />
                </li>
              </ul>
              <p className="h4">and much more...</p>
            </div>
            <div className="col-lg-5 col-xl-4 col-xxl-3 me-xl-5">
              <div className="card card-tbox min-h-auto">
                <div className="card-body">
                  <nav>
                    <div className="nav-tabs nav nav-fill">
                      <a
                        className={`nav-link ${activeTab === "search" ? "active" : ""}`}
                        data-bs-toggle="tab"
                        href="#tab-1"
                        onClick={(event) => handleTabClick("search", event)}
                      >
                        SEARCH BILL
                      </a>
                      <a
                        className={`nav-link ${activeTab === "invoice" ? "active" : ""}`}
                        data-bs-toggle="tab"
                        href="#tab-2"
                        onClick={(event) => handleTabClick("invoice", event)}
                      >
                        PAY INVOICE
                      </a>
                    </div>
                  </nav>
                  <div className="tab-content">
                    <div className={`tab-pane fade ${activeTab === "search" ? "show active" : ""}`} id="tab-1">
                      <form action="#" method="post">
                        <label htmlFor="countries" className="form-label">
                          Destination country
                        </label>
                        <div className="select mb-3">
                          <select className="form-control countries" id="countries">
                            <option value="GB" data-capital="London">
                              United Kingdom
                            </option>
                            <option value="GH" data-capital="Accra">
                              Ghana
                            </option>
                            <option value="NG" data-capital="Abuja">
                              Nigeria
                            </option>
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
                          Note: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt
                          ut labore.
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
        </div>
        <a className="scroll-down" href="#discover">
          DISCOVER <br /> MORE
          <span className="bg-down-arrow">
            <svg width="20" height="12" viewBox="0 0 20 12" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0.707993 0.707001L9.70799 9.707L18.708 0.707001" stroke="#F37920" strokeWidth="2" />
            </svg>
          </span>
        </a>
      </section>

      <section className="section section-sm" id="discover">
        <div className="container">
          <div className="row pt-4">
            <div className="col-lg-4">
              <div className="services-item">
                <div className="services-item-img">
                  <img src="/images/icon-security.png" alt="Banking level security" />
                </div>
                <h3 className="alt mb-3">Banking level security</h3>
                <p>
                  We employ the latest banking security mechanisms to ensure that your transactions and details are
                  secure.
                </p>
              </div>
            </div>
            <div className="col-lg-4">
              <div className="services-item">
                <div className="services-item-img">
                  <img src="/images/icon-visa.png" alt="Verified by VISA" />
                </div>
                <h3 className="alt mb-3">Verified by VISA</h3>
                <p>Being verified by visa means your payment goes through enhanced security for that extra peace of mind.</p>
              </div>
            </div>
            <div className="col-lg-4">
              <div className="services-item">
                <div className="services-item-img">
                  <img src="/images/icon-team.png" alt="Team of professionals" />
                </div>
                <h3 className="alt mb-3">Team of professionals</h3>
                <p>MyBillAfrica is managed by a team of highly skilled professionals ensuring an unrivalled service.</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      <div className="container pb-4">
        <section className="section-app-content bg-secondary">
          <div className="row align-items-center justify-content-around">
            <div className="col-lg-6 col-xl-4 offset-xl-1">
              <h3 className="title-lg">
                STAY ORGANISED <br />
                <strong className="text-primary">
                  Never miss <br /> a bill again
                </strong>
              </h3>
              <p>
                We provide tools to help you manage all your bills in one place. With renewal reminders and auto payment
                you will always be in control.
              </p>
              <a className="btn btn-primary" href="#">
                GET STARTED
              </a>
            </div>
            <div className="col-lg-6 col-xl-6">
              <img src="/images/mba_homepage_img_appscreen_providerslist.png" alt="appscreen providerslist" />
            </div>
          </div>
        </section>
      </div>

      <div className="container pb-lg-5">
        <section className="section-app-content">
          <div className="row align-items-center justify-content-around">
            <div className="col-lg-6 col-xl-4 order-lg-1">
              <h3 className="title-lg">
                DON'T GET LOST <br />
                <strong className="text-primary">
                  KEEP TRACK OF <br /> YOUR SPENDING
                </strong>
              </h3>
              <p>
                We provide tools to help you stay on top of your spending. Manage your outgoings directly from your
                account, set budgets and view quarterly reports.
              </p>
              <a className="btn btn-primary" href="#">
                GET STARTED
              </a>
            </div>
            <div className="col-lg-6 col-xl-5">
              <img src="/images/mba_homepage_img_appscreen_budget.png" alt="appscreen budget" />
            </div>
          </div>
        </section>
      </div>

      <div className="container">
        <section className="section-app-content app-shadow">
          <div className="row align-items-center justify-content-around">
            <div className="col-lg-6 col-xl-4 offset-xl-1">
              <h3 className="title-lg">
                SHORT OF FUNDS? <br />
                <strong className="text-primary">
                  REQUEST HELP <br /> TO PAY A BILL
                </strong>
              </h3>
              <p>You can easily request for help paying your bills from your family or friends anywhere in the world.</p>
              <a className="btn btn-primary" href="#">
                GET STARTED
              </a>
            </div>
            <div className="col-lg-6 col-xl-6">
              <img src="/images/mba_homepage_img_appscreen_help.png" alt="appscreen help" />
            </div>
          </div>
        </section>
      </div>

      <section className="section-sm">
        <div className="container">
          <div className="row text-center justify-content-center mb-4">
            <div className="col-lg-7 col-xxl-6">
              <h3 className="title-lg">
                MBA <br />
                <strong className="text-primary">RECENT NEWS</strong>
              </h3>
              <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nunc sit amet ipsum ac eli.</p>
            </div>
          </div>
          <div className="row">
            {recentNews.map((news) => (
              <div key={news.id} className="col-md-6 col-xl-3 mb-5">
                <div className="card icard">
                  <div className="card-img">
                    <img className="card-img-top" src={news.image} alt="" />
                  </div>
                  <div className="card-body">
                    <h4>
                      <a href="#">{news.title}</a>
                    </h4>
                    <p>{news.excerpt}</p>
                  </div>
                  <div className="card-footer">
                    <a className="btn btn-primary btn-sm" href="#">
                      READ MORE
                    </a>
                    <ul className="social-share">
                      <li>
                        <span>SHARE:</span>
                      </li>
                      <li>
                        <a href="#" target="_blank" rel="noreferrer">
                          <svg width="18" height="18" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path
                              fillRule="evenodd"
                              clipRule="evenodd"
                              d="M5.66606 16.7489C12.4256 16.7489 16.1038 11.1802 16.1038 6.31119V5.81506C16.7986 5.28557 17.4028 4.6469 17.8931 3.92395C17.2269 4.21468 16.5252 4.41611 15.8063 4.52301C16.5453 4.05857 17.1056 3.35778 17.3959 2.53457C16.6775 2.93443 15.9083 3.23528 15.1093 3.42895C14.7729 3.05022 14.3594 2.74786 13.8966 2.54218C13.4337 2.3365 12.9322 2.23225 12.4256 2.23645C11.4547 2.25111 10.5276 2.64334 9.84099 3.32998C9.15435 4.01662 8.76212 4.94369 8.74744 5.91463C8.72482 6.18378 8.75874 6.45474 8.847 6.71C7.38444 6.63913 5.95395 6.25646 4.65128 5.58779C3.34861 4.91909 2.20388 3.97976 1.29375 2.8327C0.966555 3.40837 0.795161 4.05942 0.7965 4.72157C0.809207 5.32261 0.958956 5.91283 1.23434 6.4472C1.50972 6.98158 1.90347 7.44603 2.38556 7.80519C1.78882 7.7927 1.20624 7.62103 0.698063 7.30794C0.697033 8.15923 0.994939 8.98379 1.5398 9.63781C2.08466 10.2919 2.84187 10.7338 3.67931 10.8866C3.35568 10.9739 3.0199 11.0076 2.68538 10.9861C2.44902 11.002 2.21197 10.9681 1.98956 10.8866C2.23299 11.623 2.69774 12.2665 3.32038 12.7291C3.94302 13.1916 4.69322 13.4508 5.46863 13.4713C4.16147 14.4922 2.55295 15.0514 0.894375 15.0614C0.592712 15.077 0.290549 15.0428 0 14.9602C1.64503 16.1545 3.63347 16.7823 5.66606 16.7489Z"
                              fill="currentColor"
                            />
                          </svg>
                        </a>
                      </li>
                      <li>
                        <a href="#" target="_blank" rel="noreferrer">
                          <svg width="18" height="18" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path
                              fillRule="evenodd"
                              clipRule="evenodd"
                              d="M10.6441 17.8931V9.74194H13.4273L13.825 6.561H10.6441V4.57256C10.6441 3.67819 10.9422 2.98181 12.2348 2.98181H13.9223V0.0995625C13.5241 0.0995625 12.5329 0 11.4394 0C9.05388 0 7.36413 1.49119 7.36413 4.17487V6.56044H4.58032V9.74138H7.36357V17.8931H10.6441Z"
                              fill="currentColor"
                            />
                          </svg>
                        </a>
                      </li>
                    </ul>
                  </div>
                </div>
              </div>
            ))}
          </div>
          <div className="text-center pt-2 pb-4">
            <a className="btn btn-secondary btn-lg" href="#">
              VIEW ALL NEWS
            </a>
          </div>
        </div>
      </section>

      <section className="section pb-0">
        <div className="container">
          <div className="row justify-content-center">
            <div className="col-lg-12 col-xxl-10">
              <div className="app-content">
                <div className="row align-items-center">
                  <div className="col-lg-6 col-xl-5 offset-xl-1">
                    <h3 className="title-lg">
                      THE APP <br /> <strong className="text-primary">IN YOUR HANDS</strong>
                    </h3>
                    <p>
                      Stay on top of your bills on the go with the <br /> MyBillAfrica app.
                    </p>
                    <h4 className="title-app">Available on</h4>
                    <a className="mb-2 me-2" href="#">
                      <img src="/images/app-store.png" alt="App Store" />
                    </a>
                    <a className="mb-2" href="#">
                      <img src="/images/google-play.png" alt="Google Play" />
                    </a>
                  </div>
                  <div className="col-lg-5 col-xl-5">
                    <div className="position-relative pt-4 pt-lg-0">
                      <img className="bg-circle" src="/images/bg-circle.png" alt="" />
                      <div className="round-frame">
                        <img src="/images/mba_homepage_img_appinhand.png" alt="" />
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
};
