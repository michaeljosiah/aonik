import { useMemo, useState, type FormEvent, type MouseEvent } from "react";
import { NavLink, useLocation, useNavigate } from "react-router-dom";

import { useAuth } from "../../app/auth/AuthContext";

type TabKey = "personal" | "business";

type LocationState = {
  from?: string;
};

type RegistrationCountry = {
  code: string;
  name: string;
  capital?: string;
};

export const Register = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const { register } = useAuth();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const from = (location.state as LocationState | null)?.from;

  const [activeTab, setActiveTab] = useState<TabKey>("personal");

  const countries = useMemo<RegistrationCountry[]>(
    () => [
      { code: "AF", name: "Afghanistan", capital: "Kabul" },
      { code: "AL", name: "Albania", capital: "Tirana" },
      { code: "DZ", name: "Algeria", capital: "Algiers" },
      { code: "AO", name: "Angola", capital: "Luanda" },
      { code: "AR", name: "Argentina", capital: "Buenos Aires" },
      { code: "AM", name: "Armenia", capital: "Yerevan" },
      { code: "AU", name: "Australia", capital: "Canberra" },
      { code: "AT", name: "Austria", capital: "Vienna" },
      { code: "AZ", name: "Azerbaijan", capital: "Baku" },
      { code: "BH", name: "Bahrain", capital: "Manama" },
      { code: "BD", name: "Bangladesh", capital: "Dhaka" },
      { code: "BE", name: "Belgium", capital: "Brussels" },
      { code: "BJ", name: "Benin", capital: "Porto-Novo" },
      { code: "BW", name: "Botswana", capital: "Gaborone" },
      { code: "BR", name: "Brazil", capital: "Brasilia" },
      { code: "BF", name: "Burkina Faso", capital: "Ouagadougou" },
      { code: "BI", name: "Burundi", capital: "Bujumbura" },
      { code: "CM", name: "Cameroon", capital: "Yaounde" },
      { code: "CA", name: "Canada", capital: "Ottawa" },
      { code: "CF", name: "Central African Republic", capital: "Bangui" },
      { code: "TD", name: "Chad", capital: "N'Djamena" },
      { code: "CL", name: "Chile", capital: "Santiago" },
      { code: "CN", name: "China", capital: "Beijing" },
      { code: "CO", name: "Colombia", capital: "Bogota" },
      { code: "CG", name: "Republic of the Congo", capital: "Brazzaville" },
      { code: "CD", name: "Democratic Republic of the Congo", capital: "Kinshasa" },
      { code: "CI", name: "Cote d'Ivoire", capital: "Yamoussoukro" },
      { code: "EG", name: "Egypt", capital: "Cairo" },
      { code: "ET", name: "Ethiopia", capital: "Addis Ababa" },
      { code: "FR", name: "France", capital: "Paris" },
      { code: "GA", name: "Gabon", capital: "Libreville" },
      { code: "GM", name: "Gambia", capital: "Banjul" },
      { code: "DE", name: "Germany", capital: "Berlin" },
      { code: "GH", name: "Ghana", capital: "Accra" },
      { code: "GN", name: "Guinea", capital: "Conakry" },
      { code: "KE", name: "Kenya", capital: "Nairobi" },
      { code: "LR", name: "Liberia", capital: "Monrovia" },
      { code: "MW", name: "Malawi", capital: "Lilongwe" },
      { code: "ML", name: "Mali", capital: "Bamako" },
      { code: "MA", name: "Morocco", capital: "Rabat" },
      { code: "MZ", name: "Mozambique", capital: "Maputo" },
      { code: "NA", name: "Namibia", capital: "Windhoek" },
      { code: "NE", name: "Niger", capital: "Niamey" },
      { code: "NG", name: "Nigeria", capital: "Abuja" },
      { code: "PK", name: "Pakistan", capital: "Islamabad" },
      { code: "RW", name: "Rwanda", capital: "Kigali" },
      { code: "SN", name: "Senegal", capital: "Dakar" },
      { code: "SL", name: "Sierra Leone", capital: "Freetown" },
      { code: "SO", name: "Somalia", capital: "Mogadishu" },
      { code: "ZA", name: "South Africa", capital: "Pretoria" },
      { code: "SS", name: "South Sudan", capital: "Juba" },
      { code: "TZ", name: "Tanzania", capital: "Dodoma" },
      { code: "TG", name: "Togo", capital: "Lome" },
      { code: "UG", name: "Uganda", capital: "Kampala" },
      { code: "GB", name: "United Kingdom", capital: "London" },
      { code: "US", name: "United States", capital: "Washington" },
      { code: "ZM", name: "Zambia", capital: "Lusaka" },
      { code: "ZW", name: "Zimbabwe", capital: "Harare" }
    ],
    []
  );

  const [personalCountry, setPersonalCountry] = useState<string>("");
  const [personalFirstName, setPersonalFirstName] = useState<string>("");
  const [personalLastName, setPersonalLastName] = useState<string>("");
  const [personalEmail, setPersonalEmail] = useState<string>("");
  const [personalPhone, setPersonalPhone] = useState<string>("");
  const [personalPassword, setPersonalPassword] = useState<string>("");

  const [businessCountry, setBusinessCountry] = useState<string>("");
  const [businessFirstName, setBusinessFirstName] = useState<string>("");
  const [businessLastName, setBusinessLastName] = useState<string>("");
  const [businessEmail, setBusinessEmail] = useState<string>("");
  const [businessPassword, setBusinessPassword] = useState<string>("");

  const handleTabClick = (tab: TabKey, event: MouseEvent<HTMLAnchorElement>) => {
    event.preventDefault();
    setActiveTab(tab);
  };

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setErrorMessage(null);
    setIsSubmitting(true);

    try {
      if (activeTab === "personal") {
        const firstName = personalFirstName.trim();
        const lastName = personalLastName.trim();
        const email = personalEmail.trim();
        const phone = personalPhone.trim();

        if (!firstName || !lastName || !email || !personalPassword) {
          setErrorMessage("Please complete all required fields.");
          return;
        }

        await register({
          firstName,
          lastName,
          email,
          phone: phone || undefined,
          password: personalPassword,
          registrationCountry: personalCountry || undefined
        });
        return;
      }

      const firstName = businessFirstName.trim();
      const lastName = businessLastName.trim();
      const email = businessEmail.trim();

      if (!firstName || !lastName || !email || !businessPassword) {
        setErrorMessage("Please complete all required fields.");
        return;
      }

      await register({
        firstName,
        lastName,
        email,
        password: businessPassword,
        registrationCountry: businessCountry || undefined
      });
    } catch {
      setErrorMessage("Unable to register at the moment. Please try again.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleClose = () => {
    navigate("/", { replace: true });
  };

  return (
    <div className="fullscreen-xl">
      <button type="button" className="btn-close close" aria-label="Close" onClick={handleClose}></button>
      <div className="container">
        <div
          className="img-lg-full-left py-3 d-none d-lg-block"
          style={{ backgroundImage: "url('/images/MBA_img_login_reg.jpg')" }}
        ></div>
      </div>
      <div className="container-fluid">
        <div className="row align-items-center">
          <div className="col-lg-6 d-lg-flex align-items-end min-vh-lg-100">
            <div className="info-box d-none d-lg-block text-white">
              <h2>
                HEADLINE <br />
                <strong>LOREM IPSUM</strong>
              </h2>
              <p>
                Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nullam pretium, augue luctus lobortis
                vestibulum, nunc leo luctus massa.
              </p>
            </div>
          </div>
          <div className="col-lg-6">
            <div className="login-content">
              <div className="login-header text-center">
                <img className="mb-4" src="/images/payabo_logo_horizontal.png" alt="Payabo" />
                <h4>Register now, it's free!</h4>
                <p>
                  Already have an account? <NavLink to="/login" state={from ? { from } : undefined}>Login now</NavLink>
                </p>
              </div>
              <nav>
                <div className="login-tabs nav nav-fill">
                  <a
                    className={`nav-link ${activeTab === "personal" ? "active" : ""}`}
                    data-bs-toggle="tab"
                    href="#tab-1"
                    onClick={(event) => handleTabClick("personal", event)}
                  >
                    PERSONAL
                  </a>
                  <a
                    className={`nav-link ${activeTab === "business" ? "active" : ""}`}
                    data-bs-toggle="tab"
                    href="#tab-2"
                    onClick={(event) => handleTabClick("business", event)}
                  >
                    BUSINESS
                  </a>
                </div>
              </nav>

              <div className="tab-content">
                <div className={`tab-pane fade ${activeTab === "personal" ? "show active" : ""}`} id="tab-1">
                  <form className="form" action="#" method="post" onSubmit={handleSubmit}>
                    {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}
                    <div className="form-group">
                      <label htmlFor="RegistrationCountries">Registration country</label>
                      <div className="select">
                        <select
                          className="form-control countries"
                          data-placeholder="Select country..."
                          id="RegistrationCountries"
                          value={personalCountry}
                          onChange={(e) => setPersonalCountry(e.target.value)}
                        >
                          <option value=""></option>
                          {countries.map((country) => (
                            <option key={country.code} value={country.code} data-capital={country.capital}>
                              {country.name}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>

                    <div className="row">
                      <div className="col-md-6 form-group">
                        <label htmlFor="fast-name-personal">First name</label>
                        <input
                          type="text"
                          className="form-control"
                          id="fast-name-personal"
                          name="PersonalFirstName"
                          placeholder="Your first name"
                          value={personalFirstName}
                          onChange={(e) => setPersonalFirstName(e.target.value)}
                          required
                        />
                      </div>
                      <div className="col-md-6 form-group">
                        <label htmlFor="last-name-personal">Last name</label>
                        <input
                          type="text"
                          className="form-control"
                          id="last-name-personal"
                          name="PersonalLirstName"
                          placeholder="Your last name"
                          value={personalLastName}
                          onChange={(e) => setPersonalLastName(e.target.value)}
                          required
                        />
                      </div>
                    </div>

                    <div className="form-group">
                      <label htmlFor="email-personal">Email</label>
                      <input
                        type="email"
                        className="form-control"
                        id="email-personal"
                        name="PersonalEmail"
                        placeholder="Your email address"
                        value={personalEmail}
                        onChange={(e) => setPersonalEmail(e.target.value)}
                        required
                      />
                    </div>

                    <div className="form-group">
                      <label htmlFor="phone">Contact number</label>
                      <div className="position-relative">
                        <input
                          id="phone"
                          className="form-control w-100"
                          name="phone"
                          type="tel"
                          value={personalPhone}
                          onChange={(e) => setPersonalPhone(e.target.value)}
                        />
                      </div>
                    </div>

                    <div className="form-group">
                      <label htmlFor="password-personal">Password</label>
                      <div className="input-group">
                        <input
                          type="password"
                          className="form-control"
                          id="password-personal"
                          name="PersonalPassword"
                          placeholder="Create a password"
                          value={personalPassword}
                          onChange={(e) => setPersonalPassword(e.target.value)}
                          required
                        />
                        <div className="input-group-append">
                          <span className="input-group-text">
                            <i toggle="#password-personal" className="toggle-password icon-eye"></i>
                          </span>
                        </div>
                      </div>
                    </div>

                    <div className="py-4 mb-lg-1">
                      <button type="submit" className="btn btn-primary w-100" disabled={isSubmitting}>
                        {isSubmitting ? "CREATING ACCOUNT..." : "REGISTER ACCOUNT"}
                      </button>
                    </div>

                    <div className="text-center">
                      <p className="small">
                        By continuing you agree with<br /> <a href="#">our Terms and Conditions</a> and <NavLink to="/privacy">Privacy Policy</NavLink>.
                      </p>
                    </div>
                  </form>
                </div>

                <div className={`tab-pane fade ${activeTab === "business" ? "show active" : ""}`} id="tab-2">
                  <form className="form" action="#" method="post" onSubmit={handleSubmit}>
                    {errorMessage && <div className="alert alert-danger">{errorMessage}</div>}
                    <div className="form-group">
                      <label htmlFor="registrationCountry1">Registration country</label>
                      <div className="select">
                        <select
                          className="form-control countries"
                          data-placeholder="Select country..."
                          id="registrationCountry1"
                          value={businessCountry}
                          onChange={(e) => setBusinessCountry(e.target.value)}
                        >
                          <option value=""></option>
                          {countries.map((country) => (
                            <option key={country.code} value={country.code} data-capital={country.capital}>
                              {country.name}
                            </option>
                          ))}
                        </select>
                      </div>
                    </div>

                    <div className="row">
                      <div className="col-md-6 form-group">
                        <label htmlFor="fast-name-business">First name</label>
                        <input
                          type="text"
                          className="form-control"
                          id="fast-name-business"
                          name="BusinessFirstName"
                          placeholder="Your first name"
                          value={businessFirstName}
                          onChange={(e) => setBusinessFirstName(e.target.value)}
                          required
                        />
                      </div>
                      <div className="col-md-6 form-group">
                        <label htmlFor="last-name-business">Last name</label>
                        <input
                          type="text"
                          className="form-control"
                          id="last-name-business"
                          name="BusinesslLirstName"
                          placeholder="Your last name"
                          value={businessLastName}
                          onChange={(e) => setBusinessLastName(e.target.value)}
                          required
                        />
                      </div>
                    </div>

                    <div className="form-group">
                      <label htmlFor="email-business">Email</label>
                      <input
                        type="email"
                        className="form-control"
                        id="email-business"
                        name="BusinessEmail"
                        placeholder="Your email address"
                        value={businessEmail}
                        onChange={(e) => setBusinessEmail(e.target.value)}
                        required
                      />
                    </div>

                    <div className="form-group">
                      <label htmlFor="password-business">Password</label>
                      <div className="input-group">
                        <input
                          type="password"
                          className="form-control"
                          id="password-business"
                          name="BusinessPassword"
                          placeholder="Create a password"
                          value={businessPassword}
                          onChange={(e) => setBusinessPassword(e.target.value)}
                          required
                        />
                        <div className="input-group-append">
                          <span className="input-group-text">
                            <i toggle="#password-business" className="toggle-password icon-eye"></i>
                          </span>
                        </div>
                      </div>
                    </div>

                    <div className="py-4 mb-lg-1">
                      <button type="submit" className="btn btn-primary w-100" disabled={isSubmitting}>
                        {isSubmitting ? "CREATING ACCOUNT..." : "REGISTER ACCOUNT"}
                      </button>
                    </div>

                    <div className="text-center">
                      <p className="small">
                        By continuing you agree with<br /> <a href="#">our Terms and Conditions</a> and <NavLink to="/privacy">Privacy Policy</NavLink>.
                      </p>
                    </div>
                  </form>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
