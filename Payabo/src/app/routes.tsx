import { createBrowserRouter, Navigate } from "react-router-dom";

import { App } from "./App";
import { AuthLayout } from "./layouts/AuthLayout";
import { DashboardLayout } from "./layouts/DashboardLayout";
import { FlowLayout } from "./layouts/FlowLayout";
import { MarketingLayout } from "./layouts/MarketingLayout";
import { Home } from "../pages/marketing/Home";
import { About } from "../pages/marketing/About";
import { Community } from "../pages/marketing/Community";
import { CommunityDetails } from "../pages/marketing/CommunityDetails";
import { Cookies } from "../pages/marketing/Cookies";
import { Features } from "../pages/marketing/Features";
import { FeaturesPage } from "../pages/marketing/FeaturesPage";
import { GetApp } from "../pages/marketing/GetApp";
import { Help } from "../pages/marketing/Help";
import { Privacy } from "../pages/marketing/Privacy";
import { ProviderList } from "../pages/payments/ProviderList";
import { ServiceDetails } from "../pages/payments/ServiceDetails";
import { PaymentSelection } from "../pages/payments/PaymentSelection";
import { CardCheckout } from "../pages/payments/CardCheckout";
import { FriendCheckout } from "../pages/payments/FriendCheckout";
import { FriendDetails } from "../pages/payments/FriendDetails";
import { FriendMessage } from "../pages/payments/FriendMessage";
import { SelectCard } from "../pages/payments/SelectCard";
import { SelectFriend } from "../pages/payments/SelectFriend";
import { PaymentReturn } from "../pages/payments/PaymentReturn";
import { ConfirmationBillPaid } from "../pages/payments/ConfirmationBillPaid";
import { ConfirmationOrderReceived } from "../pages/payments/ConfirmationOrderReceived";
import { ConfirmationPaymentSent } from "../pages/payments/ConfirmationPaymentSent";
import { StatusBillPaid } from "../pages/payments/StatusBillPaid";
import { StatusBillPaidFailed } from "../pages/payments/StatusBillPaidFailed";
import { StatusOrderReceived } from "../pages/payments/StatusOrderReceived";
import { StatusPaymentSent } from "../pages/payments/StatusPaymentSent";
import { Login } from "../pages/auth/Login";
import { Register } from "../pages/auth/Register";
import { RequireAuth } from "./auth/RequireAuth";
import { Logout } from "../pages/auth/Logout";
import { Dashboard } from "../pages/dashboard/Dashboard";
import { DashboardEmpty } from "../pages/dashboard/DashboardEmpty";
import { Transactions } from "../pages/dashboard/Transactions";
import { TransactionsCalendar } from "../pages/dashboard/TransactionsCalendar";
import { ManageCards } from "../pages/dashboard/ManageCards";
import { CardDetails } from "../pages/dashboard/CardDetails";
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
          { path: "/community-details", element: <CommunityDetails /> },
          { path: "/get-app", element: <GetApp /> },
          { path: "/privacy", element: <Privacy /> },
          { path: "/cookies", element: <Cookies /> }
        ]
      },
      {
        element: <AuthLayout />,
        children: [
          { path: "/login", element: <Login /> },
          { path: "/register", element: <Register /> },
          { path: "/logout", element: <Logout /> }
        ]
      },
      {
        element: (
          <RequireAuth>
            <DashboardLayout />
          </RequireAuth>
        ),
        children: [
          { path: "/dashboard", element: <Dashboard /> },
          { path: "/dashboard/empty", element: <DashboardEmpty /> },
          { path: "/dashboard/raw", element: <Navigate to="/dashboard" replace /> },
          { path: "/dashboard-sample", element: <Navigate to="/dashboard" replace /> },
          { path: "/dashboard-raw", element: <Navigate to="/dashboard" replace /> },
          { path: "/dashboard-empty", element: <DashboardEmpty /> },
          { path: "/transactions", element: <Transactions /> },
          { path: "/transactions/calendar", element: <TransactionsCalendar /> },
          { path: "/dashboard-transactions-raw", element: <Navigate to="/transactions" replace /> },
          { path: "/dashboard-transactions-rsample", element: <Navigate to="/transactions" replace /> },
          { path: "/dashboard-transactions-calendar", element: <TransactionsCalendar /> },
          { path: "/manage-cards", element: <ManageCards /> },
          { path: "/managecards-raw", element: <ManageCards /> },
          { path: "/managecards-sample", element: <ManageCards /> },
          { path: "/cards/details", element: <CardDetails /> },
          { path: "/carddetails", element: <CardDetails /> },
          { path: "/profile/personal", element: <PersonalDetails /> },
          {
            path: "/profile/personal/edit-name",
            element: <PersonalDetailsEditName />
          },
          {
            path: "/profile/personal/edit-country",
            element: <PersonalDetailsEditCountry />
          },
          {
            path: "/profile/personal/phone",
            element: <PersonalDetailsPhone />
          },
          {
            path: "/profile/personal/photo",
            element: <PersonalDetailsUpdatePhoto />
          },
          { path: "/profile/login-details", element: <LoginDetails /> },
          {
            path: "/profile/login-details/email",
            element: <LoginDetailsEmail />
          },
          {
            path: "/profile/login-details/password",
            element: <LoginDetailsPassword />
          },
          { path: "/profile/notifications", element: <NotificationSettings /> },
          { path: "/profile/marketing", element: <MarketingPreferences /> },
          { path: "/profile-notification-email", element: <NotificationSettings /> },
          { path: "/profile-marketing-email", element: <MarketingPreferences /> },
          { path: "/profile-logindetails", element: <LoginDetails /> },
          { path: "/profile-logindetails-email", element: <LoginDetailsEmail /> },
          { path: "/profile-logindetails-password", element: <LoginDetailsPassword /> },
          { path: "/profile-personaldetails", element: <PersonalDetails /> },
          { path: "/profile-personaldetails-editname", element: <PersonalDetailsEditName /> },
          { path: "/profile-personaldetails-editcountry", element: <PersonalDetailsEditCountry /> },
          { path: "/profile-personaldetails-phone", element: <PersonalDetailsPhone /> },
          { path: "/profile-personaldetails-updatephoto", element: <PersonalDetailsUpdatePhoto /> },
          { path: "/profile-notification", element: <NotificationSettings /> },
          { path: "/profile-marketing", element: <MarketingPreferences /> }
        ]
      },
      {
        element: <FlowLayout currentStep={0} headerClassName="border-bottom-0" />,
        children: [
          { path: "/payments/providers", element: <ProviderList /> },
          { path: "/serviceproviderlist-raw", element: <Navigate to="/payments/providers" replace /> },
          {
            path: "/serviceproviderlist-sample",
            element: <Navigate to="/payments/providers" replace />
          }
        ]
      },
      {
        element: <FlowLayout currentStep={1} />,
        children: [
          { path: "/payments/service/:id", element: <ServiceDetails /> },
          { path: "/servicedetails-raw", element: <Navigate to="/payments/providers" replace /> },
          { path: "/servicedetails-sample", element: <Navigate to="/payments/providers" replace /> },
          {
            path: "/servicedetails-recurringbill",
            element: <Navigate to="/payments/providers" replace />
          }
        ]
      },
      {
        element: (
          <RequireAuth>
            <FlowLayout currentStep={2} showUserPanel />
          </RequireAuth>
        ),
        children: [
          { path: "/payments/selection", element: <PaymentSelection /> },
          { path: "/paymentselection", element: <PaymentSelection /> }
        ]
      },
      {
        element: (
          <RequireAuth>
            <FlowLayout currentStep={3} showUserPanel />
          </RequireAuth>
        ),
        children: [
          {
            path: "/payments/card-checkout",
            element: <CardCheckout />
          },
          {
            path: "/payments/return",
            element: <PaymentReturn />
          },
          {
            path: "/payments/friend-checkout",
            element: <FriendCheckout />
          },
          { path: "/payments/select-card", element: <SelectCard /> },
          { path: "/payments/select-friend", element: <SelectFriend /> },
          { path: "/payments/friend-details", element: <FriendDetails /> },
          { path: "/payments/friend-message", element: <FriendMessage /> },
          { path: "/cardcheckout-row", element: <Navigate to="/payments/card-checkout" replace /> },
          { path: "/cardcheckout-sample", element: <Navigate to="/payments/card-checkout" replace /> },
          { path: "/friendcheckout-row", element: <Navigate to="/payments/friend-checkout" replace /> },
          { path: "/friendcheckout-sample", element: <Navigate to="/payments/friend-checkout" replace /> },
          {
            path: "/friendcheckout-sample-nomessage",
            element: <Navigate to="/payments/friend-checkout" replace />
          },
          { path: "/selectcard", element: <SelectCard /> },
          { path: "/selectfriend", element: <SelectFriend /> },
          { path: "/selectfriend-row", element: <Navigate to="/payments/select-friend" replace /> },
          { path: "/selectfriend-sample", element: <Navigate to="/payments/select-friend" replace /> },
          { path: "/friend-message", element: <FriendMessage /> },
          { path: "/frienddetails", element: <FriendDetails /> }
        ]
      },
      {
        element: (
          <RequireAuth>
            <FlowLayout currentStep={4} />
          </RequireAuth>
        ),
        children: [
          {
            path: "/payments/confirm/bill-paid",
            element: <ConfirmationBillPaid />
          },
          {
            path: "/payments/confirm/payment-sent",
            element: <ConfirmationPaymentSent />
          },
          {
            path: "/payments/confirm/order-received",
            element: <ConfirmationOrderReceived />
          },
          { path: "/confirmation-billpaid", element: <ConfirmationBillPaid /> },
          {
            path: "/confirmation-paymentsent",
            element: <ConfirmationPaymentSent />
          },
          {
            path: "/confirmation-orderreceived",
            element: <ConfirmationOrderReceived />
          },
          { path: "/payments/status/bill-paid", element: <StatusBillPaid /> },
          {
            path: "/payments/status/bill-paid-failed",
            element: <StatusBillPaidFailed />
          },
          { path: "/payments/status/payment-sent", element: <StatusPaymentSent /> },
          {
            path: "/payments/status/order-received",
            element: <StatusOrderReceived />
          },
          { path: "/status-billpaid", element: <StatusBillPaid /> },
          { path: "/status-billpaid-failled", element: <StatusBillPaidFailed /> },
          { path: "/status-paymentsent", element: <StatusPaymentSent /> },
          { path: "/status-order-received", element: <StatusOrderReceived /> },
          {
            path: "/payments/transaction-details",
            element: <TransactionDetails />
          },
          { path: "/transactiondetails-raw", element: <Navigate to="/payments/transaction-details" replace /> }
        ]
      }
    ]
  }
]);
