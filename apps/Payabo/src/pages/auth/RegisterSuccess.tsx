import { useMemo, useState } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";

type RegisterSuccessState = {
  email?: string;
  returnTo?: string;
};

const normalizeReturnTo = (value?: string) => {
  if (!value) {
    return "/dashboard";
  }

  return value.startsWith("/") ? value : "/dashboard";
};

export const RegisterSuccess = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const state = location.state as RegisterSuccessState | null;
  const email = state?.email?.trim() || "";
  const returnTo = useMemo(() => normalizeReturnTo(state?.returnTo), [state?.returnTo]);

  const handleLogin = async () => {
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      await login({
        returnTo,
        loginHint: email || undefined,
        prompt: "login"
      });
    } catch {
      setErrorMessage("Unable to start secure sign in. Please try again.");
      setIsSubmitting(false);
    }
  };

  const handleClose = () => {
    navigate("/", { replace: true });
  };

  return (
    <div className="fullscreen-xl">
      <button type="button" className="btn-close close" aria-label="Close" onClick={handleClose}></button>
      <div className="container">
        <div
          className="img-lg-full-left py-3 d-none d-lg-block"
          style={{ backgroundImage: "url('/images/MBA_img_login_reg.jpg')" }}
        ></div>
      </div>
      <div className="container-fluid">
        <div className="row align-items-center">
          <div className="col-lg-6 d-lg-flex align-items-end min-vh-lg-100">
            <div className="info-box d-none d-lg-block text-white">
              <h2>
                WELCOME TO <br />
                <strong>PAYABO</strong>
              </h2>
              <p>
                Your account has been created successfully. Continue to login and start managing bills, subscriptions,
                and payments in one place.
              </p>
            </div>
          </div>
          <div className="col-lg-6">
            <div className="login-content">
              <div className="login-header text-center">
                <img className="mb-4" src="/images/payabo_logo_horizontal.png" alt="Payabo" />
                <h4>Registration successful</h4>
                <p>Welcome to the platform. Use the button below to login securely.</p>
                {email && <p className="small text-muted mb-0">Account: {email}</p>}
              </div>

              {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}

              <div className="py-4">
                <button type="button" className="btn btn-primary w-100" onClick={handleLogin} disabled={isSubmitting}>
                  {isSubmitting ? "REDIRECTING..." : "LOGIN"}
                </button>
              </div>

              <div className="text-center">
                <p className="small">
                  Already signed in on another tab? <NavLink to={returnTo}>Continue</NavLink>
                </p>
                <p className="small">
                  Need to create another account? <NavLink to="/register">Register again</NavLink>
                </p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
