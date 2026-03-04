import { Link } from "react-router-dom";

type SavedCard = {
  id: string;
  brand: string;
  last4: string;
  expiry: string;
  isDefault: boolean;
};

const savedCards: SavedCard[] = [
  { id: "card-1", brand: "Visa", last4: "4921", expiry: "09/27", isDefault: true },
  { id: "card-2", brand: "Mastercard", last4: "1044", expiry: "11/26", isDefault: false }
];

export const ManageCards = () => {
  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4 py-lg-5">
        <div className="d-flex justify-content-between align-items-center mb-4">
          <div>
            <h3 className="alt mb-1">Manage cards</h3>
            <p className="text-muted mb-0">Review your saved payment methods and set your default card.</p>
          </div>
          <button type="button" className="btn btn-primary">Add new card</button>
        </div>

        <div className="row g-3">
          {savedCards.map((card) => (
            <div className="col-12" key={card.id}>
              <article className="card border-0 shadow-sm">
                <div className="card-body d-flex justify-content-between align-items-center">
                  <div>
                    <h5 className="mb-1">{card.brand} •••• {card.last4}</h5>
                    <p className="text-muted mb-0">Expires {card.expiry}</p>
                  </div>
                  <div className="d-flex align-items-center gap-2">
                    {card.isDefault ? <span className="badge bg-success">Default</span> : null}
                    <Link className="btn btn-outline-primary btn-sm" to="/cards/details">View details</Link>
                  </div>
                </div>
              </article>
            </div>
          ))}
        </div>
      </div>
    </main>
  );
};
