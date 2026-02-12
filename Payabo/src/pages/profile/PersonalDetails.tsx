import { useEffect, useState } from "react";

import { getCustomerProfile, type CustomerProfile } from "../../api/profile";

export const PersonalDetails = () => {
  const [profile, setProfile] = useState<CustomerProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        const response = await getCustomerProfile();
        if (!cancelled) {
          setProfile(response);
        }
      } catch {
        if (!cancelled) {
          setErrorMessage("Unable to load profile yet. Please sign in with a configured identity provider.");
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

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Personal details</h3>

        {isLoading && <p>Loading profile...</p>}
        {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

        {profile && (
          <div className="card card-tbox">
            <div className="card-body">
              <p><strong>Name:</strong> {[profile.firstName, profile.lastName].filter(Boolean).join(" ") || "Not set"}</p>
              <p><strong>Email:</strong> {profile.email}</p>
              <p><strong>Phone:</strong> {profile.phone ?? "Not set"}</p>
              <p><strong>Country:</strong> {profile.countryCode ?? "Not set"}</p>
              <p className="mb-0"><strong>Party ID:</strong> {profile.partyId}</p>
            </div>
          </div>
        )}
      </div>
    </main>
  );
};
