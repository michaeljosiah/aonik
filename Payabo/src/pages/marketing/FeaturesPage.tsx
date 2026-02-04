export const FeaturesPage = () => {
  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container">
        <div className="row">
          <div className="col-lg-4 col-xl-3">
            <div className="main-sidebar pt-5 pe-0">
              <div className="navcolumn-sticky">
                <h4 className="title-xl text-gray">Features</h4>
                <div className="scroll-nav" id="side-navbar">
                  <a href="#features-one" className="scroll-nav-item d-flex align-items-center">
                    <i className="icon recurringbills"></i> MANAGE RECURRING BILLS
                  </a>
                  <a href="#features-two" className="scroll-nav-item d-flex align-items-center">
                    <i className="icon insights"></i> Spending Insights
                  </a>
                  <a href="#features-three" className="scroll-nav-item d-flex align-items-center">
                    <i className="icon crossborder"></i> Cross Border Bill Payments
                  </a>
                  <a href="#features-four" className="scroll-nav-item d-flex align-items-center">
                    <i className="icon budget"></i> Budgeting
                  </a>
                  <a href="#features-five" className="scroll-nav-item d-flex align-items-center">
                    <i className="icon community"></i> Community Payments
                  </a>
                  <a href="#features-six" className="scroll-nav-item d-flex align-items-center">
                    <i className="icon recommendations"></i> Recommendations
                  </a>
                </div>
              </div>
            </div>
          </div>
          <div className="col-lg-8 col-xl-9">
            <div data-bs-spy="scroll" data-bs-target="#side-navbar" tabIndex={0}>
              <section className="features-content-one" id="features-one">
                <div className="row align-items-center justify-content-center">
                  <div className="col-lg-7 col-xl-5 offset-xl-1">
                    <h3>
                      RECURRING <br />
                      <strong className="text-primary">BILLS</strong>
                    </h3>
                    <p>Set up recurring payments for your bills and never worry about late payments again.</p>
                    <p>
                      Recurring payments work well for utilities, memberships, insurance premiums, and any service that
                      depends on a predictable schedule.
                    </p>
                    <a className="btn btn-primary" href="#">
                      GET STARTED
                    </a>
                  </div>
                  <div className="col-lg-5 col-xl-5 text-center text-lg-right">
                    <img src="/images/mba_features_img_appscreen_recurringbills.png" alt="RECURRING BILLS" />
                  </div>
                </div>
              </section>
              <section className="features-content-two" id="features-two">
                <div className="row align-items-center justify-content-center">
                  <div className="col-lg-7 col-xl-5 order-lg-1">
                    <h3>
                      SPENDING <br />
                      <strong className="text-primary">INSIGHTS</strong>
                    </h3>
                    <p>Gain clear insights into your spending and take real control of your finances.</p>
                    <p>
                      Payabo provides built-in reports that show where money goes, how often bills recur, and where you
                      can save. We automatically track the bills and subscriptions you pay in Payabo.
                    </p>
                    <a className="btn btn-secondary" href="#">
                      GET STARTED
                    </a>
                  </div>
                  <div className="col-lg-5 col-xl-5 text-center text-lg-start">
                    <img src="/images/mba_features_img_appscreen_spendinginsights.png" alt="SPENDING INSIGHTS" />
                  </div>
                </div>
              </section>
              <section className="features-content-three" id="features-three">
                <div className="row align-items-center justify-content-center">
                  <div className="col-lg-7 col-xl-5 offset-xl-1">
                    <h3>
                      CROSS BORDER <br />
                      <strong className="text-primary">BILL PAYMENTS</strong>
                    </h3>
                    <p>
                      Pay your bills or those of your loved ones from anywhere in the world. Bills are settled directly
                      to the bill provider in real time.
                    </p>
                    <p>
                      Select the biller, enter your details, choose the amount, and pay. Payabo handles the currency
                      conversion and settlement with the provider.
                    </p>
                    <a className="btn btn-primary" href="#">
                      GET STARTED
                    </a>
                  </div>
                  <div className="col-lg-5 col-xl-5 text-center text-lg-right">
                    <img src="/images/mba_features_img_appscreen_crossborder.png" alt="CROSS BORDER BILL PAYMENTS" />
                  </div>
                </div>
              </section>
              <section className="features-content-four" id="features-four">
                <div className="row align-items-center justify-content-center">
                  <div className="col-lg-7 col-xl-6 order-lg-1">
                    <h3>
                      <strong className="text-primary">BUDGETING</strong>
                    </h3>
                    <p>Set budgets and track your spending over time. Avoid overspending with smart spending alerts.</p>
                    <h6 className="mb-2">Calculate your spending limits</h6>
                    <p>
                      Payabo reviews your transactions against your budget and helps you understand how much you have
                      available to spend.
                    </p>
                    <h6 className="mb-2">Create budgets for specific categories</h6>
                    <p>
                      Our spending breakdowns show historical patterns and make it easy to set goals where you want to
                      optimize.
                    </p>
                    <h6 className="mb-2">Get alerts to keep you informed and on track</h6>
                    <p>
                      Payabo automatically tracks your spending and will send alerts when you are nearing your desired
                      spending goals.
                    </p>
                    <a className="btn btn-secondary" href="#">
                      GET STARTED
                    </a>
                  </div>
                  <div className="col-lg-5 col-xl-5 text-center text-lg-start">
                    <img src="/images/mba_features_img_appscreen_budgeting.png" alt="BUDGETING" />
                  </div>
                </div>
              </section>
              <section className="features-content-five" id="features-five">
                <div className="row align-items-center justify-content-center">
                  <div className="col-lg-7 col-xl-6">
                    <h3>
                      COMMUNITY <br />
                      <strong className="text-primary">PAYMENTS</strong>
                    </h3>
                    <p>
                      Get assistance from friends, family members, or the wider Payabo community for help with urgent
                      bills.
                    </p>
                    <p>All funds raised from community payments are paid directly to the bill service provider.</p>
                    <h6 className="mb-2">Charity Campaigns</h6>
                    <p>
                      As a charity you can set up campaigns on Payabo for your members or the wider community to help
                      donate towards your campaign targets.
                    </p>
                    <h6 className="mb-2">Help from friends and family</h6>
                    <p>
                      If you are struggling to pay your bills, you can request assistance from your friends or family
                      directly on the platform.
                    </p>
                    <h6 className="mb-2">Help from the Payabo Community</h6>
                    <p>
                      With Payabo you can set up a bill donation page to get help from the community. For example, you can
                      set up a donation page for an expensive medical treatment, where donations are paid directly to the
                      hospital.
                    </p>
                    <a className="btn btn-primary" href="#">
                      GET STARTED
                    </a>
                  </div>
                  <div className="col-lg-5 col-xl-5 text-center">
                    <img src="/images/mba_features_img_appscreen_community.png" alt="COMMUNITY PAYMENTS" />
                  </div>
                </div>
              </section>
              <section className="features-content-six" id="features-six">
                <div className="row align-items-center justify-content-center">
                  <div className="col-lg-7 col-xl-6 order-lg-1">
                    <h3>
                      REMINDERS &amp; <br />
                      <strong className="text-primary">RECOMMENDATIONS</strong>
                    </h3>
                    <p>Get regular recommendations on how to save money and view alternative service providers.</p>
                    <p>
                      Payabo can monitor the amount of money you spend on certain types of bills and can recommend ways to
                      reduce your bill or even find cheaper providers.
                    </p>
                    <a className="btn btn-secondary" href="#">
                      GET STARTED
                    </a>
                  </div>
                  <div className="col-lg-5 col-xl-5 text-center text-lg-start">
                    <img src="/images/mba_features_img_appscreen_reminder.png" alt="REMINDERS &amp; RECOMMENDATIONS" />
                  </div>
                </div>
              </section>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
