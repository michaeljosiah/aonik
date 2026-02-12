import { useEffect } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";

import { readCheckoutAttemptState, resolvePaymentIntentIdForReturn } from "./paymentFlowState";

const normalizeResult = (value: string | null): "success" | "failed" | "pending" => {
  const normalized = value?.trim().toLowerCase();

  if (normalized === "cancelled" || normalized === "canceled" || normalized === "failed") {
    return "failed";
  }

  if (normalized === "pending") {
    return "pending";
  }

  return "success";
};

export const PaymentReturn = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  useEffect(() => {
    const attempt = readCheckoutAttemptState();

    const orderId = searchParams.get("orderId") ?? attempt?.orderId ?? "";
    const providerReferenceFromQuery = searchParams.get("providerReference") ?? searchParams.get("payment_intent");
    const providerReference = providerReferenceFromQuery ?? attempt?.providerReference ?? "";
    const paymentIntentId = resolvePaymentIntentIdForReturn({
      paymentIntentIdFromQuery: searchParams.get("paymentIntentId"),
      providerReferenceFromQuery,
      savedAttempt: attempt
    });
    const result = normalizeResult(searchParams.get("result"));

    const params = new URLSearchParams({ result });
    if (orderId) {
      params.set("orderId", orderId);
    }

    if (paymentIntentId) {
      params.set("paymentIntentId", paymentIntentId);
    }

    if (providerReference) {
      params.set("providerReference", providerReference);
    }

    navigate(`/payments/status/payment-sent?${params.toString()}`, { replace: true });
  }, [navigate, searchParams]);

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container py-5">
        <h3 className="alt mb-3">Finalizing payment</h3>
        <p>Please wait while we confirm your payment status.</p>
      </div>
    </main>
  );
};
