import { Link } from "react-router-dom";

export const CardDetails = () => {
  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4 py-lg-5">
        <div className="mb-4">
          <Link to="/manage-cards" className="small text-decoration-none">← Back to cards</Link>
          <h3 className="alt mt-2 mb-1">Card details</h3>
          <p className="text-muted mb-0">Manage security and billing preferences for this card.</p>
        </div>

        <section className="card border-0 shadow-sm mb-3">
          <div className="card-body">
            <h5 className="mb-3">Visa •••• 4921</h5>
            <div className="row g-3">
              <div className="col-md-4"><strong>Status:</strong> Active</div>
              <div className="col-md-4"><strong>Expiry:</strong> 09/27</div>
              <div className="col-md-4"><strong>Billing currency:</strong> GBP</div>
            </div>
          </div>
        </section>

        <section className="card border-0 shadow-sm">
          <div className="card-body d-flex gap-2 flex-wrap">
            <button type="button" className="btn btn-outline-primary">Set as default</button>
            <button type="button" className="btn btn-outline-secondary">Temporarily lock</button>
            <button type="button" className="btn btn-outline-danger">Remove card</button>
          </div>
        </section>
      </div>
    </main>
  );
};
