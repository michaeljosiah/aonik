import { useState, type FormEvent } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";

type LocationState = {
  from?: string;
};

export const Login = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const { login } = useAuth();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [email, setEmail] = useState("john.doe@example.com");
  const [password, setPassword] = useState("password");
  const from = (location.state as LocationState | null)?.from;

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      await login(email, password);
      navigate(from && from.startsWith("/") ? from : "/dashboard", { replace: true });
    } catch {
      setErrorMessage("Unable to sign in. Please check your credentials and try again.");
    } finally {
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
                HEADLINE <br />
                <strong>LOREM IPSUM</strong>
              </h2>
              <p>
                Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam pretium, augue luctus lobortis
                vestibulum, nunc leo luctus massa.
              </p>
            </div>
          </div>
          <div className="col-lg-6">
            <div className="login-content">
              <div className="login-header text-center">
                <img className="mb-4" src="/images/payabo_logo_horizontal.png" alt="Payabo" />
                <h4>Nice to see you again.</h4>
                <p>
                  Don't have an account? <NavLink to="/register" state={from ? { from } : undefined}>Register now</NavLink>
                </p>
              </div>
              <form action="#" method="post" onSubmit={handleSubmit}>
                {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}
                <div className="form-group">
                  <label htmlFor="email-login">Email</label>
                  <input
                    type="email"
                    className="form-control"
                    id="email-login"
                    name="LoginEmail"
                    placeholder="Your email address"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                  />
                </div>
                <div className="form-group">
                  <label htmlFor="password-login">Password</label>
                  <div className="input-group">
                    <input
                      type="password"
                      className="form-control"
                      id="password-login"
                      name="LoginPassword"
                      placeholder="Your password"
                      value={password}
                      onChange={(e) => setPassword(e.target.value)}
                      required
                    />
                    <div className="input-group-append">
                      <span className="input-group-text">
                        <i className="toggle-password icon-eye"></i>
                      </span>
                    </div>
                  </div>
                </div>
                <div className="form-check form-check-inline mt-3">
                  <input type="checkbox" className="form-check-input" id="loginCheck" name="example1" />
                  <label className="form-check-label mb-0" htmlFor="loginCheck">
                    Remember my login
                  </label>
                </div>
                <div className="py-4">
                  <button type="submit" className="btn btn-primary w-100" disabled={isSubmitting}>
                    {isSubmitting ? "SIGNING IN..." : "LOGIN"}
                  </button>
                </div>
                <div className="text-center">
                  <p>
                    <a href="#">Forgot your password?</a>
                  </p>
                  <p className="small">
                    By continuing you agree with <br /> <a href="#">our Terms and Conditions</a> and <NavLink to="/privacy">Privacy Policy</NavLink>.
                  </p>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
