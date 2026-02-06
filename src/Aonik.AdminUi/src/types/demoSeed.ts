export type DemoSeedType = 'BillCollection' | 'CrossBorderPayments';

export interface DemoSeedResponse {
  tenantId: string;
  seedType: DemoSeedType;
  seededAt: string;
  operations: string[];
}
