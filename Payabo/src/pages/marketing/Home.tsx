export const Home = () => {
  return (
    <main className="main-wrapper">
      <section
        className="section-hero fullscreen-img"
        style={{ backgroundImage: "url('/images/mba_homepage_img_header.jpg')" }}
      >
        <div className="container">
          <div className="row justify-content-between">
            <div className="col-lg-6 col-xl-5 col-xxl-4 pt-xl-5 mt-xl-5 pb-4">
              <h2 className="mb-3 mt-4">
                PAY YOUR BILLS <br />
                <strong>IN ONE PLACE</strong>
              </h2>
              <p className="mb-3">
                Pay your bills or those of your loved ones in Africa from anywhere in the world, directly to the bill
                provider. We cover medical bills, utility bills, energy bills and much more.
              </p>
              <p className="h4 mb-4 pb-3">Payment is instant, easy and secure.</p>
              <ul className="list-brand-logo">
                <li>
                  <img src="/images/logo-AIRTEL.png" alt="Airtel" />
                </li>
                <li>
                  <img src="/images/logo-DSTV.png" alt="Dstv" />
                </li>
                <li>
                  <img src="/images/logo-ETISALAT.png" alt="Etisalat" />
                </li>
                <li>
                  <img src="/images/logo-PHCN.png" alt="Phcn" />
                </li>
              </ul>
              <p className="h4">and much more...</p>
            </div>
            <div className="col-lg-5 col-xl-4 col-xxl-3 me-xl-5">
              <div className="card card-tbox min-h-auto">
                <div className="card-body">
                  <nav>
                    <div className="nav-tabs nav nav-fill">
                      <button className="nav-link active" type="button">
                        SEARCH BILL
                      </button>
                      <button className="nav-link" type="button">
                        PAY INVOICE
                      </button>
                    </div>
                  </nav>
                  <div className="tab-content">
                    <div className="tab-pane fade show active">
                      <form action="#" method="post">
                        <label htmlFor="countries" className="form-label">
                          Destination country
                        </label>
                        <div className="select mb-3">
                          <select className="form-control countries" id="countries">
                            <option value="GB">United Kingdom</option>
                            <option value="GH">Ghana</option>
                            <option value="NG">Nigeria</option>
                          </select>
                        </div>
                        <p className="text-md mb-4">
                          Note: Start by selecting the country you wish to pay a bill from.
                        </p>
                        <div className="text-center">
                          <button type="submit" className="btn btn-primary btn-sm">
                            GET STARTED
                          </button>
                        </div>
                      </form>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
        <a className="scroll-down" href="#discover">
          DISCOVER <br /> MORE
          <span className="bg-down-arrow">
            <svg width="20" height="12" viewBox="0 0 20 12" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0.707993 0.707001L9.70799 9.707L18.708 0.707001" stroke="#F37920" strokeWidth="2" />
            </svg>
          </span>
        </a>
      </section>

      <section className="section section-sm" id="discover">
        <div className="container">
          <div className="row pt-4">
            <div className="col-lg-4">
              <div className="services-item">
                <div className="services-item-img">
                  <img src="/images/icon-security.png" alt="Banking level security" />
                </div>
                <h3 className="alt mb-3">Banking level security</h3>
                <p>
                  We employ the latest banking security mechanisms to ensure that your transactions and details are
                  secure.
                </p>
              </div>
            </div>
            <div className="col-lg-4">
              <div className="services-item">
                <div className="services-item-img">
                  <img src="/images/icon-visa.png" alt="Verified by VISA" />
                </div>
                <h3 className="alt mb-3">Verified by VISA</h3>
                <p>Being verified by visa means your payment goes through enhanced security for that extra peace of mind.</p>
              </div>
            </div>
            <div className="col-lg-4">
              <div className="services-item">
                <div className="services-item-img">
                  <img src="/images/icon-team.png" alt="Team of professionals" />
                </div>
                <h3 className="alt mb-3">Team of professionals</h3>
                <p>MyBillAfrica is managed by a team of highly skilled professionals ensuring an unrivalled service.</p>
              </div>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
};
