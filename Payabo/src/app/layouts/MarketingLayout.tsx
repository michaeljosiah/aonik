import { Outlet } from "react-router-dom";

import { CookieAlert } from "../../components/common/CookieAlert";
import { Footer } from "../../components/common/Footer";
import { HeaderMarketing } from "../../components/common/HeaderMarketing";
import { Preloader } from "../../components/common/Preloader";

export const MarketingLayout = () => {
  return (
    <>
      <Preloader />
      <HeaderMarketing />
      <Outlet />
      <Footer />
      <CookieAlert />
    </>
  );
};
