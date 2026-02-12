import { apiGet } from "./client";

export type CustomerProfile = {
  partyId: string;
  userId: string;
  tenantId: string;
  email: string;
  firstName: string | null;
  lastName: string | null;
  title: string | null;
  phone: string | null;
  countryCode: string | null;
  photoUrl: string | null;
};

export const getCustomerProfile = async (): Promise<CustomerProfile> => {
  return await apiGet<CustomerProfile>("/profiles/customers/me");
};
