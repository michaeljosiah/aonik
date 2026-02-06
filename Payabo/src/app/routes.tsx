import { createBrowserRouter } from "react-router-dom";

import { App } from "./App";
import { AuthLayout } from "./layouts/AuthLayout";
import { DashboardLayout } from "./layouts/DashboardLayout";
import { FlowLayout } from "./layouts/FlowLayout";
import { MarketingLayout } from "./layouts/MarketingLayout";
import { StaticHtmlPage } from "../components/common/StaticHtmlPage";
import { Home } from "../pages/marketing/Home";
import { About } from "../pages/marketing/About";
import { Community } from "../pages/marketing/Community";
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
import { SelectCard } from "../pages/payments/SelectCard";
import { Login } from "../pages/auth/Login";
import { Register } from "../pages/auth/Register";
import { RequireAuth } from "./auth/RequireAuth";
import { Logout } from "../pages/auth/Logout";

import cardCheckoutRowHtml from "../../../website/MyBillAfrica/cardcheckout-row.html?raw";
import cardCheckoutSampleHtml from "../../../website/MyBillAfrica/cardcheckout-sample.html?raw";
import cardDetailsHtml from "../../../website/MyBillAfrica/carddetails.html?raw";
import confirmationBillPaidHtml from "../../../website/MyBillAfrica/confirmation-billpaid.html?raw";
import confirmationOrderReceivedHtml from "../../../website/MyBillAfrica/confirmation-orderreceived.html?raw";
import confirmationPaymentSentHtml from "../../../website/MyBillAfrica/confirmation-paymentsent.html?raw";
import dashboardEmptyHtml from "../../../website/MyBillAfrica/dashboard-empty.html?raw";
import dashboardRawHtml from "../../../website/MyBillAfrica/dashboard-raw.html?raw";
import dashboardSampleHtml from "../../../website/MyBillAfrica/dashboard-sample.html?raw";
import dashboardTransactionsCalendarHtml from "../../../website/MyBillAfrica/dashboard-transactions-calendar.html?raw";
import dashboardTransactionsRawHtml from "../../../website/MyBillAfrica/dashboard-transactions-raw.html?raw";
import dashboardTransactionsSampleHtml from "../../../website/MyBillAfrica/dashboard-transactions-rsample.html?raw";
import friendCheckoutRowHtml from "../../../website/MyBillAfrica/friendcheckout-row.html?raw";
import friendCheckoutSampleHtml from "../../../website/MyBillAfrica/friendcheckout-sample.html?raw";
import friendCheckoutSampleNoMessageHtml from "../../../website/MyBillAfrica/friendcheckout-sample-nomessage.html?raw";
import friendDetailsHtml from "../../../website/MyBillAfrica/frienddetails.html?raw";
import friendMessageHtml from "../../../website/MyBillAfrica/friend-message.html?raw";
import manageCardsRawHtml from "../../../website/MyBillAfrica/managecards-raw.html?raw";
import manageCardsSampleHtml from "../../../website/MyBillAfrica/managecards-sample.html?raw";
import profileLoginDetailsEmailHtml from "../../../website/MyBillAfrica/profile-logindetails-email.html?raw";
import profileLoginDetailsHtml from "../../../website/MyBillAfrica/profile-logindetails.html?raw";
import profileLoginDetailsPasswordHtml from "../../../website/MyBillAfrica/profile-logindetails-password.html?raw";
import profileMarketingEmailHtml from "../../../website/MyBillAfrica/profile-marketing-email.html?raw";
import profileMarketingHtml from "../../../website/MyBillAfrica/profile-marketing.html?raw";
import profileNotificationEmailHtml from "../../../website/MyBillAfrica/profile-notification-email.html?raw";
import profileNotificationHtml from "../../../website/MyBillAfrica/profile-notification.html?raw";
import profilePersonalDetailsEditCountryHtml from "../../../website/MyBillAfrica/profile-personaldetails-editcountry.html?raw";
import profilePersonalDetailsEditNameHtml from "../../../website/MyBillAfrica/profile-personaldetails-editname.html?raw";
import profilePersonalDetailsHtml from "../../../website/MyBillAfrica/profile-personaldetails.html?raw";
import profilePersonalDetailsPhoneHtml from "../../../website/MyBillAfrica/profile-personaldetails-phone.html?raw";
import profilePersonalDetailsUpdatePhotoHtml from "../../../website/MyBillAfrica/profile-personaldetails-updatephoto.html?raw";
import selectFriendHtml from "../../../website/MyBillAfrica/selectfriend.html?raw";
import selectFriendRowHtml from "../../../website/MyBillAfrica/selectfriend-row.html?raw";
import selectFriendSampleHtml from "../../../website/MyBillAfrica/selectfriend-sample.html?raw";
import serviceDetailsRawHtml from "../../../website/MyBillAfrica/servicedetails-raw.html?raw";
import serviceDetailsRecurringHtml from "../../../website/MyBillAfrica/servicedetails-recurringbill.html?raw";
import serviceDetailsSampleHtml from "../../../website/MyBillAfrica/servicedetails-sample.html?raw";
import serviceProviderListRawHtml from "../../../website/MyBillAfrica/serviceproviderlist-raw.html?raw";
import serviceProviderListSampleHtml from "../../../website/MyBillAfrica/serviceproviderlist-sample.html?raw";
import statusBillPaidFailedHtml from "../../../website/MyBillAfrica/status-billpaid-failled.html?raw";
import statusBillPaidHtml from "../../../website/MyBillAfrica/status-billpaid.html?raw";
import statusOrderReceivedHtml from "../../../website/MyBillAfrica/status-order-received.html?raw";
import statusPaymentSentHtml from "../../../website/MyBillAfrica/status-paymentsent.html?raw";
import transactionDetailsHtml from "../../../website/MyBillAfrica/transactiondetails-raw.html?raw";

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
          { path: "/dashboard", element: <StaticHtmlPage html={dashboardSampleHtml} selector="main" /> },
          { path: "/dashboard/empty", element: <StaticHtmlPage html={dashboardEmptyHtml} selector="main" /> },
          { path: "/dashboard/raw", element: <StaticHtmlPage html={dashboardRawHtml} selector="main" /> },
          { path: "/dashboard-sample", element: <StaticHtmlPage html={dashboardSampleHtml} selector="main" /> },
          { path: "/dashboard-raw", element: <StaticHtmlPage html={dashboardRawHtml} selector="main" /> },
          { path: "/dashboard-empty", element: <StaticHtmlPage html={dashboardEmptyHtml} selector="main" /> },
          { path: "/transactions", element: <StaticHtmlPage html={dashboardTransactionsRawHtml} selector="main" /> },
          {
            path: "/transactions/calendar",
            element: <StaticHtmlPage html={dashboardTransactionsCalendarHtml} selector="main" />
          },
          {
            path: "/dashboard-transactions-raw",
            element: <StaticHtmlPage html={dashboardTransactionsRawHtml} selector="main" />
          },
          {
            path: "/dashboard-transactions-rsample",
            element: <StaticHtmlPage html={dashboardTransactionsSampleHtml} selector="main" />
          },
          {
            path: "/dashboard-transactions-calendar",
            element: <StaticHtmlPage html={dashboardTransactionsCalendarHtml} selector="main" />
          },
          { path: "/manage-cards", element: <StaticHtmlPage html={manageCardsRawHtml} selector="main" /> },
          { path: "/managecards-raw", element: <StaticHtmlPage html={manageCardsRawHtml} selector="main" /> },
          { path: "/managecards-sample", element: <StaticHtmlPage html={manageCardsSampleHtml} selector="main" /> },
          { path: "/cards/details", element: <StaticHtmlPage html={cardDetailsHtml} selector="main" /> },
          { path: "/carddetails", element: <StaticHtmlPage html={cardDetailsHtml} selector="main" /> },
          { path: "/profile/personal", element: <StaticHtmlPage html={profilePersonalDetailsHtml} selector="main" /> },
          {
            path: "/profile/personal/edit-name",
            element: <StaticHtmlPage html={profilePersonalDetailsEditNameHtml} selector="main" />
          },
          {
            path: "/profile/personal/edit-country",
            element: <StaticHtmlPage html={profilePersonalDetailsEditCountryHtml} selector="main" />
          },
          {
            path: "/profile/personal/phone",
            element: <StaticHtmlPage html={profilePersonalDetailsPhoneHtml} selector="main" />
          },
          {
            path: "/profile/personal/photo",
            element: <StaticHtmlPage html={profilePersonalDetailsUpdatePhotoHtml} selector="main" />
          },
          { path: "/profile/login-details", element: <StaticHtmlPage html={profileLoginDetailsHtml} selector="main" /> },
          {
            path: "/profile/login-details/email",
            element: <StaticHtmlPage html={profileLoginDetailsEmailHtml} selector="main" />
          },
          {
            path: "/profile/login-details/password",
            element: <StaticHtmlPage html={profileLoginDetailsPasswordHtml} selector="main" />
          },
          { path: "/profile/notifications", element: <StaticHtmlPage html={profileNotificationHtml} selector="main" /> },
          { path: "/profile/marketing", element: <StaticHtmlPage html={profileMarketingHtml} selector="main" /> },
          {
            path: "/profile-notification-email",
            element: <StaticHtmlPage html={profileNotificationEmailHtml} selector="main" />
          },
          { path: "/profile-marketing-email", element: <StaticHtmlPage html={profileMarketingEmailHtml} selector="main" /> },
          { path: "/profile-logindetails", element: <StaticHtmlPage html={profileLoginDetailsHtml} selector="main" /> },
          {
            path: "/profile-logindetails-email",
            element: <StaticHtmlPage html={profileLoginDetailsEmailHtml} selector="main" />
          },
          {
            path: "/profile-logindetails-password",
            element: <StaticHtmlPage html={profileLoginDetailsPasswordHtml} selector="main" />
          },
          {
            path: "/profile-personaldetails",
            element: <StaticHtmlPage html={profilePersonalDetailsHtml} selector="main" />
          },
          {
            path: "/profile-personaldetails-editname",
            element: <StaticHtmlPage html={profilePersonalDetailsEditNameHtml} selector="main" />
          },
          {
            path: "/profile-personaldetails-editcountry",
            element: <StaticHtmlPage html={profilePersonalDetailsEditCountryHtml} selector="main" />
          },
          {
            path: "/profile-personaldetails-phone",
            element: <StaticHtmlPage html={profilePersonalDetailsPhoneHtml} selector="main" />
          },
          {
            path: "/profile-personaldetails-updatephoto",
            element: <StaticHtmlPage html={profilePersonalDetailsUpdatePhotoHtml} selector="main" />
          },
          { path: "/profile-notification", element: <StaticHtmlPage html={profileNotificationHtml} selector="main" /> },
          { path: "/profile-marketing", element: <StaticHtmlPage html={profileMarketingHtml} selector="main" /> }
        ]
      },
      {
        element: <FlowLayout currentStep={0} headerClassName="border-bottom-0" />,
        children: [
          { path: "/payments/providers", element: <ProviderList /> },
          { path: "/serviceproviderlist-raw", element: <StaticHtmlPage html={serviceProviderListRawHtml} selector="main" /> },
          {
            path: "/serviceproviderlist-sample",
            element: <StaticHtmlPage html={serviceProviderListSampleHtml} selector="main" />
          }
        ]
      },
      {
        element: <FlowLayout currentStep={1} />,
        children: [
          { path: "/payments/service/:id", element: <ServiceDetails /> },
          { path: "/servicedetails-raw", element: <StaticHtmlPage html={serviceDetailsRawHtml} selector="main" /> },
          { path: "/servicedetails-sample", element: <StaticHtmlPage html={serviceDetailsSampleHtml} selector="main" /> },
          {
            path: "/servicedetails-recurringbill",
            element: <StaticHtmlPage html={serviceDetailsRecurringHtml} selector="main" />
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
            path: "/payments/friend-checkout",
            element: <StaticHtmlPage html={friendCheckoutSampleHtml} selector="main" />
          },
          { path: "/payments/select-card", element: <SelectCard /> },
          { path: "/payments/select-friend", element: <StaticHtmlPage html={selectFriendHtml} selector="main" /> },
          { path: "/cardcheckout-row", element: <StaticHtmlPage html={cardCheckoutRowHtml} selector="main" /> },
          { path: "/cardcheckout-sample", element: <StaticHtmlPage html={cardCheckoutSampleHtml} selector="main" /> },
          { path: "/friendcheckout-row", element: <StaticHtmlPage html={friendCheckoutRowHtml} selector="main" /> },
          { path: "/friendcheckout-sample", element: <StaticHtmlPage html={friendCheckoutSampleHtml} selector="main" /> },
          {
            path: "/friendcheckout-sample-nomessage",
            element: <StaticHtmlPage html={friendCheckoutSampleNoMessageHtml} selector="main" />
          },
          { path: "/selectcard", element: <SelectCard /> },
          { path: "/selectfriend", element: <StaticHtmlPage html={selectFriendHtml} selector="main" /> },
          { path: "/selectfriend-row", element: <StaticHtmlPage html={selectFriendRowHtml} selector="main" /> },
          { path: "/selectfriend-sample", element: <StaticHtmlPage html={selectFriendSampleHtml} selector="main" /> },
          { path: "/friend-message", element: <StaticHtmlPage html={friendMessageHtml} selector="main" /> },
          { path: "/frienddetails", element: <StaticHtmlPage html={friendDetailsHtml} selector="main" /> }
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
            element: <StaticHtmlPage html={confirmationBillPaidHtml} selector="main" />
          },
          {
            path: "/payments/confirm/payment-sent",
            element: <StaticHtmlPage html={confirmationPaymentSentHtml} selector="main" />
          },
          {
            path: "/payments/confirm/order-received",
            element: <StaticHtmlPage html={confirmationOrderReceivedHtml} selector="main" />
          },
          { path: "/confirmation-billpaid", element: <StaticHtmlPage html={confirmationBillPaidHtml} selector="main" /> },
          {
            path: "/confirmation-paymentsent",
            element: <StaticHtmlPage html={confirmationPaymentSentHtml} selector="main" />
          },
          {
            path: "/confirmation-orderreceived",
            element: <StaticHtmlPage html={confirmationOrderReceivedHtml} selector="main" />
          },
          { path: "/payments/status/bill-paid", element: <StaticHtmlPage html={statusBillPaidHtml} selector="main" /> },
          {
            path: "/payments/status/bill-paid-failed",
            element: <StaticHtmlPage html={statusBillPaidFailedHtml} selector="main" />
          },
          { path: "/payments/status/payment-sent", element: <StaticHtmlPage html={statusPaymentSentHtml} selector="main" /> },
          {
            path: "/payments/status/order-received",
            element: <StaticHtmlPage html={statusOrderReceivedHtml} selector="main" />
          },
          { path: "/status-billpaid", element: <StaticHtmlPage html={statusBillPaidHtml} selector="main" /> },
          { path: "/status-billpaid-failled", element: <StaticHtmlPage html={statusBillPaidFailedHtml} selector="main" /> },
          { path: "/status-paymentsent", element: <StaticHtmlPage html={statusPaymentSentHtml} selector="main" /> },
          { path: "/status-order-received", element: <StaticHtmlPage html={statusOrderReceivedHtml} selector="main" /> },
          {
            path: "/payments/transaction-details",
            element: <StaticHtmlPage html={transactionDetailsHtml} selector="main" />
          },
          { path: "/transactiondetails-raw", element: <StaticHtmlPage html={transactionDetailsHtml} selector="main" /> }
        ]
      }
    ]
  }
]);
