import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";

import { createPersonalTransaction, listPersonalAccounts, type PersonalAccount } from "../../api/personalFinance";

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

      navigate("/transactions");
    } catch {
      setErrorMessage("Unable to create transaction. Check your values and try again.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <div className="d-flex justify-content-between align-items-center mb-3">
          <h3 className="alt mb-0">Add transaction</h3>
          <Link className="btn btn-outline-secondary" to="/transactions">Back to transactions</Link>
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
    </main>
  );
};
