import { useState } from "react";

import { deleteCustomerPhoto, uploadCustomerPhoto } from "../../api/profile";

export const PersonalDetailsUpdatePhoto = () => {
  const [isSaving, setIsSaving] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const onUpload = async (file: File | null) => {
    if (!file) {
      return;
    }

    setIsSaving(true);
    setMessage(null);
    setErrorMessage(null);

    try {
      await uploadCustomerPhoto(file);
      setMessage("Profile photo uploaded.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Unable to upload photo.");
    } finally {
      setIsSaving(false);
    }
  };

  const onDelete = async () => {
    setIsSaving(true);
    setMessage(null);
    setErrorMessage(null);

    try {
      await deleteCustomerPhoto();
      setMessage("Profile photo deleted.");
    } catch (error) {
      setErrorMessage(error instanceof Error ? error.message : "Unable to delete photo.");
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-4">
        <h3 className="alt mb-3">Update profile photo</h3>
        {message && <div className="alert alert-success">{message}</div>}
        {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

        <div className="card card-tbox">
          <div className="card-body d-flex gap-2 align-items-center">
            <input
              type="file"
              className="form-control"
              accept="image/*"
              onChange={(event) => onUpload(event.target.files?.[0] ?? null)}
              disabled={isSaving}
            />
            <button type="button" className="btn btn-outline-danger" onClick={onDelete} disabled={isSaving}>
              Delete photo
            </button>
          </div>
        </div>
      </div>
    </main>
  );
};
