export const Register = () => {
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
                JOIN <br />
                <strong>PAYABO</strong>
              </h2>
              <p>Track bills, manage subscriptions, and pay securely from anywhere in the world.</p>
            </div>
          </div>
          <div className="col-lg-6">
            <div className="login-content">
              <div className="login-header text-center">
                <img className="mb-4" src="/images/logo.png" alt="MyBillAfrica" />
                <h4>Create your account.</h4>
                <p>
                  Already have an account? <a href="/login">Login now</a>
                </p>
              </div>
              <form action="#" method="post">
                <div className="form-group">
                  <label htmlFor="register-name">Full name</label>
                  <input type="text" className="form-control" id="register-name" name="RegisterName" placeholder="Your full name" />
                </div>
                <div className="form-group">
                  <label htmlFor="register-email">Email</label>
                  <input type="email" className="form-control" id="register-email" name="RegisterEmail" placeholder="Your email address" />
                </div>
                <div className="form-group">
                  <label htmlFor="register-password">Password</label>
                  <input type="password" className="form-control" id="register-password" name="RegisterPassword" placeholder="Create a password" />
                </div>
                <div className="py-4">
                  <button type="submit" className="btn btn-primary w-100">
                    REGISTER
                  </button>
                </div>
                <p className="small text-center">
                  By registering you agree with our <a href="#">Terms and Conditions</a> and <a href="/privacy">Privacy Policy</a>.
                </p>
              </form>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
