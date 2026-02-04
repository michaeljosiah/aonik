export const About = () => {
  return (
    <main className="main-wrapper">
      <section
        className="section-header fullscreen-img shape-u-right"
        style={{ backgroundImage: "url('/images/MBA_img_about.jpg')" }}
      >
        <div className="container">
          <div className="row justify-content-start">
            <div className="col-md-8 col-lg-6 col-xl-5 col-xxl-4 offset-xl-1">
              <h2>
                EMPOWERING <br />
                <strong>EVERYDAY MONEY</strong>
              </h2>
              <p>
                Payabo helps people stay ahead of bills, budgets, and subscriptions with calm guidance and instant
                payments.
              </p>
            </div>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <div className="row align-items-xl-center no-gutters">
            <div className="col-lg-5 offset-xl-1 col-xl-4 col-xxl-4">
              <h3 className="mt-lg-5 mt-xl-0">
                WHAT IS <br />
                <strong className="text-primary">OUR GOAL</strong>
              </h3>
              <h4>Make money management simple, fast, and secure for everyday people and families.</h4>
              <p>
                We bring bills, subscriptions, and payments into one place, with reminders, receipts, and insights that
                reduce stress and help you plan ahead.
              </p>
            </div>
            <div className="offset-lg-1 col-lg-6 col-xxl-6 full-img-right">
              <img src="/images/MBA_img_diaspora.jpg" alt="Diaspora" />
            </div>
          </div>
          <div className="py-4">
            <div className="shape-round-center">
              <span className="gradient-box"></span>
            </div>
          </div>
          <div className="row align-items-xl-center no-gutters">
            <div className="order-lg-1 offset-lg-1 col-lg-5 offset-xl-1 col-xl-4 col-xxl-4">
              <h3 className="mt-lg-5">
                BUILT FOR <br />
                <strong className="text-primary">REAL LIFE</strong>
              </h3>
              <h4>Payabo adapts to how you live, from shared household bills to supporting family abroad.</h4>
              <p>
                Split payments, automate essentials, and get alerts before you overspend. Payabo stays in the background
                until you need it.
              </p>
            </div>
            <div className="col-lg-6 col-xxl-6 full-img-left">
              <img src="/images/MBA_img_family.jpg" alt="Family" />
            </div>
          </div>
        </div>
      </section>

      <section className="section pt-2">
        <div className="container">
          <div className="row justify-content-center">
            <div className="col-lg-12 col-xxl-10">
              <div className="app-ibox">
                <div className="row justify-content-around">
                  <div className="col-lg-6 col-xl-5 offset-xl-1">
                    <div className="app-inner-ibox text-center">
                      <img className="icon-phone" src="/images/icon_phone.jpg" alt="Phone" />
                      <h3>GET THE APP</h3>
                      <p>Manage bills, budgets, and reminders on the go. Your receipts and insights stay with you.</p>
                      <form className="form-phone" action="#" method="post">
                        <div className="input-group justify-content-center">
                          <input id="phone" className="form-control" name="phone" type="tel" />
                          <div className="input-group-append">
                            <button type="submit" className="input-group-text">
                              GET IT
                            </button>
                          </div>
                        </div>
                      </form>
                    </div>
                  </div>
                  <div className="col-lg-5 col-xl-5 offset-xl-1">
                    <div className="phone-frame">
                      <img src="/images/MBA_appscreen_01.png" alt="" />
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
};
