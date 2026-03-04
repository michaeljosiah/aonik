import { Outlet } from "react-router-dom";

import { UiScripts } from "../components/common/UiScripts";

export const App = () => {
  return (
    <>
      <UiScripts />
      <Outlet />
    </>
  );
};
