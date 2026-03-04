import { useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";

import {
  getPublicCatalogBillerCategories,
  getPublicCatalogBillers,
  getPublicCatalogCountries,
  type CatalogBiller,
  type CatalogBillerCategory,
  type CatalogCountry,
  type CatalogPagination
} from "../../api/catalog";

const refreshSelects = () => {
  window.requestAnimationFrame(() => {
    window.dispatchEvent(new Event("payabo:refresh-selects"));
  });
};

const normalizeCountryCode = (value: string | null) => value?.trim().toUpperCase() ?? "";

export const ProviderList = () => {
  const [searchParams] = useSearchParams();
  const requestedCountryCode = useMemo(() => normalizeCountryCode(searchParams.get("countryCode")), [searchParams]);

  const [countries, setCountries] = useState<CatalogCountry[]>([]);
  const [selectedCountry, setSelectedCountry] = useState<string>(requestedCountryCode);
  const [countriesError, setCountriesError] = useState<string | null>(null);
  const [isLoadingCountries, setIsLoadingCountries] = useState<boolean>(true);

  const [categories, setCategories] = useState<CatalogBillerCategory[]>([]);
  const [selectedCategoryId, setSelectedCategoryId] = useState<string>("");
  const [categoriesError, setCategoriesError] = useState<string | null>(null);
  const [isLoadingCategories, setIsLoadingCategories] = useState<boolean>(false);

  const [search, setSearch] = useState<string>("");
  const [debouncedSearch, setDebouncedSearch] = useState<string>("");
  const [page, setPage] = useState<number>(1);

  const [billers, setBillers] = useState<CatalogBiller[]>([]);
  const [pagination, setPagination] = useState<CatalogPagination | null>(null);
  const [billersError, setBillersError] = useState<string | null>(null);
  const [isLoadingBillers, setIsLoadingBillers] = useState<boolean>(true);
  const [isLoadingMore, setIsLoadingMore] = useState<boolean>(false);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      setDebouncedSearch(search.trim());
    }, 300);

    return () => {
      window.clearTimeout(timeout);
    };
  }, [search]);

  useEffect(() => {
    let cancelled = false;

    const loadCountries = async () => {
      setIsLoadingCountries(true);
      setCountriesError(null);

      try {
        const result = await getPublicCatalogCountries();
        if (cancelled) {
          return;
        }

        setCountries(result);
        setSelectedCountry((current) => {
          const currentValue = normalizeCountryCode(current);
          if (currentValue && result.some((country) => country.code.toUpperCase() === currentValue)) {
            return currentValue;
          }

          return result[0]?.code ?? "";
        });
        refreshSelects();
      } catch {
        if (cancelled) {
          return;
        }

        setCountries([]);
        setSelectedCountry("");
        setCountriesError("We couldn't load countries right now. Please try again.");
      } finally {
        if (!cancelled) {
          setIsLoadingCountries(false);
        }
      }
    };

    void loadCountries();

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    let cancelled = false;

    const loadCategories = async () => {
      if (!selectedCountry) {
        setCategories([]);
        setSelectedCategoryId("");
        setCategoriesError(null);
        setIsLoadingCategories(false);
        return;
      }

      setIsLoadingCategories(true);
      setCategoriesError(null);

      try {
        const result = await getPublicCatalogBillerCategories({ countryCode: selectedCountry });
        if (cancelled) {
          return;
        }

        setCategories(result);
        setSelectedCategoryId((current) => {
          if (current && result.some((category) => category.id === current)) {
            return current;
          }

          return "";
        });
        refreshSelects();
      } catch {
        if (cancelled) {
          return;
        }

        setCategories([]);
        setSelectedCategoryId("");
        setCategoriesError("We couldn't load bill categories right now. Please try again.");
      } finally {
        if (!cancelled) {
          setIsLoadingCategories(false);
        }
      }
    };

    void loadCategories();

    return () => {
      cancelled = true;
    };
  }, [selectedCountry]);

  useEffect(() => {
    setPage(1);
  }, [selectedCountry, selectedCategoryId, debouncedSearch]);

  useEffect(() => {
    let cancelled = false;

    const loadBillers = async () => {
      if (!selectedCountry) {
        setBillers([]);
        setPagination(null);
        setBillersError(null);
        setIsLoadingBillers(false);
        setIsLoadingMore(false);
        return;
      }

      const isLoadingNextPage = page > 1;
      if (isLoadingNextPage) {
        setIsLoadingMore(true);
      } else {
        setIsLoadingBillers(true);
      }
      setBillersError(null);

      try {
        const result = await getPublicCatalogBillers({
          countryCode: selectedCountry,
          categoryId: selectedCategoryId || undefined,
          search: debouncedSearch || undefined,
          page,
          pageSize: 12
        });

        if (cancelled) {
          return;
        }

        setPagination(result.pagination);
        setBillers((current) => {
          if (page === 1) {
            return result.billers;
          }

          const existing = new Set(current.map((item) => item.id));
          const next = result.billers.filter((item) => !existing.has(item.id));
          return [...current, ...next];
        });
      } catch {
        if (cancelled) {
          return;
        }

        if (page === 1) {
          setBillers([]);
          setPagination(null);
        }
        setBillersError("We couldn't load providers right now. Please try again.");
      } finally {
        if (!cancelled) {
          setIsLoadingBillers(false);
          setIsLoadingMore(false);
        }
      }
    };

    void loadBillers();

    return () => {
      cancelled = true;
    };
  }, [selectedCountry, selectedCategoryId, debouncedSearch, page]);

  const hasMore = (pagination?.page ?? 0) < (pagination?.totalPages ?? 0);

  return (
    <main className="main-wrapper overflow-hidden">
      <div className="container">
        <div className="row">
          <div className="col-lg-4 col-xl-3">
            <div className="main-sidebar pt-5">
              <img className="mb-5 mt-2 w-100" src="/images/promo-banner.png" alt="Promo Banner" />
              <h3 className="alt mt-4">Contact us</h3>
              <div className="contact-info">
                <img className="contact-info-img" src="/images/illustration-contactus.png" alt="Contact support" />
                <p>Need help choosing a provider? Our support team can guide you.</p>
                <h6>Support channels</h6>
                <ul>
                  <li>
                    <a href="tel:+44123456789">+44 123 456 789</a>
                  </li>
                  <li>
                    <a href="mailto:mail@mybillafrica.com">mail@mybillafrica.com</a>
                  </li>
                  <li>All days: 8AM - 5PM</li>
                </ul>
              </div>
            </div>
          </div>
          <div className="col-lg-8 col-xl-9">
            <div className="wrapper-content">
              <div className="row align-items-end mb-md-2">
                <div className="col-md-10 col-xl-6">
                  <Link className="back-left-arrow" to="/">
                    <svg width="16" height="16" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                      <path
                        d="M-7.69392e-05 8.00008L8 0L9.60002 1.60002L3.19995 8.00008L9.60002 14.4001L8 16.0002L-7.69392e-05 8.00008Z"
                        fill="currentColor"
                      />
                    </svg>
                    Back to Homepage
                  </Link>
                  <h3 className="alt mt-4 mb-3">Select the service provider</h3>
                  <p>Find the service provider you would like to pay your bill for.</p>
                </div>
              </div>

              <div className="row row-cols-1 row-cols-md-2 row-cols-xl-3 align-items-end mb-3">
                <div className="col">
                  <div className="form-group">
                    <label htmlFor="destinationCountry">Destination country</label>
                    <select
                      className="form-control countries"
                      data-placeholder="Select country..."
                      id="destinationCountry"
                      value={selectedCountry}
                      onChange={(event) => setSelectedCountry(event.target.value)}
                      disabled={isLoadingCountries || countries.length === 0}
                    >
                      {isLoadingCountries && <option value="">Loading countries...</option>}
                      {!isLoadingCountries && countries.length === 0 && <option value="">No countries available</option>}
                      {countries.map((country) => (
                        <option key={country.code} value={country.code}>
                          {country.name}
                        </option>
                      ))}
                    </select>
                    {countriesError && <p className="text-danger small mt-2 mb-0">{countriesError}</p>}
                  </div>
                </div>

                <div className="col">
                  <div className="form-group">
                    <label htmlFor="billCategories">Bill category</label>
                    <select
                      className="form-control categories"
                      data-placeholder="Select bill category"
                      id="billCategories"
                      value={selectedCategoryId}
                      onChange={(event) => setSelectedCategoryId(event.target.value)}
                      disabled={isLoadingCountries || isLoadingCategories || categories.length === 0}
                    >
                      <option value="">All categories</option>
                      {categories.map((category) => (
                        <option key={category.id} value={category.id} data-img={category.iconUrl ?? ""}>
                          {category.name}
                        </option>
                      ))}
                    </select>
                    {categoriesError && <p className="text-danger small mt-2 mb-0">{categoriesError}</p>}
                  </div>
                </div>

                <div className="col-md-12 col-lg-12">
                  <form
                    onSubmit={(event) => {
                      event.preventDefault();
                    }}
                  >
                    <div className="form-group">
                      <div className="input-group search-box">
                        <span className="input-group-text">
                          <svg width="25" height="25" viewBox="0 0 25 25" fill="none" xmlns="http://www.w3.org/2000/svg">
                            <path d="M10.1499 10.1484L24.0015 24.002" stroke="#B4BFC3" strokeWidth="2" />
                            <path
                              d="M10 20C15.5228 20 20 15.5228 20 10C20 4.47715 15.5228 0 10 0C4.47715 0 0 4.47715 0 10C0 15.5228 4.47715 20 10 20Z"
                              fill="white"
                            />
                            <path
                              d="M10 19C14.9706 19 19 14.9706 19 10C19 5.02944 14.9706 1 10 1C5.02944 1 1 5.02944 1 10C1 14.9706 5.02944 19 10 19Z"
                              stroke="#B4BFC3"
                              strokeWidth="2"
                            />
                          </svg>
                        </span>
                        <input
                          type="text"
                          className="form-control"
                          placeholder="Search for a provider"
                          value={search}
                          onChange={(event) => setSearch(event.target.value)}
                          disabled={isLoadingCountries || countries.length === 0}
                        />
                        <button
                          type="button"
                          className="search-close"
                          onClick={() => setSearch("")}
                          aria-label="Clear search"
                        >
                          X
                        </button>
                      </div>
                    </div>
                  </form>
                </div>
              </div>

              {isLoadingBillers && page === 1 && <p>Loading providers...</p>}
              {billersError && <p className="text-danger mb-3">{billersError}</p>}

              {!isLoadingBillers && !billersError && billers.length === 0 && (
                <p>No providers found for your current filters.</p>
              )}

              <div className="row row-cols-1 row-cols-md-2 row-cols-xl-3">
                {billers.map((biller) => (
                  <div key={biller.id} className="col mb-4">
                    <div className="card icard h-100">
                      <div className="card-md-img">
                        <img className="card-img-top" src={biller.logoUrl ?? "/images/product-img-01.png"} alt={biller.name} />
                      </div>
                      <div className="card-body">
                        <h4>{biller.name}</h4>
                        <p>{biller.isFeatured ? "Featured provider" : "Bill payment provider"}</p>
                      </div>
                      <div className="card-footer">
                        <Link
                          className="btn btn-primary btn-sm w-100"
                          to={`/payments/service/${biller.id}?countryCode=${encodeURIComponent(selectedCountry)}&billerName=${encodeURIComponent(biller.name)}`}
                        >
                          SELECT PROVIDER
                        </Link>
                      </div>
                    </div>
                  </div>
                ))}
              </div>

              {hasMore && (
                <div className="text-center mt-4 mb-3">
                  <button
                    type="button"
                    className="btn btn-secondary btn-lg"
                    onClick={() => setPage((current) => current + 1)}
                    disabled={isLoadingMore}
                  >
                    {isLoadingMore ? "LOADING..." : "LOAD MORE..."}
                  </button>
                </div>
              )}
            </div>
          </div>
        </div>
      </div>
    </main>
  );
};
