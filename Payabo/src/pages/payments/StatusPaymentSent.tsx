import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";

import { getPublicPaymentIntentStatus, type PublicPaymentIntentStatus } from "../../api/payments";
import { getPublicBillPaymentDraft, type GuestBillPaymentDraftDetail } from "../../api/orders";
import { useAuth } from "../../app/auth/AuthContext";
import { upsertPaymentHistory } from "./paymentHistory";
import { readCheckoutAttemptState, resolvePaymentIntentIdForReturn } from "./paymentFlowState";

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

const mapBackendStatusToUiStatus = (status: string | null | undefined): UiStatus | null => {
  if (!status) {
    return null;
  }

  const normalizedStatus = status.trim().toLowerCase();

  if (normalizedStatus === "pending" || normalizedStatus === "processing" || normalizedStatus === "authorized") {
    return "pending";
  }

  if (
    normalizedStatus === "failed" ||
    normalizedStatus === "cancelled" ||
    normalizedStatus === "canceled" ||
    normalizedStatus === "declined" ||
    normalizedStatus === "expired"
  ) {
    return "failed";
  }

  if (normalizedStatus === "captured" || normalizedStatus === "completed" || normalizedStatus === "succeeded" || normalizedStatus === "paid") {
    return "success";
  }

  return null;
};

type StatusPaymentSentProps = {
  forcedResult?: UiStatus;
};

export const StatusPaymentSent = ({ forcedResult }: StatusPaymentSentProps) => {
  const [searchParams] = useSearchParams();
  const { user } = useAuth();
  const [refreshTick, setRefreshTick] = useState(0);
  const savedAttempt = readCheckoutAttemptState();

  const orderId = searchParams.get("orderId") ?? savedAttempt?.orderId ?? "";
  const providerReferenceFromQuery = searchParams.get("providerReference") ?? searchParams.get("payment_intent");
  const providerReference = providerReferenceFromQuery ?? savedAttempt?.providerReference ?? "";
  const paymentIntentId = resolvePaymentIntentIdForReturn({
    paymentIntentIdFromQuery: searchParams.get("paymentIntentId"),
    providerReferenceFromQuery,
    savedAttempt
  });

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
  }, [orderId, paymentIntentId, providerReference, refreshTick]);

  const uiStatus = useMemo<UiStatus>(() => {
    const queryResultStatus = forcedResult ?? normalizeResult(searchParams.get("result"));

    const backendPaymentStatus = mapBackendStatusToUiStatus(paymentStatus?.status);
    const backendOrderStatus = mapBackendStatusToUiStatus(paymentStatus?.orderStatus);

    if (queryResultStatus === "failed") {
      return "failed";
    }

    if (backendPaymentStatus === "failed" || backendOrderStatus === "failed") {
      return "failed";
    }

    if (backendPaymentStatus === "pending" || backendOrderStatus === "pending") {
      return "pending";
    }

    if (queryResultStatus === "pending") {
      return "pending";
    }

    if (backendPaymentStatus === "success" || backendOrderStatus === "success") {
      return "success";
    }

    return queryResultStatus;
  }, [forcedResult, paymentStatus?.orderStatus, paymentStatus?.status, searchParams]);

  useEffect(() => {
    if (uiStatus !== "pending") {
      return;
    }

    const timer = window.setTimeout(() => setRefreshTick((value) => value + 1), 5000);
    return () => window.clearTimeout(timer);
  }, [refreshTick, uiStatus]);

  useEffect(() => {
    if (!user?.id || !paymentStatus) {
      return;
    }

    upsertPaymentHistory({
      userId: user.id,
      orderId: paymentStatus.orderId,
      paymentIntentId: paymentStatus.paymentIntentId,
      providerReference: paymentStatus.providerReference,
      status: paymentStatus.status,
      orderStatus: paymentStatus.orderStatus,
      amount: paymentStatus.amount,
      currency: paymentStatus.currency,
      serviceName: draft?.serviceName ?? "Service",
      billerName: draft?.billerName ?? null,
      createdAt: paymentStatus.createdAt
    });
  }, [draft?.billerName, draft?.serviceName, paymentStatus, user?.id]);

  const title = uiStatus === "success" ? "Payment submitted" : uiStatus === "pending" ? "Payment pending" : "Payment failed";

  const subtitle =
    uiStatus === "success"
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

        <button type="button" className="btn btn-link p-0 mb-3" onClick={() => setRefreshTick((value) => value + 1)}>
          Refresh status
        </button>

        {errorMessage && <div className="alert alert-warning">{errorMessage}</div>}

        {paymentStatus && (
          <div className="card card-tbox mb-3">
            <div className="card-body">
              <h5 className="mb-3">Payment reference</h5>
              <p className="mb-1">
                <strong>Order ID:</strong> {paymentStatus.orderId}
              </p>
              <p className="mb-1">
                <strong>Payment Intent ID:</strong> {paymentStatus.paymentIntentId}
              </p>
              <p className="mb-1">
                <strong>Provider Reference:</strong> {paymentStatus.providerReference}
              </p>
              <p className="mb-1">
                <strong>Payment Status:</strong> {paymentStatus.status}
              </p>
              <p className="mb-0">
                <strong>Order Status:</strong> {paymentStatus.orderStatus}
              </p>
            </div>
          </div>
        )}

        {draft && (
          <div className="card card-tbox mb-4">
            <div className="card-body">
              <h5 className="mb-3">Service details</h5>
              <p className="mb-1">
                <strong>Biller:</strong> {draft.billerName ?? "Selected provider"}
              </p>
              <p className="mb-1">
                <strong>Service:</strong> {draft.serviceName}
              </p>
              <p className="mb-0">
                <strong>Amount:</strong> {draft.requestedAmount ?? "Not set"} {draft.currency}
              </p>
            </div>
          </div>
        )}

        <div className="d-flex gap-3">
          <Link className="btn btn-primary" to="/dashboard">
            Go to dashboard
          </Link>
          <Link className="btn btn-secondary" to="/payments/selection">
            Pay another bill
          </Link>
        </div>
      </div>
    </main>
  );
};
