import { apiDelete, apiGet, apiPostForm, apiPut } from "./client";

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

export const updateCustomerProfile = async (request: {
  firstName?: string | null;
  lastName?: string | null;
  title?: string | null;
  phone?: string | null;
  countryCode?: string | null;
}): Promise<CustomerProfile> => {
  return await apiPut<CustomerProfile>("/profiles/customers/me", {
    firstName: request.firstName ?? null,
    lastName: request.lastName ?? null,
    title: request.title ?? null,
    phone: request.phone ?? null,
    countryCode: request.countryCode ?? null
  });
};

export const updateCustomerEmail = async (request: {
  currentEmail: string;
  newEmail: string;
  password: string;
}): Promise<CustomerProfile> => {
  return await apiPut<CustomerProfile>("/profiles/customers/me/email", request);
};

export const updateCustomerPassword = async (request: {
  currentPassword: string;
  newPassword: string;
}): Promise<{ status: string }> => {
  return await apiPut<{ status: string }>("/profiles/customers/me/password", request);
};

export const uploadCustomerPhoto = async (file: File): Promise<{ photoUrl: string }> => {
  const form = new FormData();
  form.append("file", file);
  return await apiPostForm<{ photoUrl: string }>("/profiles/customers/me/photo", form);
};

export const deleteCustomerPhoto = async (): Promise<{ status: string }> => {
  return await apiDelete<{ status: string }>("/profiles/customers/me/photo");
};
