import { Link } from "react-router-dom";

export const LoginDetails = () => {
  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Login details</h3>
        <div className="list-group">
          <Link className="list-group-item list-group-item-action" to="/profile/login-details/email">
            Update email
          </Link>
          <Link className="list-group-item list-group-item-action" to="/profile/login-details/password">
            Update password
          </Link>
        </div>
      </div>
    </main>
  );
};
