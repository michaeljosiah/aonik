import { useState } from "react";

import { updateCustomerEmail } from "../../api/profile";
import { useAuth } from "../../app/auth/AuthContext";

export const LoginDetailsEmail = () => {
  const { login, user } = useAuth();
  const [newEmail, setNewEmail] = useState("");
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const submit = async () => {
    setIsSaving(true);
    setMessage(null);
    setErrorMessage(null);

    try {
      await updateCustomerEmail({ newEmail });
      setMessage("If the request is eligible, check your new email for a confirmation link. Your current email stays unchanged until you confirm. If no email arrives, sign in again and retry.");
      setNewEmail("");
    } catch (error) {
      const status = (error as { status?: number } | null)?.status;
      setErrorMessage(status === 401 || status === 403
        ? "Please sign in again before requesting an email change."
        : error instanceof Error ? error.message : "Unable to request an email change.");
    } finally {
      setIsSaving(false);
    }
  };

  const signInAgain = async () => {
    try {
      await login({ prompt: "login", loginHint: user?.email, returnTo: "/profile/login-details/email" });
    } catch {
      setErrorMessage("Unable to start secure sign in. Please try again.");
    }
  };

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Change email</h3>
        <p>Sign in again before requesting a change. We will ask you to confirm your new email address.</p>
        <button type="button" className="btn btn-link mb-3" onClick={signInAgain} disabled={isSaving}>Sign in again</button>
        {message && <div className="alert alert-success">{message}</div>}
        {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

        <div className="card card-tbox">
          <form className="card-body" onSubmit={(event) => { event.preventDefault(); void submit(); }}>
            <div className="mb-3">
              <label className="form-label" htmlFor="new-email">New email</label>
              <input id="new-email" className="form-control" type="email" autoComplete="email" required value={newEmail} onChange={(event) => setNewEmail(event.target.value)} />
            </div>
            <button type="submit" className="btn btn-primary" disabled={isSaving || !newEmail.trim()}>
              {isSaving ? "Requesting..." : "Send confirmation link"}
            </button>
          </form>
        </div>
      </div>
    </main>
  );
};
