export const HeaderDashboard = () => {
  return (
    <header className="header-sub">
      <div className="header-top">
        <nav className="navbar navbar-expand-lg">
          <div className="container">
            <a className="navbar-brand" href="/">
              <img src="/images/logo.png" alt="Logo" />
            </a>
            <ul className="navbar-nav ml-auto">
              <li className="nav-item">
                <a className="nav-link" href="/help">
                  HELP &amp; SUPPORT
                </a>
              </li>
            </ul>
            <ul className="navbar-nav navbar-btn">
              <li>
                <a className="btn btn-primary btn-sm" href="/login">
                  LOGOUT
                </a>
              </li>
            </ul>
          </div>
        </nav>
      </div>
    </header>
  );
};
