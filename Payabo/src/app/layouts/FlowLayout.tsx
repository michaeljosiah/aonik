import { NavLink, Outlet } from "react-router-dom";
import { useAuth } from "../auth/AuthContext";

import { Footer } from "../../components/common/Footer";
import { Preloader } from "../../components/common/Preloader";
import { ProgressBar } from "../../components/navigation/ProgressBar";

interface FlowLayoutProps {
  currentStep?: number;
  headerClassName?: string;
  showUserPanel?: boolean;
}

export const FlowLayout = ({ currentStep = 0, headerClassName, showUserPanel = false }: FlowLayoutProps) => {
  const { user } = useAuth();

  return (
    <>
      <Preloader />
      <header className={`header-sub ${headerClassName ?? ""}`.trim()}>
        <div className="header-top">
          <nav className="navbar navbar-expand-lg">
            <div className="container">
              <NavLink className="navbar-brand" to="/">
                <img className="brand-logo-horizontal" src="/images/payabo_logo_horizontal.png" alt="Payabo" />
              </NavLink>
              <ProgressBar currentStep={currentStep} />
              <button type="button" className="btn-close"></button>
            </div>
          </nav>
        </div>
        {showUserPanel && (
          <div className="container">
            <div className="row align-items-center">
              <div className="col-10 col-md-7 col-lg-8">
                <div className="dropdown-h d-inline-block position-relative py-3">
                  <a
                    className="dropdown-user d-inline-flex align-items-center text-decoration-none"
                    href="#"
                    id="dropdownUser"
                    data-bs-toggle="dropdown"
                    aria-expanded="false"
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
                        <div>
                          <strong className="d-block">My personal details</strong>
                          <small>Edit your name, mobile number ...</small>
                        </div>
                      </NavLink>
                    </li>
                    <li>
                      <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/login-details">
                        <div>
                          <strong className="d-block">My login details</strong>
                          <small>Edit your email, password ...</small>
                        </div>
                      </NavLink>
                    </li>
                    <li>
                      <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/notifications">
                        <div>
                          <strong className="d-block">Notification settings</strong>
                          <small>Manage your notifications</small>
                        </div>
                      </NavLink>
                    </li>
                    <li>
                      <NavLink className="dropdown-user-item dropdown-item d-flex gap-2 align-items-center" to="/profile/marketing">
                        <div>
                          <strong className="d-block">Marketing preferences</strong>
                          <small>Manage marketing communication</small>
                        </div>
                      </NavLink>
                    </li>
                  </ul>
                </div>
              </div>
            </div>
          </div>
        )}
      </header>
      <Outlet />
      <Footer />
    </>
  );
};
