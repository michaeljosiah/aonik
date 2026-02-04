import { createBrowserRouter } from "react-router-dom";

import { App } from "./App";
import { AuthLayout } from "./layouts/AuthLayout";
import { DashboardLayout } from "./layouts/DashboardLayout";
import { FlowLayout } from "./layouts/FlowLayout";
import { MarketingLayout } from "./layouts/MarketingLayout";
import { Login } from "../pages/auth/Login";
import { Register } from "../pages/auth/Register";
import { Dashboard } from "../pages/dashboard/Dashboard";
import { DashboardEmpty } from "../pages/dashboard/DashboardEmpty";
import { Transactions } from "../pages/dashboard/Transactions";
import { TransactionsCalendar } from "../pages/dashboard/TransactionsCalendar";
import { ManageCards } from "../pages/dashboard/ManageCards";
import { CardDetails } from "../pages/dashboard/CardDetails";
import { Home } from "../pages/marketing/Home";
import { About } from "../pages/marketing/About";
import { Help } from "../pages/marketing/Help";
import { Community } from "../pages/marketing/Community";
import { GetApp } from "../pages/marketing/GetApp";
import { Features } from "../pages/marketing/Features";
import { FeaturesPage } from "../pages/marketing/FeaturesPage";
import { Privacy } from "../pages/marketing/Privacy";
import { Cookies } from "../pages/marketing/Cookies";
import { ProviderList } from "../pages/payments/ProviderList";
import { ServiceDetails } from "../pages/payments/ServiceDetails";
import { PaymentSelection } from "../pages/payments/PaymentSelection";
import { CardCheckout } from "../pages/payments/CardCheckout";
import { FriendCheckout } from "../pages/payments/FriendCheckout";
import { SelectCard } from "../pages/payments/SelectCard";
import { SelectFriend } from "../pages/payments/SelectFriend";
import { ConfirmationBillPaid } from "../pages/payments/ConfirmationBillPaid";
import { ConfirmationPaymentSent } from "../pages/payments/ConfirmationPaymentSent";
import { ConfirmationOrderReceived } from "../pages/payments/ConfirmationOrderReceived";
import { StatusBillPaid } from "../pages/payments/StatusBillPaid";
import { StatusBillPaidFailed } from "../pages/payments/StatusBillPaidFailed";
import { StatusPaymentSent } from "../pages/payments/StatusPaymentSent";
import { StatusOrderReceived } from "../pages/payments/StatusOrderReceived";
import { TransactionDetails } from "../pages/payments/TransactionDetails";
import { PersonalDetails } from "../pages/profile/PersonalDetails";
import { PersonalDetailsEditName } from "../pages/profile/PersonalDetailsEditName";
import { PersonalDetailsEditCountry } from "../pages/profile/PersonalDetailsEditCountry";
import { PersonalDetailsPhone } from "../pages/profile/PersonalDetailsPhone";
import { PersonalDetailsUpdatePhoto } from "../pages/profile/PersonalDetailsUpdatePhoto";
import { LoginDetails } from "../pages/profile/LoginDetails";
import { LoginDetailsEmail } from "../pages/profile/LoginDetailsEmail";
import { LoginDetailsPassword } from "../pages/profile/LoginDetailsPassword";
import { NotificationSettings } from "../pages/profile/NotificationSettings";
import { MarketingPreferences } from "../pages/profile/MarketingPreferences";

export const router = createBrowserRouter([
  {
    element: <App />,
    children: [
      {
        element: <MarketingLayout />,
        children: [
          { path: "/", element: <Home /> },
          { path: "/features", element: <Features /> },
          { path: "/features-page", element: <FeaturesPage /> },
          { path: "/about", element: <About /> },
          { path: "/help", element: <Help /> },
          { path: "/community", element: <Community /> },
          { path: "/get-app", element: <GetApp /> },
          { path: "/privacy", element: <Privacy /> },
          { path: "/cookies", element: <Cookies /> }
        ]
      },
      {
        element: <AuthLayout />,
        children: [
          { path: "/login", element: <Login /> },
          { path: "/register", element: <Register /> }
        ]
      },
      {
        element: <DashboardLayout />,
        children: [
          { path: "/dashboard", element: <Dashboard /> },
          { path: "/dashboard/empty", element: <DashboardEmpty /> },
          { path: "/transactions", element: <Transactions /> },
          { path: "/transactions/calendar", element: <TransactionsCalendar /> },
          { path: "/manage-cards", element: <ManageCards /> },
          { path: "/cards/details", element: <CardDetails /> },
          { path: "/profile/personal", element: <PersonalDetails /> },
          { path: "/profile/personal/edit-name", element: <PersonalDetailsEditName /> },
          { path: "/profile/personal/edit-country", element: <PersonalDetailsEditCountry /> },
          { path: "/profile/personal/phone", element: <PersonalDetailsPhone /> },
          { path: "/profile/personal/photo", element: <PersonalDetailsUpdatePhoto /> },
          { path: "/profile/login-details", element: <LoginDetails /> },
          { path: "/profile/login-details/email", element: <LoginDetailsEmail /> },
          { path: "/profile/login-details/password", element: <LoginDetailsPassword /> },
          { path: "/profile/notifications", element: <NotificationSettings /> },
          { path: "/profile/marketing", element: <MarketingPreferences /> }
        ]
      },
      {
        element: <FlowLayout currentStep={2} />,
        children: [
          { path: "/payments/providers", element: <ProviderList /> },
          { path: "/payments/service/:id", element: <ServiceDetails /> },
          { path: "/payments/selection", element: <PaymentSelection /> },
          { path: "/payments/card-checkout", element: <CardCheckout /> },
          { path: "/payments/friend-checkout", element: <FriendCheckout /> },
          { path: "/payments/select-card", element: <SelectCard /> },
          { path: "/payments/select-friend", element: <SelectFriend /> },
          { path: "/payments/confirm/bill-paid", element: <ConfirmationBillPaid /> },
          { path: "/payments/confirm/payment-sent", element: <ConfirmationPaymentSent /> },
          { path: "/payments/confirm/order-received", element: <ConfirmationOrderReceived /> },
          { path: "/payments/status/bill-paid", element: <StatusBillPaid /> },
          { path: "/payments/status/bill-paid-failed", element: <StatusBillPaidFailed /> },
          { path: "/payments/status/payment-sent", element: <StatusPaymentSent /> },
          { path: "/payments/status/order-received", element: <StatusOrderReceived /> },
          { path: "/payments/transaction-details", element: <TransactionDetails /> }
        ]
      }
    ]
  }
]);
