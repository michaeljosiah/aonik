import { Link } from "react-router-dom";

export const CommunityDetails = () => {
  return (
    <main className="main-wrapper">
      <section
        className="section-header fullscreen-img bg-img-top shape-u-left"
        style={{ backgroundImage: "url('/images/MBA_img_community-details.jpg')" }}
      >
        <div className="container">
          <div className="row justify-content-start">
            <div className="col-md-8 col-lg-6 col-xl-5 col-xxl-4 offset-xl-1">
              <h2>
                COMMUNITY <br />
                <strong>SUPPORT FUND</strong>
              </h2>
              <p>
                Help households stay current on essential bills. Contributions are routed to approved providers for
                transparent impact.
              </p>
              <Link className="btn btn-outline-light" to="/community">
                BACK TO COMMUNITY
              </Link>
            </div>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <div className="row g-4">
            <div className="col-lg-8">
              <div className="card icard h-100">
                <div className="card-body">
                  <h3 className="mb-3">About this fund</h3>
                  <p>
                    This fund supports urgent utility, education, and healthcare payments for vulnerable families. Every
                    payout is approved and tracked to ensure funds are used for the intended bill category.
                  </p>
                  <p className="mb-0">
                    Organizers share status updates each week so contributors can see exactly how pooled funds are
                    helping the community.
                  </p>
                </div>
              </div>
            </div>
            <div className="col-lg-4">
              <div className="card icard h-100">
                <div className="card-body">
                  <h4 className="mb-3">Fund progress</h4>
                  <div className="mb-3">
                    <strong className="d-block">GBP 120.50 raised</strong>
                    <span className="text-gray">of GBP 1,000 goal</span>
                  </div>
                  <div className="progress mb-4">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <Link className="btn btn-primary btn-sm" to="/community">
                    SHARE FUND
                  </Link>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
};
