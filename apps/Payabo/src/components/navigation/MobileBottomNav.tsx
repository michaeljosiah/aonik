import { type ReactNode } from "react";
import { NavLink } from "react-router-dom";

type MobileNavIconProps = {
  viewBox: string;
  children: ReactNode;
};

type MobileNavItem = {
  label: string;
  to: string;
  icon: ReactNode;
};

const MobileNavIcon = ({ viewBox, children }: MobileNavIconProps) => (
  <svg width="22" height="22" viewBox={viewBox} fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    {children}
  </svg>
);

const navItems: MobileNavItem[] = [
  {
    label: "Spend",
    to: "/transactions",
    icon: (
      <MobileNavIcon viewBox="0 0 24 24">
        <path d="M5 12H19" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        <path d="M13 6L19 12L13 18" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
      </MobileNavIcon>
    )
  },
  {
    label: "Plan",
    to: "/dashboard",
    icon: (
      <MobileNavIcon viewBox="0 0 24 24">
        <path d="M12 4L20 20L12 16L4 20L12 4Z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
      </MobileNavIcon>
    )
  },
  {
    label: "Pay",
    to: "/payments/providers",
    icon: (
      <MobileNavIcon viewBox="0 0 24 24">
        <path d="M4 8.5C4 7.12 5.12 6 6.5 6H17.5C18.88 6 20 7.12 20 8.5V15.5C20 16.88 18.88 18 17.5 18H6.5C5.12 18 4 16.88 4 15.5V8.5Z" stroke="currentColor" strokeWidth="1.8" />
        <path d="M15 12H17" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
      </MobileNavIcon>
    )
  },
  {
    label: "Chat",
    to: "/chat",
    icon: (
      <MobileNavIcon viewBox="0 0 24 24">
        <path d="M12 4.4C7.58 4.4 4 7.53 4 11.4C4 13.28 4.86 14.99 6.25 16.25V20L9.43 18.08C10.24 18.29 11.1 18.4 12 18.4C16.42 18.4 20 15.27 20 11.4C20 7.53 16.42 4.4 12 4.4Z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
      </MobileNavIcon>
    )
  }
];

export const MobileBottomNav = () => {
  return (
    <nav className="payabo-chat-bottom-nav" aria-label="Primary">
      {navItems.map((item) => (
        <NavLink
          key={item.label}
          to={item.to}
          className={({ isActive }) => `payabo-chat-bottom-nav__item${isActive ? " active" : ""}`}
        >
          <span className="payabo-chat-bottom-nav__icon">{item.icon}</span>
          <span>{item.label}</span>
        </NavLink>
      ))}
    </nav>
  );
};
