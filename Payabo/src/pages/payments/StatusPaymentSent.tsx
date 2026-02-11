import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { getPublicPaymentIntentStatus, type PublicPaymentIntentStatus } from "../../api/payments";
import { getPublicBillPaymentDraft, type GuestBillPaymentDraftDetail } from "../../api/orders";
import { readCheckoutAttemptState } from "./paymentFlowState";

type UiStatus = "success" | "failed" | "pending";

const normalizeResult = (value: string | null): UiStatus => {
  const normalized = value?.trim().toLowerCase();

  if (normalized === "cancelled" || normalized === "canceled" || normalized === "failed") {
    return "failed";
  }

  if (normalized === "pending") {
    return "pending";
  }

  return "success";
};

type StatusPaymentSentProps = {
  forcedResult?: UiStatus;
};

export const StatusPaymentSent = ({ forcedResult }: StatusPaymentSentProps) => {
  const [searchParams] = useSearchParams();
  const savedAttempt = readCheckoutAttemptState();

  const orderId = searchParams.get("orderId") ?? savedAttempt?.orderId ?? "";
  const paymentIntentId = searchParams.get("paymentIntentId") ?? savedAttempt?.paymentIntentId ?? "";
  const providerReference = searchParams.get("providerReference") ?? searchParams.get("payment_intent") ?? savedAttempt?.providerReference ?? "";

  const [paymentStatus, setPaymentStatus] = useState<PublicPaymentIntentStatus | null>(null);
  const [draft, setDraft] = useState<GuestBillPaymentDraftDetail | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const loadStatus = async () => {
      if (!orderId) {
        setErrorMessage("We could not determine your order. Please return to payment selection.");
        setIsLoading(false);
        return;
      }

      setIsLoading(true);
      setErrorMessage(null);

      try {
        const [statusResult, draftResult] = await Promise.all([
          getPublicPaymentIntentStatus({
            orderId,
            paymentIntentId: paymentIntentId || undefined,
            providerReference: providerReference || undefined
          }),
          getPublicBillPaymentDraft(orderId)
        ]);

        if (!cancelled) {
          setPaymentStatus(statusResult);
          setDraft(draftResult);
        }
      } catch {
        if (!cancelled) {
          setPaymentStatus(null);
          setDraft(null);
          setErrorMessage("We could not confirm this payment yet. Please refresh in a moment.");
        }
      } finally {
        if (!cancelled) {
          setIsLoading(false);
        }
      }
    };

    void loadStatus();

    return () => {
      cancelled = true;
    };
  }, [orderId, paymentIntentId, providerReference]);

  const uiStatus = useMemo<UiStatus>(() => {
    const resultStatus = forcedResult ?? normalizeResult(searchParams.get("result"));

    if (resultStatus === "failed") {
      return "failed";
    }

    if (paymentStatus?.status.toLowerCase() === "pending") {
      return "pending";
    }

    return "success";
  }, [forcedResult, paymentStatus?.status, searchParams]);

  const title = uiStatus === "success" ? "Payment submitted" : uiStatus === "pending" ? "Payment pending" : "Payment failed";

  const subtitle = uiStatus === "success"
    ? "Your payment has been submitted and is being processed."
    : uiStatus === "pending"
      ? "We received your request and are still waiting for provider confirmation."
      : "Your payment could not be completed. You can retry from payment selection.";

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-5">
        <h3 className="alt mb-3">{title}</h3>
        <p>{subtitle}</p>

        {isLoading && <p>Loading latest status...</p>}

        {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

        {paymentStatus && (
          <div className="card card-tbox mb-3">
            <div className="card-body">
              <h5 className="mb-3">Payment reference</h5>
              <p className="mb-1"><strong>Order ID:</strong> {paymentStatus.orderId}</p>
              <p className="mb-1"><strong>Payment Intent ID:</strong> {paymentStatus.paymentIntentId}</p>
              <p className="mb-1"><strong>Provider Reference:</strong> {paymentStatus.providerReference}</p>
              <p className="mb-1"><strong>Payment Status:</strong> {paymentStatus.status}</p>
              <p className="mb-0"><strong>Order Status:</strong> {paymentStatus.orderStatus}</p>
            </div>
          </div>
        )}

        {draft && (
          <div className="card card-tbox mb-4">
            <div className="card-body">
              <h5 className="mb-3">Service details</h5>
              <p className="mb-1"><strong>Biller:</strong> {draft.billerName ?? "Selected provider"}</p>
              <p className="mb-1"><strong>Service:</strong> {draft.serviceName}</p>
              <p className="mb-0"><strong>Amount:</strong> {draft.requestedAmount ?? "Not set"} {draft.currency}</p>
            </div>
          </div>
        )}

        <div className="d-flex gap-3">
          <Link className="btn btn-primary" to="/dashboard">Go to dashboard</Link>
          <Link className="btn btn-secondary" to="/payments/selection">Pay another bill</Link>
        </div>
      </div>
    </main>
  );
};
