export const Features = () => {
  return (
    <main className="main-wrapper">
      <section className="section-top shape-u-left">
        <div className="container">
          <div className="row align-items-center justify-content-around">
            <div className="col-md-4 col-lg-5 col-xl-5 col-xxl-4">
              <img className="mx-auto d-block" src="/images/mba_phone.png" alt="App Screen" />
            </div>
            <div className="col-md-8 col-lg-6 col-xl-5 col-xxl-4">
              <h3>
                MANAGE <br />
                <strong className="text-primary">RECURRING BILLS</strong>
              </h3>
              <p>
                Payabo keeps your essentials on schedule. Set monthly or weekly bills once, get reminders ahead of time,
                and keep a clean record of every payment.
              </p>
              <p>
                From utilities and school fees to subscriptions, Payabo makes it easy to plan, pay, and track in one
                place.
              </p>
              <a className="btn btn-primary" href="#">
                GET STARTED
              </a>
            </div>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <div className="row">
            <div className="col-md-6 col-lg-4">
              <div className="round-ibox">
                <div className="icon-img">
                  <img src="/images/icon-education.png" alt="" />
                </div>
                <h4>Smart reminders</h4>
                <p>Know what is due, when it is due, and what is already paid. Payabo keeps your timeline clear.</p>
              </div>
            </div>
            <div className="col-md-6 col-lg-4">
              <div className="round-ibox">
                <div className="icon-img">
                  <img src="/images/icon-education.png" alt="" />
                </div>
                <h4>Budget guardrails</h4>
                <p>Set monthly limits and get gentle alerts before you overspend on everyday categories.</p>
              </div>
            </div>
            <div className="col-md-6 col-lg-4">
              <div className="round-ibox">
                <div className="icon-img">
                  <img src="/images/icon-education.png" alt="" />
                </div>
                <h4>Instant bill pay</h4>
                <p>Pay quickly and securely, then store receipts automatically for future reference.</p>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
};
