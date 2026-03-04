import { useState } from "react";

import { updateCustomerPassword } from "../../api/profile";

export const LoginDetailsPassword = () => {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const submit = async () => {
    setIsSaving(true);
    setMessage(null);
    setErrorMessage(null);

    try {
      await updateCustomerPassword({ currentPassword, newPassword });
      setMessage("Password updated.");
      setCurrentPassword("");
      setNewPassword("");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Unable to update password.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Update password</h3>
        {message && <div className="alert alert-success">{message}</div>}
        {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

        <div className="card card-tbox">
          <div className="card-body">
            <div className="mb-3">
              <label className="form-label">Current password</label>
              <input className="form-control" type="password" value={currentPassword} onChange={(event) => setCurrentPassword(event.target.value)} />
            </div>
            <div className="mb-3">
              <label className="form-label">New password</label>
              <input className="form-control" type="password" value={newPassword} onChange={(event) => setNewPassword(event.target.value)} />
            </div>
            <button type="button" className="btn btn-primary" onClick={submit} disabled={isSaving}>
              {isSaving ? "Updating..." : "Update password"}
            </button>
          </div>
        </div>
      </div>
    </main>
  );
};
