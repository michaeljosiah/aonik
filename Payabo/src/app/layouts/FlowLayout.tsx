import { Outlet } from "react-router-dom";

import { ProgressBar } from "../../components/navigation/ProgressBar";

interface FlowLayoutProps {
  currentStep: number;
}

export const FlowLayout = ({ currentStep }: FlowLayoutProps) => {
  return (
    <>
      <header className="header-sub">
        <div className="header-top">
          <nav className="navbar navbar-expand-lg">
            <div className="container">
              <a className="navbar-brand" href="/">
                <img src="/images/logo.png" alt="Logo" />
              </a>
              <ProgressBar currentStep={currentStep} />
              <button type="button" className="btn-close"></button>
            </div>
          </nav>
        </div>
      </header>
      <Outlet />
    </>
  );
};
