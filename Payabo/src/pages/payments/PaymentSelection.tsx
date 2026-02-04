export const PaymentSelection = () => {
  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container">
        <div className="row">
          <div className="col-lg-4 col-xl-3">
            <div className="main-sidebar pt-5">
              <h4>Order summary</h4>
              <div className="list-group summery-sidebar pb-4">
                <div className="list-group-item">
                  <div className="d-flex justify-content-between mb-2">
                    <h4 className="alt mb-0">Destination country</h4>
                    <a className="text-underline small" href="#">
                      Edit
                    </a>
                  </div>
                  <div className="d-flex align-items-center">
                    <img className="rounded me-3" src="/images/flags/gb.svg" alt="" />
                    <h4 className="alt fw-normal mb-0">United Kingdom</h4>
                  </div>
                </div>
                <div className="list-group-item">
                  <div className="d-flex justify-content-between mb-2">
                    <h4 className="alt mb-0">Biller</h4>
                    <a className="text-underline small" href="#">
                      Edit
                    </a>
                  </div>
                  <div className="d-flex align-items-center">
                    <img className="me-3" src="/images/product-img-04.png" alt="" />
                    <h4 className="alt fw-normal mb-0">DStv</h4>
                  </div>
                </div>
                <div className="list-group-item">
                  <div className="d-flex justify-content-between mb-2">
                    <h4 className="alt mb-0">Service details</h4>
                    <a className="text-underline small" href="#">
                      Edit
                    </a>
                  </div>
                  <div className="d-flex align-items-center">
                    <p className="mb-0">
                      Montage Cable TV <br />
                      Card ID #123456789 <br />
                      ₦ 350.00 <br />
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>
          <div className="col-lg-8 col-xl-9">
            <div className="card card-pt">
              <div className="card-body">
                <h3 className="mb-3">Choose payment method</h3>
                <p>Select a saved card or add a new payment method to complete this bill payment.</p>
                <div className="d-flex gap-3">
                  <button className="btn btn-primary" type="button">
                    Pay with card
                  </button>
                  <button className="btn btn-secondary" type="button">
                    Invite a friend
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
