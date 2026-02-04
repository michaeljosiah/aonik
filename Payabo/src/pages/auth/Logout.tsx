import { useEffect } from "react";
import { useNavigate } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";

export const Logout = () => {
  const navigate = useNavigate();
  const { logout } = useAuth();

  useEffect(() => {
    logout();
    navigate("/login", { replace: true });
  }, [logout, navigate]);

  return null;
};
