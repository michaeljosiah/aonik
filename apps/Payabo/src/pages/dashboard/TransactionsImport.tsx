import { useEffect, useState } from "react";
import { Link } from "react-router-dom";

import {
  applyStatementImport,
  listPersonalAccounts,
  listStatementImports,
  uploadStatement,
  type PersonalAccount,
  type StatementImport
} from "../../api/personalFinance";
import { SidebarNav } from "../../components/navigation/SidebarNav";

export const TransactionsImport = () => {
  const [accounts, setAccounts] = useState<PersonalAccount[]>([]);
  const [imports, setImports] = useState<StatementImport[]>([]);
  const [selectedAccountId, setSelectedAccountId] = useState<string>("");
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const loadData = async () => {
    const [accountsResult, importsResult] = await Promise.all([listPersonalAccounts(), listStatementImports()]);
    setAccounts(accountsResult);
    setImports(importsResult);

    if (!selectedAccountId && accountsResult.length > 0) {
      setSelectedAccountId(accountsResult[0].personalAccountId);
    }
  };

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      try {
        await loadData();
      } catch {
        if (!cancelled) {
          setErrorMessage("Unable to load statement import data.");
        }
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, []);

  const handleUpload = async () => {
    if (!selectedAccountId || !selectedFile) {
      setErrorMessage("Select an account and CSV file before uploading.");
      return;
    }

    setIsUploading(true);
    setErrorMessage(null);

    try {
      await uploadStatement(selectedAccountId, selectedFile);
      setSelectedFile(null);
      await loadData();
    } catch {
      setErrorMessage("Statement upload failed.");
    } finally {
      setIsUploading(false);
    }
  };

  const handleApply = async (statementImportId: string) => {
    try {
      await applyStatementImport(statementImportId);
      await loadData();
    } catch {
      setErrorMessage("Failed to apply statement import.");
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
                  <h3 className="alt mt-4">Import statements</h3>
                </div>
              </div>

              {errorMessage ? <div className="alert alert-warning">{errorMessage}</div> : null}

              <div className="card card-tbox mb-3">
                <div className="card-body row g-3 align-items-end">
                  <div className="col-md-4">
                    <label className="form-label">Source account</label>
                    <select
                      className="form-select"
                      value={selectedAccountId}
                      onChange={(event) => setSelectedAccountId(event.target.value)}
                    >
                      <option value="">Select account</option>
                      {accounts.map((account) => (
                        <option key={account.personalAccountId} value={account.personalAccountId}>
                          {account.name}
                        </option>
                      ))}
                    </select>
                  </div>

                  <div className="col-md-5">
                    <label className="form-label">CSV file</label>
                    <input
                      className="form-control"
                      type="file"
                      accept=".csv,text/csv"
                      onChange={(event) => setSelectedFile(event.target.files?.[0] ?? null)}
                    />
                  </div>

                  <div className="col-md-3 d-grid">
                    <button type="button" className="btn btn-primary" disabled={isUploading} onClick={handleUpload}>
                      {isUploading ? "Uploading..." : "Upload statement"}
                    </button>
                  </div>
                </div>
              </div>

              <div className="card card-tbox">
                <div className="card-body">
                  <h6 className="mb-3">Recent imports</h6>
                  {imports.length === 0 ? (
                    <div className="alert alert-light mb-0">No statement imports yet.</div>
                  ) : (
                    <div className="table-responsive">
                      <table className="table table-card mb-0">
                        <thead>
                          <tr>
                            <th>File</th>
                            <th>Status</th>
                            <th>Parsed</th>
                            <th>Imported</th>
                            <th>Duplicates</th>
                            <th>Failed</th>
                            <th></th>
                          </tr>
                        </thead>
                        <tbody>
                          {imports.map((item) => (
                            <tr key={item.statementImportId}>
                              <td>{item.fileName}</td>
                              <td>{item.status}</td>
                              <td>{item.rowsParsed}</td>
                              <td>{item.rowsImported}</td>
                              <td>{item.rowsDuplicate}</td>
                              <td>{item.rowsFailed}</td>
                              <td>
                                {item.status === "Parsed" ? (
                                  <button
                                    type="button"
                                    className="btn btn-sm btn-outline-primary"
                                    onClick={() => void handleApply(item.statementImportId)}
                                  >
                                    Apply
                                  </button>
                                ) : null}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                    </div>
                  )}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
