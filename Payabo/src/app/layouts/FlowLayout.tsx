import { NavLink, Outlet } from "react-router-dom";

import { Footer } from "../../components/common/Footer";
import { Preloader } from "../../components/common/Preloader";
import { ProgressBar } from "../../components/navigation/ProgressBar";

interface FlowLayoutProps {
  currentStep?: number;
  headerClassName?: string;
}

export const FlowLayout = ({ currentStep = 0, headerClassName }: FlowLayoutProps) => {
  return (
    <>
      <Preloader />
      <header className={`header-sub ${headerClassName ?? ""}`.trim()}>
        <div className="header-top">
          <nav className="navbar navbar-expand-lg">
            <div className="container">
              <NavLink className="navbar-brand" to="/">
                <img src="/images/logo.png" alt="Logo" />
              </NavLink>
              <ProgressBar currentStep={currentStep} />
              <button type="button" className="btn-close"></button>
            </div>
          </nav>
        </div>
      </header>
      <Outlet />
      <Footer />
    </>
  );
};
