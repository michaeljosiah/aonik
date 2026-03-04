import { Link } from "react-router-dom";

export const DashboardEmpty = () => {
  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-5 text-center">
        <h3 className="alt mb-2">Your dashboard is ready</h3>
        <p className="text-muted mb-4">You do not have recent bill payments yet. Start by choosing a provider and making your first payment.</p>
        <Link to="/payments/providers" className="btn btn-primary px-4">Pay a bill</Link>
      </div>
    </main>
  );
};
