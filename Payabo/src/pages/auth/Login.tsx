export const Login = () => {
  return (
    <div className="fullscreen-xl">
      <button type="button" className="btn-close close"></button>
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
                <img className="mb-4" src="/images/logo.png" alt="MyBillAfrica" />
                <h4>Nice to see you again.</h4>
                <p>
                  Don't have an account? <a href="/register">Register now</a>
                </p>
              </div>
              <form action="#" method="post">
                <div className="form-group">
                  <label htmlFor="email-login">Email</label>
                  <input type="email" className="form-control" id="email-login" name="LoginEmail" placeholder="Your email address" />
                </div>
                <div className="form-group">
                  <label htmlFor="password-login">Password</label>
                  <div className="input-group">
                    <input type="password" className="form-control" id="password-login" name="LoginPassword" placeholder="Your password" />
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
                  <button type="submit" className="btn btn-primary w-100">
                    LOGIN
                  </button>
                </div>
                <div className="text-center">
                  <p>
                    <a href="#">Forgot your password?</a>
                  </p>
                  <p className="small">
                    By continuing you agree with <br /> <a href="#">our Terms and Conditions</a> and <a href="/privacy">Privacy Policy</a>.
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
