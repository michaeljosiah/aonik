import React from "react";
import ReactDOM from "react-dom/client";
import { RouterProvider } from "react-router-dom";

import "./styles/css/bootstrap.min.css";
import "./styles/css/select2.min.css";
import "./styles/css/slick.css";
import "./styles/css/intlTelInput.css";
import "./styles/css/style.css";

import { router } from "./app/routes";
import { AuthProvider } from "./app/auth/AuthContext";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  </React.StrictMode>
);
