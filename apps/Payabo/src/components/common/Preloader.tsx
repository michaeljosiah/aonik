import { useEffect, useState } from "react";

export const Preloader = () => {
  const [isVisible, setIsVisible] = useState(true);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setIsVisible(false);
    }, 500);

    return () => {
      window.clearTimeout(timeoutId);
    };
  }, []);

  if (!isVisible) {
    return null;
  }

  return (
    <div id="loading">
      <div id="loading-center">
        <div id="loading-center-absolute">
          <img className="logo-icon" src="/images/mba-logo-icon.gif" alt="MBA" />
          <img src="/images/logo-icon-bottom.png" alt="MyBillAfrica" />
          <span className="loding-text">Loading ...</span>
        </div>
      </div>
    </div>
  );
};
