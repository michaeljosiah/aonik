import { useState } from "react";
import { NavLink } from "react-router-dom";

type NavSection = "bills" | "transactions" | "wallet" | "payAssist" | null;

export const SidebarNav = () => {
  const [openSection, setOpenSection] = useState<NavSection>("bills");

  const toggleSection = (section: NavSection) => {
    setOpenSection((current) => (current === section ? null : section));
  };

  return (
    <div className="main-sidebar">
      <div id="accordion-nav">
        <div className="panel list-nav">
          <NavLink to="/dashboard" className="list-nav-item d-flex align-items-center">
            <span className="list-nav-icon">
              <svg width="24" height="23" viewBox="0 0 24 23" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path
                  d="M23.121 8.69115L15.536 1.42123C14.5973 0.524222 13.3257 0.0205078 12 0.0205078C10.6744 0.0205078 9.40277 0.524222 8.46401 1.42123L0.879012 8.69115C0.599438 8.95736 0.377782 9.2741 0.226895 9.62302C0.0760072 9.97194 -0.0011104 10.3461 1.20795e-05 10.7238V20.1317C1.20795e-05 20.8942 0.316083 21.6255 0.878692 22.1647C1.4413 22.7038 2.20436 23.0067 3.00001 23.0067H21C21.7957 23.0067 22.5587 22.7038 23.1213 22.1647C23.6839 21.6255 24 20.8942 24 20.1317V10.7238C24.0011 10.3461 23.924 9.97194 23.7731 9.62302C23.6222 9.2741 23.4006 8.95736 23.121 8.69115V8.69115ZM15 21.0901H9.00001V17.32C9.00001 16.5575 9.31608 15.8262 9.87869 15.2871C10.4413 14.7479 11.2044 14.445 12 14.445C12.7957 14.445 13.5587 14.7479 14.1213 15.2871C14.6839 15.8262 15 16.5575 15 17.32V21.0901ZM22 20.1317C22 20.3859 21.8947 20.6297 21.7071 20.8094C21.5196 20.9891 21.2652 21.0901 21 21.0901H17V17.32C17 16.0492 16.4732 14.8304 15.5355 13.9318C14.5979 13.0332 13.3261 12.5283 12 12.5283C10.6739 12.5283 9.40216 13.0332 8.46448 13.9318C7.5268 14.8304 7.00001 16.0492 7.00001 17.32V21.0901H3.00001C2.7348 21.0901 2.48044 20.9891 2.29291 20.8094C2.10537 20.6297 2.00001 20.3859 2.00001 20.1317V10.7238C2.00094 10.4698 2.1062 10.2264 2.29301 10.0462L9.87801 2.77919C10.4417 2.2415 11.2047 1.93964 12 1.93964C12.7953 1.93964 13.5583 2.2415 14.122 2.77919L21.707 10.0491C21.8931 10.2286 21.9983 10.4708 22 10.7238V20.1317Z"
                  fill="currentColor"
                />
              </svg>
            </span>
            Home
          </NavLink>
          <a
            href="#nav-list-01"
            className={`list-nav-item icon-collapsed d-flex align-items-center ${openSection === "bills" ? "" : "collapsed"}`}
            onClick={(event) => {
              event.preventDefault();
              toggleSection("bills");
            }}
          >
            <span className="list-nav-icon">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path
                  d="M19.95 18.464L16.465 21.949C15.143 23.271 13.385 23.999 11.515 23.999H7C4.243 24 2 21.757 2 19V5.00002C2 2.24302 4.243 2.00273e-05 7 2.00273e-05H17C19.757 2.00273e-05 22 2.24302 22 5.00002V13.515C22 15.385 21.272 17.142 19.95 18.465V18.464Z"
                  fill="currentColor"
                />
              </svg>
            </span>
            Bills
          </a>
          <div className={`collapse ${openSection === "bills" ? "show" : ""}`} id="nav-list-01">
            <ul className="list-sub-nav">
              <li>
                <NavLink to="/payments/providers">Pay a bill</NavLink>
              </li>
              <li>
                <a href="#">Outstanding bills</a>
              </li>
              <li>
                <a href="#">All bills</a>
              </li>
            </ul>
          </div>
          <a
            href="#nav-list-02"
            className={`list-nav-item icon-collapsed d-flex align-items-center ${openSection === "transactions" ? "" : "collapsed"}`}
            onClick={(event) => {
              event.preventDefault();
              toggleSection("transactions");
            }}
          >
            <span className="list-nav-icon">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path
                  d="M16.5 0C12.294 0 9 1.977 9 4.5V7.087C8.517 7.03 8.015 7 7.5 7C3.294 7 0 8.977 0 11.5V19.5C0 22.023 3.294 24 7.5 24C10.907 24 13.716 22.703 14.66 20.869C15.258 20.956 15.874 21 16.5 21C20.706 21 24 19.023 24 16.5V4.5C24 1.977 20.706 0 16.5 0Z"
                  fill="currentColor"
                />
              </svg>
            </span>
            Transactions
          </a>
          <div className={`collapse ${openSection === "transactions" ? "show" : ""}`} id="nav-list-02">
            <ul className="list-sub-nav">
              <li>
                <NavLink to="/transactions">All transactions</NavLink>
              </li>
              <li>
                <a href="#">Donations</a>
              </li>
            </ul>
          </div>
          <a
            href="#nav-list-03"
            className={`list-nav-item icon-collapsed d-flex align-items-center ${openSection === "wallet" ? "" : "collapsed"}`}
            onClick={(event) => {
              event.preventDefault();
              toggleSection("wallet");
            }}
          >
            <span className="list-nav-icon">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path
                  d="M21 6H5C4.141 6 3.328 5.628 2.765 5.001C3.315 4.387 4.114 4 5 4H23C23.553 4 24 3.552 24 3C24 2.448 23.553 2 23 2H5C2.239 2 0 4.239 0 7V17C0 19.761 2.239 22 5 22H21C22.657 22 24 20.657 24 19V9C24 7.343 22.657 6 21 6Z"
                  fill="currentColor"
                />
              </svg>
            </span>
            Wallet
          </a>
          <div className={`collapse ${openSection === "wallet" ? "show" : ""}`} id="nav-list-03">
            <ul className="list-sub-nav">
              <li>
                <a href="#">Budgeting</a>
              </li>
              <li>
                <NavLink to="/manage-cards">My cards</NavLink>
              </li>
              <li>
                <a href="#">Wallet settings</a>
              </li>
            </ul>
          </div>
          <a
            href="#nav-list-04"
            className={`list-nav-item icon-collapsed d-flex align-items-center ${openSection === "payAssist" ? "" : "collapsed"}`}
            onClick={(event) => {
              event.preventDefault();
              toggleSection("payAssist");
            }}
          >
            <span className="list-nav-icon">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path
                  d="M8.965 24H4C2.93913 24 1.92172 23.5786 1.17157 22.8284C0.421427 22.0783 0 21.0609 0 20V15C0 13.9391 0.421427 12.9217 1.17157 12.1716C1.92172 11.4214 2.93913 11 4 11H12.857C13.3982 11.0003 13.9302 11.1402 14.4014 11.4063C14.8727 11.6724 15.2673 12.0557 15.547 12.519L18.764 8.984C19.0301 8.69139 19.3512 8.45409 19.7091 8.28566C20.0669 8.11723 20.4545 8.02098 20.8496 8.00241C21.2446 7.98383 21.6395 8.0433 22.0116 8.17741C22.3836 8.31152 22.7256 8.51765 23.018 8.784C23.6014 9.31993 23.951 10.0635 23.9916 10.8546C24.0321 11.6458 23.7605 12.4212 23.235 13.014L16.435 20.651C15.4961 21.704 14.3453 22.5467 13.0579 23.1239C11.7706 23.701 10.3758 23.9996 8.965 24Z"
                  fill="currentColor"
                />
              </svg>
            </span>
            Pay assist requests
          </a>
          <div className={`collapse ${openSection === "payAssist" ? "show" : ""}`} id="nav-list-04">
            <ul className="list-sub-nav">
              <li>
                <a href="#">Outgoing (by me)</a>
              </li>
              <li>
                <a href="#">Ingoing (from others)</a>
              </li>
            </ul>
          </div>
          <a href="#" className="list-nav-item d-flex align-items-center">
            <span className="list-nav-icon">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path
                  d="M12 16C11.2089 16 10.4355 15.7654 9.77772 15.3259C9.11992 14.8864 8.60723 14.2616 8.30448 13.5307C8.00173 12.7998 7.92252 11.9956 8.07686 11.2196C8.2312 10.4437 8.61216 9.73098 9.17157 9.17157C9.73098 8.61216 10.4437 8.2312 11.2196 8.07686C11.9956 7.92252 12.7998 8.00173 13.5307 8.30448C14.2616 8.60723 14.8864 9.11992 15.3259 9.77772C15.7654 10.4355 16 11.2089 16 12C16 13.0609 15.5786 14.0783 14.8284 14.8284C14.0783 15.5786 13.0609 16 12 16Z"
                  fill="currentColor"
                />
              </svg>
            </span>
            Organisations
          </a>
          <a href="#" className="list-nav-item d-flex align-items-center">
            <span className="list-nav-icon">
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg">
                <path
                  d="M9 12C10.1867 12 11.3467 11.6481 12.3334 10.9888C13.3201 10.3295 14.0892 9.39246 14.5433 8.2961C14.9974 7.19975 15.1162 5.99335 14.8847 4.82946C14.6532 3.66558 14.0818 2.59648 13.2426 1.75736C12.4035 0.918247 11.3344 0.346802 10.1705 0.115291C9.00666 -0.11622 7.80026 0.00259972 6.7039 0.456726C5.60754 0.910851 4.67047 1.67989 4.01118 2.66658C3.35189 3.65328 3 4.81331 3 6C3.00159 7.59081 3.63424 9.11602 4.75911 10.2409C5.88399 11.3658 7.40919 11.9984 9 12Z"
                  fill="currentColor"
                />
              </svg>
            </span>
            Friends &amp; Recipients
          </a>
        </div>
      </div>
      <h3 className="alt mt-4">Contact us</h3>
      <div className="contact-info">
        <img className="contact-info-img" src="/images/illustration-contactus.png" alt="" />
        <p>Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor.</p>
        <h6>Contacts sub-title</h6>
        <ul>
          <li>
            <svg width="18" height="18" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path
                d="M9.74999 0.750221C9.74999 0.551309 9.82901 0.360543 9.96966 0.219891C10.1103 0.0792388 10.3011 0.000221163 10.5 0.000221163C12.4884 0.00240486 14.3948 0.793282 15.8009 2.19933C17.2069 3.60538 17.9978 5.51177 18 7.50022C18 7.69913 17.921 7.8899 17.7803 8.03055C17.6397 8.1712 17.4489 8.25022 17.25 8.25022C17.0511 8.25022 16.8603 8.1712 16.7197 8.03055C16.579 7.8899 16.5 7.69913 16.5 7.50022C16.4982 5.90947 15.8655 4.38439 14.7407 3.25955C13.6158 2.13472 12.0907 1.50201 10.5 1.50022C10.3011 1.50022 10.1103 1.4212 9.96966 1.28055C9.82901 1.1399 9.74999 0.949134 9.74999 0.750221V0.750221Z"
                fill="currentColor"
              />
            </svg>
            <a href="tel:+44123456789">+44 123 456 789</a>
          </li>
          <li>
            <svg width="18" height="18" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path
                d="M8.99956 0C6.61341 0.00258081 4.32572 0.951621 2.63845 2.63889C0.951185 4.32616 0.00214461 6.61384 -0.000436205 9C-0.0949362 16.1797 8.36881 20.5717 14.1746 16.3627C14.258 16.3073 14.3295 16.2356 14.3847 16.152C14.44 16.0684 14.478 15.9746 14.4964 15.8761C14.5148 15.7777 14.5133 15.6765 14.492 15.5786C14.4706 15.4807 14.4299 15.388 14.3722 15.3061C14.3144 15.2242 14.2409 15.1547 14.1558 15.1017C14.0708 15.0488 13.976 15.0133 13.877 14.9976C13.7781 14.9819 13.6769 14.9861 13.5797 15.0101C13.4824 15.0341 13.3909 15.0773 13.3106 15.1373C8.47456 18.642 1.42456 14.9835 1.49956 9C1.91131 -0.9495 16.0893 -0.94725 16.4996 9V10.5C16.4996 10.8978 16.3415 11.2794 16.0602 11.5607C15.7789 11.842 15.3974 12 14.9996 12C14.6017 12 14.2202 11.842 13.9389 11.5607C13.6576 11.2794 13.4996 10.8978 13.4996 10.5V9C13.3106 3.05325 4.68781 3.054 4.49956 9C4.50829 9.912 4.79315 10.7999 5.3166 11.5468C5.84005 12.2937 6.57751 12.8644 7.4318 13.1838C8.28609 13.5032 9.21711 13.5562 10.1021 13.3359C10.9872 13.1156 11.7847 12.6323 12.3896 11.9497C12.7149 12.524 13.2205 12.9749 13.8281 13.2326C14.4357 13.4904 15.1113 13.5406 15.7503 13.3754C16.3893 13.2103 16.956 12.8391 17.3627 12.3192C17.7693 11.7994 17.9932 11.16 17.9996 10.5V9C17.997 6.61384 17.0479 4.32616 15.3607 2.63889C13.6734 0.951621 11.3857 0.00258081 8.99956 0V0Z"
                fill="currentColor"
              />
            </svg>
            <a href="mailto:mail@mybillafrica.com">mail@mybillafrica.com</a>
          </li>
          <li>
            <svg width="18" height="18" viewBox="0 0 18 18" fill="none" xmlns="http://www.w3.org/2000/svg">
              <path
                d="M9 18C4.03725 18 0 13.9628 0 9C0 4.03725 4.03725 0 9 0C13.9628 0 18 4.03725 18 9C18 13.9628 13.9628 18 9 18ZM9 1.5C4.8645 1.5 1.5 4.8645 1.5 9C1.5 13.1355 4.8645 16.5 9 16.5C13.1355 16.5 16.5 13.1355 16.5 9C16.5 4.8645 13.1355 1.5 9 1.5ZM12.75 9C12.75 8.58525 12.4147 8.25 12 8.25H9.75V4.5C9.75 4.08525 9.414 3.75 9 3.75C8.586 3.75 8.25 4.08525 8.25 4.5V9C8.25 9.41475 8.586 9.75 9 9.75H12C12.4147 9.75 12.75 9.41475 12.75 9Z"
                fill="currentColor"
              />
            </svg>
            All days: 8AM - 5PM
          </li>
        </ul>
      </div>
    </div>
  );
};
