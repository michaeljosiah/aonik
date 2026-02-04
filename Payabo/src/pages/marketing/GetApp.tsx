export const GetApp = () => {
  return (
    <main className="main-wrapper">
      <section className="section-banner" style={{ backgroundImage: "url('/images/banner-get-app.png')" }}>
        <div className="container">
          <div className="row align-items-center justify-content-center">
            <div className="col-lg-5 col-xl-6 col-xl-5 order-lg-1">
              <div className="phone-frame mx-lg-auto">
                <img src="/images/MBA_appscreen_01.png" alt="" />
              </div>
            </div>
            <div className="col-lg-7 col-xl-6 col-xxl-5">
              <h2 className="mb-3">
                THE APP <br />
                <strong className="text-primary">IN YOUR HANDS</strong>
              </h2>
              <p>
                Payabo keeps your bills, budgets, and reminders with you. Get alerts, pay fast, and keep receipts in one
                place.
              </p>
              <form className="form-phone" action="#" method="post">
                <div className="input-group">
                  <input id="phone" className="form-control" name="phone" type="tel" />
                  <div className="input-group-append">
                    <button type="submit" className="input-group-text">
                      GET IT
                    </button>
                  </div>
                </div>
              </form>
              <h4 className="title-app">Available on</h4>
              <a className="mb-2 me-2" href="#">
                <img src="/images/app-store.png" alt="App Store" />
              </a>
              <a className="mb-2" href="#">
                <img src="/images/google-play.png" alt="Google Play" />
              </a>
            </div>
          </div>
        </div>
      </section>

      <section className="section-sm bg-secondary pb-lg-0 mb-lg-100">
        <div className="container">
          <div className="row align-items-xl-center">
            <div className="col-lg-6 col-xl-6">
              <img className="img-shadow mb-lg-n100 mb-4 mb-lg-0" src="/images/section-app-img1.png" alt="" />
            </div>
            <div className="col-lg-5 offset-lg-1 offset-xl-1 col-xl-4">
              <h3>
                STAY ON TOP <br />
                <strong className="text-primary">OF EVERY BILL</strong>
              </h3>
              <p>
                See upcoming due dates, confirm what was paid, and share bills with family or friends. Payabo keeps your
                timeline clear and your payments organized.
              </p>
            </div>
          </div>
        </div>
      </section>

      <section className="section pt-lg-0">
        <div className="container">
          <div className="row align-items-center">
            <div className="col-lg-5 offset-lg-1 col-xl-5 offset-xl-1 order-lg-1">
              <div className="phone-frame mx-lg-auto mb-4 mb-lg-0">
                <img src="/images/phone-relax.jpg" alt="" />
              </div>
            </div>
            <div className="col-lg-6 offset-xl-1 col-xl-4 shape-round-top">
              <h3>
                BUDGET WITH <br />
                <strong className="text-primary">CONFIDENCE</strong>
              </h3>
              <p>
                Set monthly limits, track progress by category, and get helpful nudges before you overspend. Payabo helps
                you build better money habits without extra effort.
              </p>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
};
