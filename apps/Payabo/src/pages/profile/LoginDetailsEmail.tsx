import { useState } from "react";

import { updateCustomerEmail } from "../../api/profile";

export const LoginDetailsEmail = () => {
  const [currentEmail, setCurrentEmail] = useState("");
  const [newEmail, setNewEmail] = useState("");
  const [password, setPassword] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const submit = async () => {
    setIsSaving(true);
    setMessage(null);
    setErrorMessage(null);

    try {
      await updateCustomerEmail({ currentEmail, newEmail, password });
      setMessage("Email updated.");
      setCurrentEmail(newEmail);
      setNewEmail("");
      setPassword("");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Unable to update email.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Update email</h3>
        {message && <div className="alert alert-success">{message}</div>}
        {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

        <div className="card card-tbox">
          <div className="card-body">
            <div className="mb-3">
              <label className="form-label">Current email</label>
              <input className="form-control" type="email" value={currentEmail} onChange={(event) => setCurrentEmail(event.target.value)} />
            </div>
            <div className="mb-3">
              <label className="form-label">New email</label>
              <input className="form-control" type="email" value={newEmail} onChange={(event) => setNewEmail(event.target.value)} />
            </div>
            <div className="mb-3">
              <label className="form-label">Password confirmation</label>
              <input className="form-control" type="password" value={password} onChange={(event) => setPassword(event.target.value)} />
            </div>
            <button type="button" className="btn btn-primary" onClick={submit} disabled={isSaving}>
              {isSaving ? "Updating..." : "Update email"}
            </button>
          </div>
        </div>
      </div>
    </main>
  );
};
