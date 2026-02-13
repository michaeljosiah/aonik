import { useEffect, useMemo, useState, type ChangeEvent, type FormEvent } from "react";

type NotificationSettingsState = {
  pushNotifications: boolean;
  emailNotifications: boolean;
  smsNotifications: boolean;
  billReminders: boolean;
  paymentStatus: boolean;
  securityAlerts: boolean;
  productUpdates: boolean;
};

type NotificationOption = {
  key: keyof NotificationSettingsState;
  label: string;
  hint: string;
};

const notificationSettingsStorageKey = "payabo.profile.notificationSettings";

const defaultSettings: NotificationSettingsState = {
  pushNotifications: true,
  emailNotifications: true,
  smsNotifications: false,
  billReminders: true,
  paymentStatus: true,
  securityAlerts: true,
  productUpdates: false
};

const channelOptions: NotificationOption[] = [
  {
    key: "pushNotifications",
    label: "Push notifications",
    hint: "Send updates directly to your signed-in devices."
  },
  {
    key: "emailNotifications",
    label: "Email notifications",
    hint: "Send activity summaries and important alerts by email."
  },
  {
    key: "smsNotifications",
    label: "SMS notifications",
    hint: "Send urgent account and payment alerts by text message."
  }
];

const typeOptions: NotificationOption[] = [
  {
    key: "billReminders",
    label: "Bill reminders",
    hint: "Get reminders before a scheduled bill payment is due."
  },
  {
    key: "paymentStatus",
    label: "Payment status updates",
    hint: "Receive notifications when a payment succeeds, fails, or is pending."
  },
  {
    key: "securityAlerts",
    label: "Security alerts",
    hint: "Be informed about important sign-ins and account security events."
  },
  {
    key: "productUpdates",
    label: "Product updates",
    hint: "Hear about new Payabo features and account improvements."
  }
];

const readStoredSettings = (): NotificationSettingsState => {
  try {
    const raw = localStorage.getItem(notificationSettingsStorageKey);
    if (!raw) {
      return defaultSettings;
    }

    const parsed = JSON.parse(raw) as Partial<NotificationSettingsState>;

    return {
      pushNotifications: typeof parsed.pushNotifications === "boolean" ? parsed.pushNotifications : defaultSettings.pushNotifications,
      emailNotifications: typeof parsed.emailNotifications === "boolean" ? parsed.emailNotifications : defaultSettings.emailNotifications,
      smsNotifications: typeof parsed.smsNotifications === "boolean" ? parsed.smsNotifications : defaultSettings.smsNotifications,
      billReminders: typeof parsed.billReminders === "boolean" ? parsed.billReminders : defaultSettings.billReminders,
      paymentStatus: typeof parsed.paymentStatus === "boolean" ? parsed.paymentStatus : defaultSettings.paymentStatus,
      securityAlerts: typeof parsed.securityAlerts === "boolean" ? parsed.securityAlerts : defaultSettings.securityAlerts,
      productUpdates: typeof parsed.productUpdates === "boolean" ? parsed.productUpdates : defaultSettings.productUpdates
    };
  } catch {
    return defaultSettings;
  }
};

export const NotificationSettings = () => {
  const [settings, setSettings] = useState<NotificationSettingsState>(defaultSettings);
  const [isHydrated, setIsHydrated] = useState<boolean>(false);
  const [savedAt, setSavedAt] = useState<Date | null>(null);

  useEffect(() => {
    setSettings(readStoredSettings());
    setIsHydrated(true);
  }, []);

  const handleToggleOption = (event: ChangeEvent<HTMLInputElement>) => {
    const { name, checked } = event.target;
    const key = name as keyof NotificationSettingsState;

    setSettings((current) => ({
      ...current,
      [key]: checked
    }));

    setSavedAt(null);
  };

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    try {
      localStorage.setItem(notificationSettingsStorageKey, JSON.stringify(settings));
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
                <h3 className="alt mb-2">Notification settings</h3>
                <p className="text-muted mb-4">Choose what Payabo should notify you about and where to send updates.</p>

                <form onSubmit={handleSubmit}>
                  <h5 className="mb-3">Notification channels</h5>
                  <div className="d-flex flex-column gap-3 mb-4">
                    {channelOptions.map((option) => (
                      <label className="d-flex align-items-start gap-2" key={option.key}>
                        <input type="checkbox" name={option.key} checked={settings[option.key]} onChange={handleToggleOption} />
                        <span>
                          <strong className="d-block">{option.label}</strong>
                          <small className="text-muted">{option.hint}</small>
                        </span>
                      </label>
                    ))}
                  </div>

                  <h5 className="mb-3">Notification types</h5>
                  <div className="d-flex flex-column gap-3">
                    {typeOptions.map((option) => (
                      <label className="d-flex align-items-start gap-2" key={option.key}>
                        <input type="checkbox" name={option.key} checked={settings[option.key]} onChange={handleToggleOption} />
                        <span>
                          <strong className="d-block">{option.label}</strong>
                          <small className="text-muted">{option.hint}</small>
                        </span>
                      </label>
                    ))}
                  </div>

                  <div className="d-flex align-items-center gap-3 mt-4">
                    <button type="submit" className="btn btn-primary px-4">
                      Save changes
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
