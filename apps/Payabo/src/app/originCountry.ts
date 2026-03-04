export type OriginCountryCode = "GB" | "GH" | "NG";

export type OriginCountry = {
  code: OriginCountryCode;
  name: string;
  currency: string;
  flagSrc: string;
};

const originCountryStorageKey = "payabo:origin-country";
const defaultOriginCountryCode: OriginCountryCode = "GB";

export const originCountries: OriginCountry[] = [
  { code: "GB", name: "United Kingdom", currency: "GBP", flagSrc: "/images/flags/gb.svg" },
  { code: "GH", name: "Ghana", currency: "GHS", flagSrc: "/images/flags/gh.svg" },
  { code: "NG", name: "Nigeria", currency: "NGN", flagSrc: "/images/flags/ng.svg" }
];

export const normalizeOriginCountryCode = (value: string | null | undefined): OriginCountryCode => {
  const normalized = value?.trim().toUpperCase();
  if (normalized === "GH" || normalized === "NG" || normalized === "GB") {
    return normalized;
  }

  return defaultOriginCountryCode;
};

export const getOriginCountryByCode = (code: OriginCountryCode): OriginCountry => {
  return originCountries.find((item) => item.code === code) ?? originCountries[0];
};

export const getSelectedOriginCountryCode = (): OriginCountryCode => {
  if (typeof window === "undefined") {
    return defaultOriginCountryCode;
  }

  return normalizeOriginCountryCode(window.localStorage.getItem(originCountryStorageKey));
};

export const getSelectedOriginCountry = (): OriginCountry => {
  return getOriginCountryByCode(getSelectedOriginCountryCode());
};

export const setSelectedOriginCountryCode = (code: OriginCountryCode): OriginCountry => {
  const normalizedCode = normalizeOriginCountryCode(code);
  if (typeof window !== "undefined") {
    window.localStorage.setItem(originCountryStorageKey, normalizedCode);
    window.dispatchEvent(new CustomEvent("payabo:origin-country-changed", { detail: { code: normalizedCode } }));
  }

  return getOriginCountryByCode(normalizedCode);
};
