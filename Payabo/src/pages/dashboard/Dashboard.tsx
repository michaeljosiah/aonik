import { SidebarNav } from "../../components/navigation/SidebarNav";

export const Dashboard = () => {
  return (
    <main className="bg-secondary overflow-hidden">
      <div className="container">
        <div className="row">
          <div className="col-lg-4 col-xl-3">
            <SidebarNav />
          </div>
          <div className="col-lg-8 col-xl-9">
            <div className="card card-pt">
              <div className="card-body">
                <h3 className="mb-2">Welcome back</h3>
                <p className="mb-0">Your Payabo dashboard will show upcoming bills, reminders, and recent activity.</p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
