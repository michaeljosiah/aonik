import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import { getCustomerProfile, updateCustomerProfile, type CustomerProfile } from "../../api/profile";

export const PersonalDetails = () => {
  const [profile, setProfile] = useState<CustomerProfile | null>(null);
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [phone, setPhone] = useState("");
  const [countryCode, setCountryCode] = useState("");
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const applyProfile = (next: CustomerProfile) => {
    setProfile(next);
    setFirstName(next.firstName ?? "");
    setLastName(next.lastName ?? "");
    setPhone(next.phone ?? "");
    setCountryCode(next.countryCode ?? "");
  };

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        const response = await getCustomerProfile();
        if (!cancelled) {
          applyProfile(response);
        }
      } catch {
        if (!cancelled) {
          setErrorMessage("Unable to load profile yet. Please sign in again.");
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    };

    void load();
    return () => {
      cancelled = true;
    };
  }, []);

  const save = async () => {
    setErrorMessage(null);
    setMessage(null);
    setIsSaving(true);

    try {
      const updated = await updateCustomerProfile({
        firstName: firstName.trim() || null,
        lastName: lastName.trim() || null,
        phone: phone.trim() || null,
        countryCode: countryCode.trim().toUpperCase() || null
      });
      applyProfile(updated);
      setMessage("Profile updated.");
    } catch (error) {
      const messageFromError = error instanceof Error ? error.message : "Unable to update profile.";
      setErrorMessage(messageFromError);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Personal details</h3>

        {isLoading && <p>Loading profile...</p>}
        {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}
        {message && <div className="alert alert-success">{message}</div>}

        {profile && (
          <div className="card card-tbox">
            <div className="card-body">
              <div className="row g-3">
                <div className="col-md-6">
                  <label className="form-label">First name</label>
                  <input className="form-control" value={firstName} onChange={(event) => setFirstName(event.target.value)} />
                </div>
                <div className="col-md-6">
                  <label className="form-label">Last name</label>
                  <input className="form-control" value={lastName} onChange={(event) => setLastName(event.target.value)} />
                </div>
                <div className="col-md-6">
                  <label className="form-label">Phone</label>
                  <input className="form-control" value={phone} onChange={(event) => setPhone(event.target.value)} />
                </div>
                <div className="col-md-6">
                  <label className="form-label">Country code (ISO-2)</label>
                  <input className="form-control" value={countryCode} onChange={(event) => setCountryCode(event.target.value)} maxLength={2} />
                </div>
              </div>

              <div className="mt-3 d-flex gap-2">
                <button type="button" className="btn btn-primary" onClick={save} disabled={isSaving}>
                  {isSaving ? "Saving..." : "Save profile"}
                </button>
                <Link className="btn btn-secondary" to="/profile/personal/photo">
                  Update photo
                </Link>
              </div>
            </div>
          </div>
        )}
      </div>
    </main>
  );
};
