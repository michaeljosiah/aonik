import { Outlet } from "react-router-dom";

import { HeaderDashboard } from "../../components/common/HeaderDashboard";

export const DashboardLayout = () => {
  return (
    <>
      <HeaderDashboard />
      <Outlet />
    </>
  );
};
