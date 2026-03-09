import { type ReactNode, useEffect, useState } from "react";
import { NavLink, useLocation } from "react-router-dom";

type NavSection = "billPayments" | "personalFinance" | "wallet" | "payAssist" | null;

type SidebarIconProps = {
  viewBox: string;
  children: ReactNode;
};

const SidebarIcon = ({ viewBox, children }: SidebarIconProps) => (
  <svg width="24" height="24" viewBox={viewBox} fill="none" xmlns="http://www.w3.org/2000/svg">
    {children}
  </svg>
);

export const SidebarNav = () => {
  const location = useLocation();

  const getSectionForPath = (pathname: string): NavSection => {
    if (pathname.startsWith("/personal-finance/")) {
      return "personalFinance";
    }

    if (pathname.startsWith("/wallet/") || pathname.startsWith("/manage-cards") || pathname.startsWith("/cards/")) {
      return "wallet";
    }

    if (pathname.startsWith("/transactions") || pathname.startsWith("/payments/")) {
      return "billPayments";
    }

    return "billPayments";
  };

  const [openSection, setOpenSection] = useState<NavSection>(() => getSectionForPath(location.pathname));

  useEffect(() => {
    setOpenSection(getSectionForPath(location.pathname));
  }, [location.pathname]);

  const toggleSection = (section: NavSection) => {
    setOpenSection((current) => (current === section ? null : section));
  };

  return (
    <div className="main-sidebar">
      <div id="accordion-nav">
        <div className="panel list-nav">
          <NavLink to="/dashboard" className="list-nav-item d-flex align-items-center">
            <span className="list-nav-icon">
              <SidebarIcon viewBox="0 0 24 23">
                <path
                  d="M23.121 8.69115L15.536 1.42123C14.5973 0.524222 13.3257 0.0205078 12 0.0205078C10.6744 0.0205078 9.40277 0.524222 8.46401 1.42123L0.879012 8.69115C0.599438 8.95736 0.377782 9.2741 0.226895 9.62302C0.0760072 9.97194 -0.0011104 10.3461 1.20795e-05 10.7238V20.1317C1.20795e-05 20.8942 0.316083 21.6255 0.878692 22.1647C1.4413 22.7038 2.20436 23.0067 3.00001 23.0067H21C21.7957 23.0067 22.5587 22.7038 23.1213 22.1647C23.6839 21.6255 24 20.8942 24 20.1317V10.7238C24.0011 10.3461 23.924 9.97194 23.7731 9.62302C23.6222 9.2741 23.4006 8.95736 23.121 8.69115V8.69115ZM15 21.0901H9.00001V17.32C9.00001 16.5575 9.31608 15.8262 9.87869 15.2871C10.4413 14.7479 11.2044 14.445 12 14.445C12.7957 14.445 13.5587 14.7479 14.1213 15.2871C14.6839 15.8262 15 16.5575 15 17.32V21.0901ZM22 20.1317C22 20.3859 21.8947 20.6297 21.7071 20.8094C21.5196 20.9891 21.2652 21.0901 21 21.0901H17V17.32C17 16.0492 16.4732 14.8304 15.5355 13.9318C14.5979 13.0332 13.3261 12.5283 12 12.5283C10.6739 12.5283 9.40216 13.0332 8.46448 13.9318C7.5268 14.8304 7.00001 16.0492 7.00001 17.32V21.0901H3.00001C2.7348 21.0901 2.48044 20.9891 2.29291 20.8094C2.10537 20.6297 2.00001 20.3859 2.00001 20.1317V10.7238C2.00094 10.4698 2.1062 10.2264 2.29301 10.0462L9.87801 2.77919C10.4417 2.2415 11.2047 1.93964 12 1.93964C12.7953 1.93964 13.5583 2.2415 14.122 2.77919L21.707 10.0491C21.8931 10.2286 21.9983 10.4708 22 10.7238V20.1317Z"
                  fill="currentColor"
                />
              </SidebarIcon>
            </span>
            Home
          </NavLink>

          <a
            href="#nav-list-01"
            className={`list-nav-item icon-collapsed d-flex align-items-center ${openSection === "billPayments" ? "" : "collapsed"}`}
            onClick={(event) => {
              event.preventDefault();
              toggleSection("billPayments");
            }}
          >
            <span className="list-nav-icon">
              <SidebarIcon viewBox="0 0 34.501 36">
                <path
                  d="M23.5,34.5A1.5,1.5,0,0,1,22,36H8.5A7.509,7.509,0,0,1,1,28.5V7.5A7.509,7.509,0,0,1,8.5,0h6.772A10.437,10.437,0,0,1,22.7,3.075L27.923,8.3a10.556,10.556,0,0,1,1.122,1.325A1.5,1.5,0,1,1,26.6,11.372a7.467,7.467,0,0,0-.8-.945L20.576,5.2A7.517,7.517,0,0,0,19,3.99V10.5A1.5,1.5,0,0,0,20.5,12H25a1.5,1.5,0,1,1,0,3H20.5A4.505,4.505,0,0,1,16,10.5V3.035C15.76,3.012,15.517,3,15.272,3H8.5A4.505,4.505,0,0,0,4,7.5v21A4.505,4.505,0,0,0,8.5,33H22A1.5,1.5,0,0,1,23.5,34.5Zm8.527-10.1-4.561-.76A1.152,1.152,0,0,1,26.5,22.5,1.5,1.5,0,0,1,28,21h3.4a1.51,1.51,0,0,1,1.3.75,1.5,1.5,0,1,0,2.6-1.5A4.513,4.513,0,0,0,31.4,18H31V16.5a1.5,1.5,0,1,0-3,0V18a4.505,4.505,0,0,0-4.5,4.5,4.143,4.143,0,0,0,3.473,4.1l4.561.76A1.152,1.152,0,0,1,32.5,28.5,1.5,1.5,0,0,1,31,30H27.6a1.51,1.51,0,0,1-1.3-.75,1.5,1.5,0,1,0-2.595,1.5A4.512,4.512,0,0,0,27.6,33H28v1.5a1.5,1.5,0,1,0,3,0V33a4.505,4.505,0,0,0,4.5-4.5,4.143,4.143,0,0,0-3.473-4.1ZM10.75,22.5H19a1.5,1.5,0,0,0,0-3H10.75A3.755,3.755,0,0,0,7,23.25v3A3.755,3.755,0,0,0,10.75,30H19a1.5,1.5,0,0,0,0-3H10.75a.75.75,0,0,1-.75-.75v-3a.75.75,0,0,1,.75-.75Zm-2.25-6h3a1.5,1.5,0,0,0,0-3h-3a1.5,1.5,0,1,0,0,3Zm0-6h3a1.5,1.5,0,0,0,0-3h-3a1.5,1.5,0,1,0,0,3Z"
                  fill="currentColor"
                />
              </SidebarIcon>
            </span>
            Bill payments
          </a>
          <div className={`collapse ${openSection === "billPayments" ? "show" : ""}`} id="nav-list-01">
            <ul className="list-sub-nav">
              <li>
                <NavLink to="/payments/providers">Pay a bill</NavLink>
              </li>
              <li>
                <NavLink to="/transactions">Transactions</NavLink>
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
            className={`list-nav-item icon-collapsed d-flex align-items-center ${openSection === "personalFinance" ? "" : "collapsed"}`}
            onClick={(event) => {
              event.preventDefault();
              toggleSection("personalFinance");
            }}
          >
            <span className="list-nav-icon">
              <SidebarIcon viewBox="0 0 48 48">
                <path
                  d="M33,0C24.588,0,18,3.954,18,9v5.174A25.631,25.631,0,0,0,15,14C6.588,14,0,17.954,0,23V39c0,5.046,6.588,9,15,9,6.814,0,12.432-2.594,14.32-6.262A25.587,25.587,0,0,0,33,42c8.412,0,15-3.954,15-9V9C48,3.954,41.412,0,33,0ZM44,25c0,2.36-4.7,5-11,5a21.67,21.67,0,0,1-3-.206V25.826A25.993,25.993,0,0,0,33,26a20.69,20.69,0,0,0,11-2.822ZM4,29.178A20.69,20.69,0,0,0,15,32a20.69,20.69,0,0,0,11-2.822V31c0,2.36-4.7,5-11,5S4,33.36,4,31ZM44,17c0,2.36-4.7,5-11,5a21.682,21.682,0,0,1-3.132-.224,7.922,7.922,0,0,0-3.412-4.646A24.025,24.025,0,0,0,33,18a20.69,20.69,0,0,0,11-2.822ZM33,4c6.3,0,11,2.64,11,5s-4.7,5-11,5S22,11.36,22,9,26.7,4,33,4ZM15,18c6.3,0,11,2.64,11,5s-4.7,5-11,5S4,25.36,4,23,8.7,18,15,18Zm0,26C8.7,44,4,41.36,4,39V37.178A20.69,20.69,0,0,0,15,40a20.69,20.69,0,0,0,11-2.822V39C26,41.36,21.3,44,15,44Zm18-6a21.669,21.669,0,0,1-3-.206V33.826A25.994,25.994,0,0,0,33,34a20.69,20.69,0,0,0,11-2.822V33C44,35.36,39.3,38,33,38Z"
                  fill="currentColor"
                />
              </SidebarIcon>
            </span>
            Personal finance
          </a>
          <div className={`collapse ${openSection === "personalFinance" ? "show" : ""}`} id="nav-list-02">
            <ul className="list-sub-nav">
              <li>
                <NavLink to="/personal-finance/transactions">All transactions</NavLink>
              </li>
              <li>
                <NavLink to="/personal-finance/transactions/manual/new">Add transaction</NavLink>
              </li>
              <li>
                <NavLink to="/personal-finance/transactions/import">Import statement</NavLink>
              </li>
              <li>
                <NavLink to="/personal-finance/transactions/review">Review queue</NavLink>
              </li>
              <li>
                <NavLink to="/personal-finance/insights/spending">Spending insights</NavLink>
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
              <SidebarIcon viewBox="0 0 36 30">
                <path
                  d="M31.5,8H7.5A4.518,4.518,0,0,1,4.148,6.5,4.493,4.493,0,0,1,7.5,5h27a1.5,1.5,0,1,0,0-3H7.5A7.5,7.5,0,0,0,0,9.5v15A7.5,7.5,0,0,0,7.5,32h24A4.5,4.5,0,0,0,36,27.5v-15A4.5,4.5,0,0,0,31.5,8ZM33,27.5A1.5,1.5,0,0,1,31.5,29H7.5A4.505,4.505,0,0,1,3,24.5V9.5A7.518,7.518,0,0,0,7.5,11h24A1.5,1.5,0,0,1,33,12.5ZM30,20a1.5,1.5,0,1,1-1.5-1.5A1.5,1.5,0,0,1,30,20Z"
                  transform="translate(0 -2)"
                  fill="currentColor"
                />
              </SidebarIcon>
            </span>
            Wallet
          </a>
          <div className={`collapse ${openSection === "wallet" ? "show" : ""}`} id="nav-list-03">
            <ul className="list-sub-nav">
              <li>
                <NavLink to="/wallet/accounts">Accounts</NavLink>
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
              <SidebarIcon viewBox="0 0 48.004 48">
                <path
                  d="M15,24A12,12,0,1,0,3,12,12,12,0,0,0,15,24ZM15,4a8,8,0,1,1-8,8,8,8,0,0,1,8-8Z"
                  transform="translate(3 0)"
                  fill="currentColor"
                />
                <path
                  d="M18,14A18.022,18.022,0,0,0,0,32a2,2,0,0,0,4,0,14,14,0,1,1,28,0,2,2,0,0,0,4,0A18.022,18.022,0,0,0,18,14Z"
                  transform="translate(0 14)"
                  fill="currentColor"
                />
                <path
                  d="M28,7.875a4.214,4.214,0,0,0-4,4.4,4.214,4.214,0,0,0-4-4.4,4.214,4.214,0,0,0-4,4.4c0,3.46,4.512,7.514,6.76,9.318a1.984,1.984,0,0,0,2.48,0c2.248-1.8,6.76-5.858,6.76-9.318a4.214,4.214,0,0,0-4-4.4Z"
                  transform="translate(15.998 7.875)"
                  fill="currentColor"
                />
              </SidebarIcon>
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
              <SidebarIcon viewBox="0 0 48 48">
                <path
                  d="M24,32a8,8,0,1,1,8-8A8,8,0,0,1,24,32Zm0-12a4,4,0,1,0,4,4A4,4,0,0,0,24,20ZM36,46a12,12,0,0,0-24,0,2,2,0,0,0,4,0,8,8,0,0,1,16,0,2,2,0,0,0,4,0Zm0-30a8,8,0,1,1,8-8A8,8,0,0,1,36,16ZM36,4a4,4,0,1,0,4,4A4,4,0,0,0,36,4ZM48,30A12.012,12.012,0,0,0,36,18a2,2,0,0,0,0,4,8,8,0,0,1,8,8,2,2,0,0,0,4,0ZM12,16a8,8,0,1,1,8-8A8,8,0,0,1,12,16ZM12,4a4,4,0,1,0,4,4A4,4,0,0,0,12,4ZM4,30a8,8,0,0,1,8-8,2,2,0,0,0,0-4A12.012,12.012,0,0,0,0,30a2,2,0,0,0,4,0Z"
                  fill="currentColor"
                />
              </SidebarIcon>
            </span>
            Organisations
          </a>

          <NavLink to="/chat" className="list-nav-item d-flex align-items-center">
            <span className="list-nav-icon">
              <SidebarIcon viewBox="0 0 24 24">
                <path
                  d="M9 12C10.1867 12 11.3467 11.6481 12.3334 10.9888C13.3201 10.3295 14.0892 9.39246 14.5433 8.2961C14.9974 7.19975 15.1162 5.99335 14.8847 4.82946C14.6532 3.66558 14.0818 2.59648 13.2426 1.75736C12.4035 0.918247 11.3344 0.346802 10.1705 0.115291C9.00666 -0.11622 7.80026 0.00259972 6.7039 0.456726C5.60754 0.910851 4.67047 1.67989 4.01118 2.66658C3.35189 3.65328 3 4.81331 3 6C3.00159 7.59081 3.63424 9.11602 4.75911 10.2409C5.88399 11.3658 7.40919 11.9984 9 12ZM9 2C9.79113 2 10.5645 2.2346 11.2223 2.67412C11.8801 3.11365 12.3928 3.73836 12.6955 4.46927C12.9983 5.20017 13.0775 6.00444 12.9231 6.78036C12.7688 7.55629 12.3878 8.26902 11.8284 8.82843C11.269 9.38784 10.5563 9.7688 9.78036 9.92314C9.00444 10.0775 8.20017 9.99827 7.46927 9.69552C6.73836 9.39277 6.11365 8.88008 5.67412 8.22228C5.2346 7.56449 5 6.79113 5 6C5 4.93914 5.42143 3.92172 6.17157 3.17158C6.92172 2.42143 7.93913 2 9 2V2Z"
                  fill="currentColor"
                />
                <path
                  d="M9 14C6.61395 14.0029 4.32645 14.9521 2.63925 16.6393C0.952057 18.3265 0.00291096 20.6139 0 23C0 23.2652 0.105357 23.5196 0.292893 23.7071C0.48043 23.8946 0.734784 24 1 24C1.26522 24 1.51957 23.8946 1.70711 23.7071C1.89464 23.5196 2 23.2652 2 23C2 21.1435 2.7375 19.363 4.05025 18.0503C5.36301 16.7375 7.14348 16 9 16C10.8565 16 12.637 16.7375 13.9497 18.0503C15.2625 19.363 16 21.1435 16 23C16 23.2652 16.1054 23.5196 16.2929 23.7071C16.4804 23.8946 16.7348 24 17 24C17.2652 24 17.5196 23.8946 17.7071 23.7071C17.8946 23.5196 18 23.2652 18 23C17.9971 20.6139 17.0479 18.3265 15.3607 16.6393C13.6735 14.9521 11.3861 14.0029 9 14V14Z"
                  fill="currentColor"
                />
                <path
                  d="M22 7.875C21.4435 7.90272 20.9206 8.14977 20.5458 8.56207C20.1709 8.97437 19.9747 9.51836 20 10.075C20.0253 9.51836 19.829 8.97437 19.4542 8.56207C19.0794 8.14977 18.5565 7.90272 18 7.875C17.4435 7.90272 16.9206 8.14977 16.5458 8.56207C16.1709 8.97437 15.9747 9.51836 16 10.075C16 11.805 18.256 13.832 19.38 14.734C19.5559 14.8749 19.7746 14.9516 20 14.9516C20.2254 14.9516 20.444 14.8749 20.62 14.734C21.744 13.834 24 11.805 24 10.075C24.0253 9.51836 23.829 8.97437 23.4542 8.56207C23.0794 8.14977 22.5565 7.90272 22 7.875V7.875Z"
                  fill="currentColor"
                />
              </SidebarIcon>
            </span>
            Chat
          </NavLink>
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
