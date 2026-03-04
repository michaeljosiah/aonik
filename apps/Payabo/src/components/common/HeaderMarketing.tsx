import { useEffect, useState, type MouseEvent } from "react";
import { NavLink, useLocation } from "react-router-dom";

import {
  getSelectedOriginCountry,
  originCountries,
  setSelectedOriginCountryCode,
  type OriginCountry
} from "../../app/originCountry";
import { useAuth } from "../../app/auth/AuthContext";

export const HeaderMarketing = () => {
  const location = useLocation();
  const isSubHeader = location.pathname === "/features-page";
  const headerClassName = isSubHeader ? "header-sub cd-morph-dropdown" : "header cd-morph-dropdown";
  const { isAuthenticated } = useAuth();
  const [selectedOriginCountry, setSelectedOriginCountry] = useState<OriginCountry>(() => getSelectedOriginCountry());

  useEffect(() => {
    const syncOriginCountry = () => {
      setSelectedOriginCountry(getSelectedOriginCountry());
    };

    window.addEventListener("payabo:origin-country-changed", syncOriginCountry as EventListener);
    window.addEventListener("storage", syncOriginCountry);

    return () => {
      window.removeEventListener("payabo:origin-country-changed", syncOriginCountry as EventListener);
      window.removeEventListener("storage", syncOriginCountry);
    };
  }, []);

  const handleOriginCountryClick = (code: OriginCountry["code"]) => {
    return (event: MouseEvent<HTMLAnchorElement>) => {
      event.preventDefault();
      const nextOriginCountry = setSelectedOriginCountryCode(code);
      setSelectedOriginCountry(nextOriginCountry);
    };
  };

  const headerContent = (
    <>
      <nav className="navbar navbar-expand-xl">
        <div className="container">
          <NavLink className="navbar-brand" to="/">
            <img className="brand-logo-horizontal" src="/images/payabo_logo_horizontal.png" alt="Payabo" />
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
                  <img className="rounded-circle me-2" src={selectedOriginCountry.flagSrc} alt={selectedOriginCountry.name} width={24} height={24} />
                  <span>{selectedOriginCountry.name}</span>
                </a>
              </li>
            </ul>
            <ul className="navbar-nav navbar-btn">
              {isAuthenticated ? (
                <li>
                  <NavLink className="btn btn-primary btn-sm" to="/logout">
                    LOGOUT
                  </NavLink>
                </li>
              ) : (
                <>
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
                </>
              )}
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
                          <p>Set up recurring bills and get reminders before they are due. Never miss a payment again.</p>
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
                          <p>Set up budgets and track your spending over time. Get smart alerts before you overspend.</p>
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
                          <p>Gain clear insights into your spending. See where money goes and find practical ways to save.</p>
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
                          <p>Share bills with family or friends and track contributions in one place.</p>
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
                          <p>
                            Pay bills from anywhere in the world. We handle currency conversion and settle locally in
                            real time.
                          </p>
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
                          <p>Get proactive reminders and savings suggestions, plus alternative providers when it helps.</p>
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
                <img
                  className="rounded-circle me-2"
                  src={selectedOriginCountry.flagSrc}
                  alt={selectedOriginCountry.name}
                  width={24}
                  height={24}
                />{" "}
                {selectedOriginCountry.name}
              </h4>
              <div className="content menu-content pb-2">
                <p className="mb-2">You're viewing content specific for:</p>
                <h6 className="text-primary mb-2">{selectedOriginCountry.name}</h6>
                <p className="mb-0">If you want to view content specific to other location, choose from the countries below.</p>
              </div>
              <hr />
              {originCountries.map((country) => (
                <a
                  key={country.code}
                  className="list-flag pt-2 d-flex align-items-center justify-content-between"
                  href="#"
                  onClick={handleOriginCountryClick(country.code)}
                >
                  <div className="d-flex align-items-center">
                    <img className="rounded-circle me-2" src={country.flagSrc} alt={country.name} width={24} height={24} />
                    <span>{country.name}</span>
                  </div>
                  {country.code === selectedOriginCountry.code && <span className="text-primary small">Selected</span>}
                </a>
              ))}
            </li>
            {isAuthenticated ? (
              <li>
                <NavLink className="label btn btn-primary w-100" to="/logout">
                  LOGOUT
                </NavLink>
              </li>
            ) : (
              <>
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
              </>
            )}
          </ul>
          <div className="bg-layer" aria-hidden="true"></div>
        </div>
      </div>
    </>
  );

  return <header className={headerClassName}>{isSubHeader ? <div className="header-top">{headerContent}</div> : headerContent}</header>;
};
