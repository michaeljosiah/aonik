import { StatusPaymentSent } from "./StatusPaymentSent";

export const StatusBillPaidFailed = () => {
  return <StatusPaymentSent forcedResult="failed" />;
};
