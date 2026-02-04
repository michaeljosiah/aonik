import { NavLink } from "react-router-dom";

export const HeaderMarketing = () => {
  return (
    <header className="header cd-morph-dropdown">
      <nav className="navbar navbar-expand-xl">
        <div className="container">
          <NavLink className="navbar-brand" to="/">
            <img src="/images/logo.png" alt="Logo" />
          </NavLink>
          <button className="nav-trigger" type="button">
            <span aria-hidden="true"></span>
          </button>
          <div className="main-nav navbar-collapse" id="navbar">
            <ul className="navbar-nav ms-auto align-items-center">
              <li className="nav-item has-dropdown features" data-content="features">
                <NavLink className="nav-link dropdown-toggle" to="/features" aria-haspopup="true" aria-expanded="false">
                  FEATURES
                </NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link" to="/get-app">
                  GET THE APP
                </NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link" to="/about">
                  ABOUT
                </NavLink>
              </li>
              <li className="nav-item">
                <NavLink className="nav-link" to="/help">
                  HELP
                </NavLink>
              </li>
              <li className="nav-item has-dropdown uk" data-content="uk">
                <a
                  className="nav-link dropdown-toggle d-flex align-items-center"
                  href="#"
                  aria-haspopup="true"
                  aria-expanded="false"
                >
                  <img className="rounded-circle me-2" src="/images/flag-round-UK.png" alt="United States" />
                  <span>United Kingdom</span>
                </a>
              </li>
            </ul>
            <ul className="navbar-nav navbar-btn">
              <li>
                <NavLink className="btn btn-secondary btn-sm" to="/login" target="_blank">
                  LOGIN
                </NavLink>
              </li>
              <li>
                <NavLink className="btn btn-primary btn-sm" to="/register" target="_blank">
                  REGISTER
                </NavLink>
              </li>
            </ul>
          </div>
        </div>
      </nav>
      <div className="morph-dropdown-wrapper">
        <div className="dropdown-list container p-xl-0">
          <ul>
            <li id="features" className="dropdown-nav features">
              <h4 className="label">FEATURES</h4>
              <div className="content">
                <div className="row">
                  <div className="col-md-6">
                    <NavLink className="navbar-promo-link link-active" to="/features-page#features-one">
                      <div className="media">
                        <i className="icon recurringbills"></i>
                        <div className="media-body">
                          <h6>MANAGE RECURRING BILLS</h6>
                          <p>Setup recurring bills and get reminders when they are due. Never forget to pay your bills again.</p>
                        </div>
                      </div>
                    </NavLink>
                  </div>
                  <div className="col-md-6">
                    <NavLink className="navbar-promo-link" to="/features-page#features-four">
                      <div className="media">
                        <i className="icon budget"></i>
                        <div className="media-body">
                          <h6>Budgeting</h6>
                          <p>Setup budgets and track your spending over time. Avoid overspending again with smart spending alerts.</p>
                        </div>
                      </div>
                    </NavLink>
                  </div>
                  <div className="col-md-6">
                    <NavLink className="navbar-promo-link" to="/features-page#features-two">
                      <div className="media">
                        <i className="icon insights"></i>
                        <div className="media-body">
                          <h6>Spending Insights</h6>
                          <p>Gain crucial insights into your spending. Understand where the majority of your money is going to and learn how to save.</p>
                        </div>
                      </div>
                    </NavLink>
                  </div>
                  <div className="col-md-6">
                    <NavLink className="navbar-promo-link" to="/features-page#features-five">
                      <div className="media">
                        <i className="icon community"></i>
                        <div className="media-body">
                          <h6>Community Payments</h6>
                          <p>Get assistance with payments from your loved ones and friends as well as the wider MyBillAfrica community.</p>
                        </div>
                      </div>
                    </NavLink>
                  </div>
                  <div className="col-md-6">
                    <NavLink className="navbar-promo-link" to="/features-page#features-three">
                      <div className="media">
                        <i className="icon crossborder"></i>
                        <div className="media-body">
                          <h6>Cross Border Bill Payments</h6>
                          <p>Pay your bills from anywhere in the world. We handle currency conversion and settle with the biller in local currency in real time.</p>
                        </div>
                      </div>
                    </NavLink>
                  </div>
                  <div className="col-md-6">
                    <NavLink className="navbar-promo-link" to="/features-page#features-six">
                      <div className="media">
                        <i className="icon recommendations"></i>
                        <div className="media-body">
                          <h6>Reminders & Recommendations</h6>
                          <p>Get regular recommendations on how to save money, view alternative service providers.</p>
                        </div>
                      </div>
                    </NavLink>
                  </div>
                </div>
              </div>
            </li>
            <li>
              <h4 className="label">
                <NavLink to="/get-app">GET THE APP</NavLink>
              </h4>
            </li>
            <li>
              <h4 className="label">
                <NavLink to="/about">ABOUT</NavLink>
              </h4>
            </li>
            <li>
              <h4 className="label">
                <NavLink to="/help">HELP</NavLink>
              </h4>
            </li>
            <li id="uk" className="dropdown-nav uk">
              <h4 className="d-flex align-items-center label">
                <img className="rounded-circle me-2" src="/images/flag-round-UK.png" alt="United States" /> United Kingdom
              </h4>
              <div className="content menu-content pb-2">
                <p className="mb-2">You're viewing content specific for:</p>
                <h6 className="text-primary mb-2">United Kingdom</h6>
                <p className="mb-0">
                  If you want to view content specific to other location, choose from the countries below.
                </p>
              </div>
              <hr />
              <a className="list-flag pt-2 d-flex align-items-center justify-content-between" href="#">
                <div className="d-flex align-items-center">
                  <img className="rounded-circle me-2" src="/images/flag-round-UK.png" alt="United States" />
                  <span>United Kingdom</span>
                </div>
              </a>
              <a className="list-flag pt-2 d-flex align-items-center justify-content-between" href="#">
                <div className="d-flex align-items-center">
                  <img className="rounded-circle me-2" src="/images/flag-round-UK.png" alt="United States" />
                  <span>Great Britain</span>
                </div>
              </a>
              <a className="list-flag pt-2 d-flex align-items-center justify-content-between" href="#">
                <div className="d-flex align-items-center">
                  <img className="rounded-circle me-2" src="/images/flag-round-UK.png" alt="United States" />
                  <span>United Kingdom</span>
                </div>
                <button type="button" className="btn">
                  Coming soon
                </button>
              </a>
              <a className="list-flag pt-2 d-flex align-items-center justify-content-between" href="#">
                <div className="d-flex align-items-center">
                  <img className="rounded-circle me-2" src="/images/flag-round-UK.png" alt="United States" />
                  <span>Great Britain</span>
                </div>
                <button type="button" className="btn">
                  Coming soon
                </button>
              </a>
            </li>
            <li>
              <NavLink className="label btn btn-secondary w-100" to="/login" target="_blank">
                LOGIN
              </NavLink>
            </li>
            <li>
              <NavLink className="label btn btn-primary w-100" to="/register" target="_blank">
                REGISTER
              </NavLink>
            </li>
          </ul>
          <div className="bg-layer" aria-hidden="true"></div>
        </div>
      </div>
    </header>
  );
};
