export const Help = () => {
  return (
    <main className="main-wrapper">
      <section
        className="section-header fullscreen-img bg-img-top shape-u-left"
        style={{ backgroundImage: "url('/images/MBA_img_help.jpg')" }}
      >
        <div className="container">
          <div className="row justify-content-start">
            <div className="col-md-8 col-lg-6 col-xl-5 col-xxl-4 offset-xl-1">
              <h2>
                HELP &amp; <br />
                <strong>SUPPORT</strong>
              </h2>
              <p>Find answers about bills, budgets, and payments. We are here to help you move with confidence.</p>
            </div>
          </div>
        </div>
      </section>

      <section className="section shape-o-bottom">
        <div className="container">
          <div className="row text-center justify-content-center mb-4">
            <div className="col-lg-7 col-xxl-6">
              <h3>
                FREQUENTLY <br />
                <strong className="text-primary">ASKED QUESTIONS</strong>
              </h3>
              <p>Quick answers for the most common questions about using Payabo day to day.</p>
            </div>
          </div>
          <div className="row justify-content-center">
            <div className="col-lg-10 col-xxl-8">
              <div className="faq-accordion" id="faq">
                <div className="card">
                  <div className="card-header" id="heading1">
                    <h4 className="mb-0">
                      <button
                        className="btn btn-block text-left"
                        type="button"
                        data-bs-toggle="collapse"
                        data-bs-target="#collapse-1"
                        aria-expanded="true"
                        aria-controls="collapse-1"
                      >
                        How do I get started?
                      </button>
                    </h4>
                  </div>
                  <div id="collapse-1" className="collapse show" aria-labelledby="heading1" data-bs-parent="#faq">
                    <div className="card-body">
                      <p>
                        Create an account, choose your country, and add a biller. Payabo will guide you through the first
                        payment and set up reminders for future due dates.
                      </p>
                    </div>
                  </div>
                </div>
                <div className="card">
                  <div className="card-header" id="heading2">
                    <h4 className="mb-0">
                      <button
                        className="btn btn-block text-left collapsed"
                        type="button"
                        data-bs-toggle="collapse"
                        data-bs-target="#collapse-2"
                        aria-expanded="true"
                        aria-controls="collapse-2"
                      >
                        Is my data secure with Payabo?
                      </button>
                    </h4>
                  </div>
                  <div id="collapse-2" className="collapse" aria-labelledby="heading2" data-bs-parent="#faq">
                    <div className="card-body">
                      <p>
                        Yes. We use industry-standard security and payment protections, and we store receipts so you can
                        track what was paid and when.
                      </p>
                    </div>
                  </div>
                </div>
                <div className="card">
                  <div className="card-header" id="heading3">
                    <h4 className="mb-0">
                      <button
                        className="btn btn-block text-left collapsed"
                        type="button"
                        data-bs-toggle="collapse"
                        data-bs-target="#collapse-3"
                        aria-expanded="true"
                        aria-controls="collapse-3"
                      >
                        How do budgets work in Payabo?
                      </button>
                    </h4>
                  </div>
                  <div id="collapse-3" className="collapse" aria-labelledby="heading3" data-bs-parent="#faq">
                    <div className="card-body">
                      <p>
                        Create monthly limits by category, track progress automatically, and get alerts before you go
                        over. You can adjust budgets anytime as your needs change.
                      </p>
                    </div>
                  </div>
                </div>
                <div className="card">
                  <div className="card-header" id="heading4">
                    <h4 className="mb-0">
                      <button
                        className="btn btn-block text-left collapsed"
                        type="button"
                        data-bs-toggle="collapse"
                        data-bs-target="#collapse-4"
                        aria-expanded="true"
                        aria-controls="collapse-4"
                      >
                        What type of bills can I pay?
                      </button>
                    </h4>
                  </div>
                  <div id="collapse-4" className="collapse" aria-labelledby="heading4" data-bs-parent="#faq">
                    <div className="card-body">
                      <p>
                        You can pay common categories like utilities, internet, education, health, and subscriptions.
                        Availability depends on the biller list in each country.
                      </p>
                    </div>
                  </div>
                </div>
                <div className="card">
                  <div className="card-header" id="heading5">
                    <h4 className="mb-0">
                      <button
                        className="btn btn-block text-left collapsed"
                        type="button"
                        data-bs-toggle="collapse"
                        data-bs-target="#collapse-5"
                        aria-expanded="true"
                        aria-controls="collapse-5"
                      >
                        Can Payabo track subscriptions?
                      </button>
                    </h4>
                  </div>
                  <div id="collapse-5" className="collapse" aria-labelledby="heading5" data-bs-parent="#faq">
                    <div className="card-body">
                      <p>
                        Yes. Payabo keeps recurring charges in one list, reminds you before renewals, and helps you spot
                        services you no longer use.
                      </p>
                    </div>
                  </div>
                </div>
                <div className="card">
                  <div className="card-header" id="heading6">
                    <h4 className="mb-0">
                      <button
                        className="btn btn-block text-left collapsed"
                        type="button"
                        data-bs-toggle="collapse"
                        data-bs-target="#collapse-6"
                        aria-expanded="true"
                        aria-controls="collapse-6"
                      >
                        What are spending insights?
                      </button>
                    </h4>
                  </div>
                  <div id="collapse-6" className="collapse" aria-labelledby="heading6" data-bs-parent="#faq">
                    <div className="card-body">
                      <p>
                        We summarize your spending by category and time period so you can see trends, plan ahead, and
                        adjust your budgets with confidence.
                      </p>
                    </div>
                  </div>
                </div>
                <div className="card">
                  <div className="card-header" id="heading7">
                    <h4 className="mb-0">
                      <button
                        className="btn btn-block text-left collapsed"
                        type="button"
                        data-bs-toggle="collapse"
                        data-bs-target="#collapse-7"
                        aria-expanded="true"
                        aria-controls="collapse-7"
                      >
                        Can I send money using this service?
                      </button>
                    </h4>
                  </div>
                  <div id="collapse-7" className="collapse" aria-labelledby="heading7" data-bs-parent="#faq">
                    <div className="card-body">
                      <p>
                        Payabo is built for bill payments. For transfers, we will guide you to the right option as
                        features expand.
                      </p>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div className="section pb-0">
            <div className="row justify-content-center text-center">
              <div className="col-lg-10 col-xxl-8">
                <img className="mb-3" src="/images/icon-support.png" alt="" />
                <h3>
                  CONTACT SUPPORT <br />
                  <strong className="text-primary">WE'RE HERE TO HELP</strong>
                </h3>
                <p>
                  Available from 9am to 5pm <br /> (closed weekends and bank holidays)
                </p>
                <div className="row justify-content-center">
                  <div className="col-md-6 text-md-end">
                    <a className="btn btn-outline-gray mb-3" href="mailto:support@payabo.com">
                      <strong>Email</strong>
                      <br /> support@payabo.com
                    </a>
                  </div>
                  <div className="col-md-6 text-md-start">
                    <a className="btn btn-outline-gray mb-3" href="tel:+4402071835481">
                      <strong>Phone</strong>
                      <br /> +44(0) 207 183 5481
                    </a>
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
