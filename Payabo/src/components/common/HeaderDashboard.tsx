import { type ReactNode } from "react";
import { NavLink } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";

type MenuIconProps = {
  viewBox: string;
  children: ReactNode;
};

const MenuIcon = ({ viewBox, children }: MenuIconProps) => (
  <svg width="24" height="24" viewBox={viewBox} fill="none" xmlns="http://www.w3.org/2000/svg">
    {children}
  </svg>
);

export const HeaderDashboard = () => {
  const { user } = useAuth();

  return (
    <header className="header-sub cd-morph-dropdown">
      <div className="header-top">
        <nav className="navbar navbar-expand-lg">
          <div className="container">
            <NavLink className="navbar-brand" to="/">
              <img className="brand-logo-horizontal" src="/images/payabo_logo_horizontal.png" alt="Payabo" />
            </NavLink>
            <button className="nav-trigger" type="button">
              <span aria-hidden="true"></span>
            </button>
            <div className="main-nav navbar-collapse" id="navbar">
              <ul className="navbar-nav ml-auto">
                <li className="nav-item">
                  <NavLink className="nav-link" to="/help">
                    HELP &amp; SUPPORT
                  </NavLink>
                </li>
              </ul>
              <ul className="navbar-nav navbar-btn">
                <li>
                  <NavLink className="btn btn-primary btn-sm" to="/logout">
                    LOGOUT
                  </NavLink>
                </li>
              </ul>
            </div>
          </div>
        </nav>
        <div className="morph-dropdown-wrapper">
          <div className="dropdown-list container p-lg-0">
            <ul>
              <li>
                <h4 className="label text-center">
                  <NavLink to="/help">HELP &amp; SUPPORT</NavLink>
                </h4>
              </li>
              <li>
                <NavLink className="label btn btn-primary w-100" to="/logout">
                  LOGOUT
                </NavLink>
              </li>
            </ul>
            <div className="bg-layer" aria-hidden="true"></div>
          </div>
        </div>
      </div>
      <div className="container">
        <div className="row align-items-center">
          <div className="col-10 col-md-7 col-lg-8">
            <div className="dropdown-h d-inline-block position-relative py-3">
              <a
                className="dropdown-user d-inline-flex align-items-center text-decoration-none"
                href="#"
                id="dropdownUser"
                data-bs-toggle="dropdown"
                aria-expanded="true"
              >
                <img src="/images/profile-pic.png" alt="User" />
                <div>
                  <strong className="d-block">Welcome, {user?.fullName ?? "John Doe"}</strong>
                  <small>Last login: 05:15 PM Friday, 19 August, 2022</small>
                </div>
              </a>
              <ul className="dropdown-user-menu dropdown-menu text-small" aria-labelledby="dropdownUser">
                <li>
                  <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/personal">
                    <div className="icon-left">
                      <MenuIcon viewBox="0 0 18 24">
                        <path d="M12,12A6,6,0,1,0,6,6a6,6,0,0,0,6,6ZM12,2A4,4,0,1,1,8,6a4,4,0,0,1,4-4Z" fill="currentColor" />
                        <path d="M12,14a9.01,9.01,0,0,0-9,9,1,1,0,1,0,2,0,7,7,0,0,1,14,0,1,1,0,0,0,2,0A9.01,9.01,0,0,0,12,14Z" fill="currentColor" />
                      </MenuIcon>
                    </div>
                    <div>
                      <strong className="d-block">My personal details</strong>
                      <small>Edit your name, mobile number ...</small>
                    </div>
                  </NavLink>
                </li>
                <li>
                  <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/login-details">
                    <div className="icon-left">
                      <MenuIcon viewBox="0 0 20 24">
                        <path d="M19,8.424V7A7,7,0,1,0,5,7V8.424A5,5,0,0,0,2,13v6a5.006,5.006,0,0,0,5,5H17a5.006,5.006,0,0,0,5-5V13A5,5,0,0,0,19,8.424ZM7,7A5,5,0,0,1,17,7V8H7ZM20,19a3,3,0,0,1-3,3H7a3,3,0,0,1-3-3V13a3,3,0,0,1,3-3H17a3,3,0,0,1,3,3Z" fill="currentColor" />
                        <path d="M12,14a1,1,0,0,0-1,1v2a1,1,0,0,0,2,0V15A1,1,0,0,0,12,14Z" fill="currentColor" />
                      </MenuIcon>
                    </div>
                    <div>
                      <strong className="d-block">My login details</strong>
                      <small>Edit your email, password ...</small>
                    </div>
                  </NavLink>
                </li>
                <li>
                  <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/notifications">
                    <div className="icon-left">
                      <MenuIcon viewBox="0 0 23.961 24">
                        <path d="M20.859,15.331l-3.772,6.155a5.235,5.235,0,0,1-3.87,2.477,5.315,5.315,0,0,1-.628.037,5.212,5.212,0,0,1-3-.955A4.741,4.741,0,0,1,2.9,16.479L1.585,15.166a5.264,5.264,0,0,1,.955-8.2L8.307,3.4a8.859,8.859,0,0,1,10.327.551l1.659-1.659a1,1,0,1,1,1.414,1.414L20.05,5.364a8.951,8.951,0,0,1,.809,9.967ZM8.065,21.647l-3.719-3.72a2.721,2.721,0,0,0,.463,3.264A2.827,2.827,0,0,0,8.065,21.647Zm9.921-15.6A6.887,6.887,0,0,0,9.369,5.1L3.592,8.666A3.265,3.265,0,0,0,3,13.752l7.29,7.291a3.265,3.265,0,0,0,5.093-.6l3.755-6.125a6.937,6.937,0,0,0-1.152-8.276ZM19.265,24a1,1,0,0,1-.591-1.808,8.633,8.633,0,0,0,3.315-5.407,1,1,0,1,1,1.953.43,10.7,10.7,0,0,1-4.088,6.593,1,1,0,0,1-.589.192ZM1,5.739A1,1,0,0,1,.2,4.145,10.692,10.692,0,0,1,6.913.02a1,1,0,0,1,.4,1.96A8.636,8.636,0,0,0,1.8,5.334a1,1,0,0,1-.8.405Z" fill="currentColor" />
                      </MenuIcon>
                    </div>
                    <div>
                      <strong className="d-block">Notification settings</strong>
                      <small>Manage your notifications</small>
                    </div>
                  </NavLink>
                </li>
                <li>
                  <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/marketing">
                    <div className="icon-left">
                      <MenuIcon viewBox="0 0 24.006 24">
                        <path d="M17,0a1,1,0,0,0-1,1c0,2.949-2.583,4-5,4H4A4,4,0,0,0,0,9v2a3.979,3.979,0,0,0,1.514,3.109l3.572,7.972A3.233,3.233,0,0,0,8.039,24a2.982,2.982,0,0,0,2.72-4.2L8.559,15H11c2.417,0,5,1.051,5,4a1,1,0,0,0,2,0V1a1,1,0,0,0-1-1ZM8.937,20.619A.983.983,0,0,1,8.039,22a1.232,1.232,0,0,1-1.126-.734L4.105,15H6.359ZM16,14.6A7.723,7.723,0,0,0,11,13H4a2,2,0,0,1-2-2V9A2,2,0,0,1,4,7h7a7.723,7.723,0,0,0,5-1.6Zm7.9.852a1,1,0,0,1-1.342.448l-2-1a1,1,0,1,1,.894-1.79l2,1a1,1,0,0,1,.448,1.337Zm-3.79-9a1,1,0,0,1,.448-1.342l2-1a1,1,0,0,1,.894,1.79l-2,1A1,1,0,0,1,20.11,6.452ZM20,10a1,1,0,0,1,1-1h2a1,1,0,0,1,0,2H21A1,1,0,0,1,20,10Z" fill="currentColor" />
                      </MenuIcon>
                    </div>
                    <div>
                      <strong className="d-block">Marketing preferences</strong>
                      <small>Manage marketing communication</small>
                    </div>
                  </NavLink>
                </li>
              </ul>
            </div>
          </div>
          <div className="col-2 col-md-5 col-lg-4 text-md-end">
            <div className="envelope d-inline-flex align-items-center py-3">
              <p className="mb-0 d-none d-md-block">
                You have <strong className="text-primary">6</strong> new messages.
              </p>
              <button type="button" className="btn btn-inbox">
                <svg
                  width="24"
                  height="24"
                  viewBox="0 0 24 24"
                  fill="currentColor"
                  xmlns="http://www.w3.org/2000/svg"
                >
                  <path
                    d="M19 1H5C3.6744 1.00159 2.40356 1.52888 1.46622 2.46622C0.528882 3.40356 0.00158786 4.6744 0 6L0 18C0.00158786 19.3256 0.528882 20.5964 1.46622 21.5338C2.40356 22.4711 3.6744 22.9984 5 23H19C20.3256 22.9984 21.5964 22.4711 22.5338 21.5338C23.4711 20.5964 23.9984 19.3256 24 18V6C23.9984 4.6744 23.4711 3.40356 22.5338 2.46622C21.5964 1.52888 20.3256 1.00159 19 1V1ZM5 3H19C19.5988 3.00118 20.1835 3.18151 20.679 3.5178C21.1744 3.85409 21.5579 4.33095 21.78 4.887L14.122 12.546C13.5584 13.1073 12.7954 13.4225 12 13.4225C11.2046 13.4225 10.4416 13.1073 9.878 12.546L2.22 4.887C2.44215 4.33095 2.82561 3.85409 3.32105 3.5178C3.81648 3.18151 4.40121 3.00118 5 3V3ZM19 21H5C4.20435 21 3.44129 20.6839 2.87868 20.1213C2.31607 19.5587 2 18.7956 2 18V7.5L8.464 13.96C9.40263 14.8963 10.6743 15.422 12 15.422C13.3257 15.422 14.5974 14.8963 15.536 13.96L22 7.5V18C22 18.7956 21.6839 19.5587 21.1213 20.1213C20.5587 20.6839 19.7956 21 19 21Z"
                    fill="currentColor"
                  />
                </svg>
                <span className="alerts-envelope">
                  <span className="visually-hidden">New Massage</span>
                </span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </header>
  );
};
