const upcomingTransactions = [
  { day: "Mon", date: "12", title: "Electricity bill", amount: "£45.00" },
  { day: "Wed", date: "14", title: "Internet subscription", amount: "£29.99" },
  { day: "Fri", date: "16", title: "Water bill", amount: "£22.10" }
];

export const TransactionsCalendar = () => {
  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4 py-lg-5">
        <div className="mb-4">
          <h3 className="alt mb-1">Transactions calendar</h3>
          <p className="text-muted mb-0">Track upcoming scheduled payments in a calendar-friendly timeline.</p>
        </div>

        <div className="row g-3">
          {upcomingTransactions.map((item) => (
            <div className="col-md-4" key={`${item.day}-${item.date}`}>
              <article className="card border-0 shadow-sm h-100">
                <div className="card-body">
                  <span className="badge bg-light text-dark mb-2">{item.day} {item.date}</span>
                  <h6 className="mb-1">{item.title}</h6>
                  <p className="text-muted mb-0">Scheduled: {item.amount}</p>
                </div>
              </article>
            </div>
          ))}
        </div>
      </div>
    </main>
  );
};
