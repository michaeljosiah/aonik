export interface RefundSelection {
  componentId: string;
  amount: number | null;
  fullRemaining: boolean;
}

export interface RefundDraft {
  reason: string;
  selections: RefundSelection[];
}

export interface RefundRequest extends RefundDraft {
  refundId: string;
  expectedPreviewVersion: string;
}

export interface RefundComponentDto {
  componentId: string;
  kind: string;
  label: string;
  originalAmount: number;
  remainingAmount: number;
  canRefund: boolean;
  unavailableReason: string | null;
}

export interface RefundPreviewDto {
  currency: string;
  version: string;
  selections: RefundSelection[];
  total: number;
  cashAmount: number;
  giftAmount: number;
  redeemedPointsToRestore: number;
  earnedPointsToReverse: number;
  warnings: string[];
}

export interface RefundDto {
  refundId: string;
  orderId: string;
  status: string;
  requestedAtUtc: string;
  requestedBy: string | null;
  reason: string;
  total: number;
  currency: string;
  cashAmount: number;
  giftAmount: number;
  redeemedPointsToRestore: number;
  earnedPointsToReverse: number;
  effectsAppliedAtUtc: string | null;
  failureReason: string | null;
  canReconcile: boolean;
}

export interface RefundContextDto {
  currency: string;
  canRequest: boolean;
  disabledReason: string | null;
  remainingTotal: number;
  status: string;
  components: RefundComponentDto[];
  history: RefundDto[];
}
