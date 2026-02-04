import { Outlet } from "react-router-dom";

import { Footer } from "../../components/common/Footer";
import { HeaderDashboard } from "../../components/common/HeaderDashboard";
import { Preloader } from "../../components/common/Preloader";

export const DashboardLayout = () => {
  return (
    <>
      <Preloader />
      <HeaderDashboard />
      <Outlet />
      <Footer />
    </>
  );
};
