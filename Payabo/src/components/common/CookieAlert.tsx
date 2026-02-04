import { useEffect, useState } from "react";
import { NavLink } from "react-router-dom";

export const CookieAlert = () => {
  const [isVisible, setIsVisible] = useState(false);

  useEffect(() => {
    const storedChoice = window.localStorage.getItem("payabo_cookie_choice");
    setIsVisible(!storedChoice);
  }, []);

  const handleChoice = (choice: "accepted" | "declined") => {
    window.localStorage.setItem("payabo_cookie_choice", choice);
    setIsVisible(false);
  };

  if (!isVisible) {
    return null;
  }

  return (
    <div className="alert cookiealert fixed-bottom" role="alert">
      <div className="container">
        <div className="row align-items-center">
          <div className="col-lg-8 col-xl-9">
            <h4 className="mb-2">Can we use optional cookies?</h4>
            <p className="mb-4">
              We’re not talking about the crunchy, tasty kind. These cookies help us keep our website safe, give you a
              better experience and show more relevant ads. We won’t turn them on unless you accept. Want to know more
              or adjust your preferences? <NavLink className="text-underline" to="/cookies">Here’s our cookie notice.</NavLink>
            </p>
          </div>
          <div className="col-lg-4 col-xl-3 text-lg-end">
            <button className="btn btn-primary btn-sm mb-4 me-2" type="button" onClick={() => handleChoice("accepted")}>
              ACCEPT
            </button>
            <button className="btn btn-secondary btn-sm mb-4" type="button" onClick={() => handleChoice("declined")}>
              DECLINE
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
