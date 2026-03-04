import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";

import { createPersonalTransaction, listPersonalAccounts, type PersonalAccount } from "../../api/personalFinance";
import { SidebarNav } from "../../components/navigation/SidebarNav";

const toLocalDateTimeInput = (value: Date) => {
  const year = value.getFullYear();
  const month = String(value.getMonth() + 1).padStart(2, "0");
  const day = String(value.getDate()).padStart(2, "0");
  const hours = String(value.getHours()).padStart(2, "0");
  const minutes = String(value.getMinutes()).padStart(2, "0");
  return `${year}-${month}-${day}T${hours}:${minutes}`;
};

export const TransactionManualCreate = () => {
  const navigate = useNavigate();

  const [accounts, setAccounts] = useState<PersonalAccount[]>([]);
  const [isSaving, setIsSaving] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const [personalAccountId, setPersonalAccountId] = useState<string>("");
  const [occurredAt, setOccurredAt] = useState<string>(toLocalDateTimeInput(new Date()));
  const [amount, setAmount] = useState<string>("");
  const [currency, setCurrency] = useState<string>("USD");
  const [merchant, setMerchant] = useState<string>("");
  const [description, setDescription] = useState<string>("");
  const [category, setCategory] = useState<string>("");
  const [notes, setNotes] = useState<string>("");

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        const result = await listPersonalAccounts();
        if (!cancelled) {
          setAccounts(result);
          if (result.length > 0) {
            setPersonalAccountId(result[0].personalAccountId);
            setCurrency(result[0].currency);
          }
        }
      } catch {
        if (!cancelled) {
          setErrorMessage("Unable to load personal accounts.");
        }
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, []);

  const selectedAccount = useMemo(
    () => accounts.find((account) => account.personalAccountId === personalAccountId) ?? null,
    [accounts, personalAccountId]
  );

  const handleSubmit: React.FormEventHandler<HTMLFormElement> = async (event) => {
    event.preventDefault();

    setIsSaving(true);
    setErrorMessage(null);

    try {
      await createPersonalTransaction({
        personalAccountId: personalAccountId || null,
        occurredAt: new Date(occurredAt).toISOString(),
        amount: Number(amount),
        currency,
        merchant: merchant || null,
        description: description || null,
        category: category || null,
        notes: notes || null,
        tags: []
      });

      navigate("/personal-finance/transactions");
    } catch {
      setErrorMessage("Unable to create transaction. Check your values and try again.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <main className="bg-secondary overflow-hidden">
      <div className="container">
        <div className="row">
          <div className="col-lg-4 col-xl-3">
            <SidebarNav />
          </div>
          <div className="col-lg-8 col-xl-9">
            <div className="wrapper-content">
              <div className="row align-items-end mb-md-2">
                <div className="col-xl-8">
                  <Link className="back-left-arrow" to="/personal-finance/transactions">
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z" fill="currentColor" />
                    </svg>
                    Back to transactions
                  </Link>
                  <h3 className="alt mt-4">Add transaction</h3>
                </div>
              </div>

              {errorMessage ? <div className="alert alert-warning">{errorMessage}</div> : null}

              <form className="card card-tbox" onSubmit={handleSubmit}>
                <div className="card-body row g-3">
                  <div className="col-md-6">
                    <label className="form-label">Source account</label>
                    <select
                      className="form-select"
                      value={personalAccountId}
                      onChange={(event) => setPersonalAccountId(event.target.value)}
                    >
                      <option value="">Unassigned</option>
                      {accounts.map((account) => (
                        <option key={account.personalAccountId} value={account.personalAccountId}>
                          {account.name}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="col-md-6">
                    <label className="form-label">Occurred at</label>
                    <input
                      className="form-control"
                      type="datetime-local"
                      value={occurredAt}
                      onChange={(event) => setOccurredAt(event.target.value)}
                      required
                    />
                  </div>

                  <div className="col-md-4">
                    <label className="form-label">Amount</label>
                    <input
                      className="form-control"
                      type="number"
                      step="0.01"
                      value={amount}
                      onChange={(event) => setAmount(event.target.value)}
                      required
                    />
                    <small className="text-muted">Use negative for expenses, positive for income.</small>
                  </div>

                  <div className="col-md-4">
                    <label className="form-label">Currency</label>
                    <input
                      className="form-control"
                      value={currency}
                      onChange={(event) => setCurrency(event.target.value.toUpperCase())}
                      maxLength={3}
                      required
                    />
                  </div>

                  <div className="col-md-4">
                    <label className="form-label">Category (optional)</label>
                    <input
                      className="form-control"
                      value={category}
                      onChange={(event) => setCategory(event.target.value)}
                      placeholder="Groceries"
                    />
                  </div>

                  <div className="col-md-6">
                    <label className="form-label">Merchant</label>
                    <input
                      className="form-control"
                      value={merchant}
                      onChange={(event) => setMerchant(event.target.value)}
                      placeholder="Store name"
                    />
                  </div>

                  <div className="col-md-6">
                    <label className="form-label">Description</label>
                    <input
                      className="form-control"
                      value={description}
                      onChange={(event) => setDescription(event.target.value)}
                      placeholder="Transaction description"
                    />
                  </div>

                  <div className="col-12">
                    <label className="form-label">Notes</label>
                    <textarea
                      className="form-control"
                      rows={3}
                      value={notes}
                      onChange={(event) => setNotes(event.target.value)}
                    />
                  </div>

                  <div className="col-12 d-flex gap-2 justify-content-end">
                    <button type="submit" className="btn btn-primary" disabled={isSaving || (!selectedAccount && personalAccountId !== "")}>
                      {isSaving ? "Saving..." : "Save transaction"}
                    </button>
                  </div>
                </div>
              </form>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
