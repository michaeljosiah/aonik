import { useEffect, useState } from "react";

import { createPersonalAccount, listPersonalAccounts, type PersonalAccount } from "../../api/personalFinance";

export const WalletAccounts = () => {
  const [accounts, setAccounts] = useState<PersonalAccount[]>([]);
  const [name, setName] = useState("");
  const [accountType, setAccountType] = useState("Bank");
  const [currency, setCurrency] = useState("USD");
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const loadAccounts = async () => {
    const result = await listPersonalAccounts();
    setAccounts(result);
  };

  useEffect(() => {
    void loadAccounts();
  }, []);

  const handleCreate = async () => {
    if (!name.trim()) {
      setErrorMessage("Account name is required.");
      return;
    }

    try {
      await createPersonalAccount({
        name,
        accountType,
        currency,
        institutionName: null,
        externalReference: null,
        accountSubtype: null,
        last4: null
      });

      setName("");
      setErrorMessage(null);
      await loadAccounts();
    } catch {
      setErrorMessage("Unable to create account.");
    }
  };

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Wallet accounts</h3>

        {errorMessage ? <div className="alert alert-warning">{errorMessage}</div> : null}

        <div className="card card-tbox mb-3">
          <div className="card-body row g-3 align-items-end">
            <div className="col-md-5">
              <label className="form-label">Account name</label>
              <input className="form-control" value={name} onChange={(event) => setName(event.target.value)} />
            </div>
            <div className="col-md-3">
              <label className="form-label">Type</label>
              <select className="form-select" value={accountType} onChange={(event) => setAccountType(event.target.value)}>
                <option value="Bank">Bank</option>
                <option value="CreditCard">Credit card</option>
                <option value="Wallet">Wallet</option>
              </select>
            </div>
            <div className="col-md-2">
              <label className="form-label">Currency</label>
              <input className="form-control" value={currency} onChange={(event) => setCurrency(event.target.value.toUpperCase())} maxLength={3} />
            </div>
            <div className="col-md-2 d-grid">
              <button type="button" className="btn btn-primary" onClick={() => void handleCreate()}>Add account</button>
            </div>
          </div>
        </div>

        <div className="card card-tbox">
          <div className="card-body">
            {accounts.length === 0 ? (
              <div className="alert alert-light mb-0">No accounts yet.</div>
            ) : (
              <div className="table-responsive">
                <table className="table table-card mb-0">
                  <thead>
                    <tr>
                      <th>Name</th>
                      <th>Type</th>
                      <th>Currency</th>
                      <th>Status</th>
                    </tr>
                  </thead>
                  <tbody>
                    {accounts.map((account) => (
                      <tr key={account.personalAccountId}>
                        <td>{account.name}</td>
                        <td>{account.accountType}</td>
                        <td>{account.currency}</td>
                        <td>{account.status}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </div>
    </main>
  );
};
