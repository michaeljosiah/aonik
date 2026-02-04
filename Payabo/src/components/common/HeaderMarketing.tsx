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
                <a className="nav-link dropdown-toggle" href="#" aria-haspopup="true" aria-expanded="false">
                  FEATURES
                </a>
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
            </ul>
            <ul className="navbar-nav navbar-btn">
              <li>
                <NavLink className="btn btn-secondary btn-sm" to="/login">
                  LOGIN
                </NavLink>
              </li>
              <li>
                <NavLink className="btn btn-primary btn-sm" to="/register">
                  REGISTER
                </NavLink>
              </li>
            </ul>
          </div>
        </div>
      </nav>
    </header>
  );
};
