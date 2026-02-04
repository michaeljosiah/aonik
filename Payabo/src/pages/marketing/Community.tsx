export const Community = () => {
  return (
    <main className="main-wrapper">
      <section
        className="section-header fullscreen-img bg-img-top shape-u-left"
        style={{ backgroundImage: "url('/images/MBA_img_community.jpg')" }}
      >
        <div className="container">
          <div className="row justify-content-start">
            <div className="col-md-8 col-lg-6 col-xl-5 col-xxl-4 offset-xl-1">
              <h2>
                SUPPORT YOUR <br />
                <strong>COMMUNITY</strong>
              </h2>
              <p>Start or support community funds that keep essential bills paid for families and local projects.</p>
            </div>
          </div>
        </div>
      </section>

      <section className="py0">
        <div className="container">
          <div className="section-community">
            <div className="row">
              <div className="col-6 col-sm-4 col-lg-2">
                <div className="service-box">
                  <div className="icon">
                    <img src="/images/icon-education.png" alt="Education" />
                  </div>
                  <h4>
                    <a href="#">Education</a>
                  </h4>
                </div>
              </div>
              <div className="col-6 col-sm-4 col-lg-2">
                <div className="service-box">
                  <div className="icon">
                    <img src="/images/icon-health.png" alt="Health" />
                  </div>
                  <h4>
                    <a href="#">Health</a>
                  </h4>
                </div>
              </div>
              <div className="col-6 col-sm-4 col-lg-2">
                <div className="service-box">
                  <div className="icon">
                    <img src="/images/icon-donations.png" alt="Donations" />
                  </div>
                  <h4>
                    <a href="#">Donations</a>
                  </h4>
                </div>
              </div>
              <div className="col-6 col-sm-4 col-lg-2">
                <div className="service-box">
                  <div className="icon">
                    <img src="/images/icon-charities.png" alt="Charities" />
                  </div>
                  <h4>
                    <a href="#">Charities</a>
                  </h4>
                </div>
              </div>
              <div className="col-6 col-sm-4 col-lg-2">
                <div className="service-box">
                  <div className="icon">
                    <img src="/images/icon-sports.png" alt="Sports" />
                  </div>
                  <h4>
                    <a href="#">Sports</a>
                  </h4>
                </div>
              </div>
              <div className="col-6 col-sm-4 col-lg-2">
                <div className="service-box">
                  <div className="icon">
                    <img src="/images/icon-environment.png" alt="Environment" />
                  </div>
                  <h4>
                    <a href="#">Environment</a>
                  </h4>
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className="section">
        <div className="container">
          <div className="row align-items-end justify-content-between">
            <div className="col-lg-6 col-xl-5 mb-4">
              <h3 className="mb-3">
                FIND A <br />
                <strong className="text-primary">COMMUNITY FUND</strong>
              </h3>
            </div>
            <div className="col-lg-6 col-xl-5 mb-5">
              <form className="search-form" method="get">
                <div className="input-group">
                  <div className="input-group-prepend">
                    <button className="input-group-text" type="submit">
                      <i className="fas fa-search"></i>
                    </button>
                  </div>
                  <input type="text" className="form-control" placeholder="Search..." />
                </div>
              </form>
            </div>
          </div>
          <div className="row">
            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="community-details.html">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="community-details.html">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <strong className="text-green">GOAL ACHIEVED!</strong>
                      <span className="text-green d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress active">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "100%" }}
                      aria-valuenow={100}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>

            <div className="col-md-6 col-lg-4 col-xxl-3 mb-5">
              <div className="card icard">
                <div className="card-img">
                  <img className="card-img-top" src="/images/card-img-01.png" alt="" />
                </div>
                <div className="card-body">
                  <h4>
                    <a href="#">Community support fund</a>
                  </h4>
                  <p>Support urgent bills and community projects. Payments go directly to providers.</p>
                </div>
                <div className="card-footer">
                  <div className="row align-items-end">
                    <div className="col-5">
                      <strong className="text-gray">RAISED</strong>
                      <strong className="d-block">GBP 120.50</strong>
                    </div>
                    <div className="col-7 text-right">
                      <span className="text-gray d-block">of GBP 1,000 goal</span>
                    </div>
                  </div>
                  <div className="progress">
                    <div
                      className="progress-bar"
                      role="progressbar"
                      style={{ width: "25%" }}
                      aria-valuenow={25}
                      aria-valuemin={0}
                      aria-valuemax={100}
                    ></div>
                  </div>
                  <a className="btn btn-primary btn-sm" href="#">
                    DETAILS
                  </a>
                  <ul className="social-share">
                    <li>
                      <span>SHARE:</span>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-twitter"></i>
                      </a>
                    </li>
                    <li>
                      <a href="#" target="_blank" rel="noreferrer">
                        <i className="fab fa-facebook-f"></i>
                      </a>
                    </li>
                  </ul>
                </div>
              </div>
            </div>
          </div>
          <div className="text-center pt-2 pb-4">
            <a className="btn btn-secondary btn-lg" href="#">
              LOAD MORE...
            </a>
          </div>
        </div>
      </section>
    </main>
  );
};
