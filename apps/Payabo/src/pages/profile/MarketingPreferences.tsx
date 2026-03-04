import { useEffect, useMemo, useState, type ChangeEvent, type FormEvent } from "react";

type MarketingPreferencesState = {
  email: boolean;
  sms: boolean;
  productNews: boolean;
  offers: boolean;
  education: boolean;
  research: boolean;
};

type MarketingOption = {
  key: keyof MarketingPreferencesState;
  label: string;
  hint: string;
};

const marketingPreferencesStorageKey = "payabo.profile.marketingPreferences";

const defaultPreferences: MarketingPreferencesState = {
  email: true,
  sms: false,
  productNews: true,
  offers: false,
  education: true,
  research: false
};

const channelOptions: MarketingOption[] = [
  {
    key: "email",
    label: "Email",
    hint: "Receive newsletters and feature updates to your email address."
  },
  {
    key: "sms",
    label: "SMS",
    hint: "Receive occasional concise campaign messages by text message."
  }
];

const contentOptions: MarketingOption[] = [
  {
    key: "productNews",
    label: "Product news",
    hint: "Updates on new capabilities, releases, and user experience improvements."
  },
  {
    key: "offers",
    label: "Promotions and offers",
    hint: "Occasional pricing offers and incentives from Payabo and trusted partners."
  },
  {
    key: "education",
    label: "Financial tips and education",
    hint: "Practical content on budgeting, bill management, and financial wellness."
  },
  {
    key: "research",
    label: "Research invitations",
    hint: "Opportunities to participate in user interviews and product feedback sessions."
  }
];

const readStoredPreferences = (): MarketingPreferencesState => {
  try {
    const raw = localStorage.getItem(marketingPreferencesStorageKey);
    if (!raw) {
      return defaultPreferences;
    }

    const parsed = JSON.parse(raw) as Partial<MarketingPreferencesState>;

    return {
      email: typeof parsed.email === "boolean" ? parsed.email : defaultPreferences.email,
      sms: typeof parsed.sms === "boolean" ? parsed.sms : defaultPreferences.sms,
      productNews: typeof parsed.productNews === "boolean" ? parsed.productNews : defaultPreferences.productNews,
      offers: typeof parsed.offers === "boolean" ? parsed.offers : defaultPreferences.offers,
      education: typeof parsed.education === "boolean" ? parsed.education : defaultPreferences.education,
      research: typeof parsed.research === "boolean" ? parsed.research : defaultPreferences.research
    };
  } catch {
    return defaultPreferences;
  }
};

export const MarketingPreferences = () => {
  const [preferences, setPreferences] = useState<MarketingPreferencesState>(defaultPreferences);
  const [isHydrated, setIsHydrated] = useState<boolean>(false);
  const [savedAt, setSavedAt] = useState<Date | null>(null);

  useEffect(() => {
    setPreferences(readStoredPreferences());
    setIsHydrated(true);
  }, []);

  const handleToggle = (event: ChangeEvent<HTMLInputElement>) => {
    const { name, checked } = event.target;
    const key = name as keyof MarketingPreferencesState;

    setPreferences((current) => ({
      ...current,
      [key]: checked
    }));

    setSavedAt(null);
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    try {
      localStorage.setItem(marketingPreferencesStorageKey, JSON.stringify(preferences));
      setSavedAt(new Date());
    } catch {
      setSavedAt(null);
    }
  };

  const savedAtText = useMemo(() => {
    if (!savedAt) {
      return null;
    }

    return `Saved at ${savedAt.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}`;
  }, [savedAt]);

  if (!isHydrated) {
    return <main className="main-wrapper overflow-hidden"><div className="container py-4">Loading preferences...</div></main>;
  }

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4 py-lg-5">
        <div className="row justify-content-center">
          <div className="col-xl-8">
            <div className="card border-0 shadow-sm">
              <div className="card-body p-4 p-lg-5">
                <h3 className="alt mb-2">Marketing preferences</h3>
                <p className="text-muted mb-4">Control which non-essential communications you would like to receive.</p>

                <form onSubmit={handleSubmit}>
                  <h5 className="mb-3">Delivery channels</h5>
                  <div className="d-flex flex-column gap-3 mb-4">
                    {channelOptions.map((option) => (
                      <label className="d-flex align-items-start gap-2" key={option.key}>
                        <input type="checkbox" name={option.key} checked={preferences[option.key]} onChange={handleToggle} />
                        <span>
                          <strong className="d-block">{option.label}</strong>
                          <small className="text-muted">{option.hint}</small>
                        </span>
                      </label>
                    ))}
                  </div>

                  <h5 className="mb-3">Content preferences</h5>
                  <div className="d-flex flex-column gap-3">
                    {contentOptions.map((option) => (
                      <label className="d-flex align-items-start gap-2" key={option.key}>
                        <input type="checkbox" name={option.key} checked={preferences[option.key]} onChange={handleToggle} />
                        <span>
                          <strong className="d-block">{option.label}</strong>
                          <small className="text-muted">{option.hint}</small>
                        </span>
                      </label>
                    ))}
                  </div>

                  <div className="d-flex align-items-center gap-3 mt-4">
                    <button type="submit" className="btn btn-primary px-4">
                      Save preferences
                    </button>
                    {savedAtText ? <span className="text-success small">{savedAtText}</span> : null}
                  </div>
                </form>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
