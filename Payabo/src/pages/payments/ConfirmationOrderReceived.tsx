import { StatusPaymentSent } from "./StatusPaymentSent";

export const ConfirmationOrderReceived = () => {
  return <StatusPaymentSent forcedResult="pending" />;
};
