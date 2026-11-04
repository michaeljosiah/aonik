import type { AdminModule } from '../types';
import { PublishingPage } from '@/pages/arke/publishing/PublishingPage';
import { ArkeWorkspacePage } from '@/pages/arke/ArkeWorkspacePage';

// Publishing uses staff APIs; other destinations retain their explicit connection states.
export const arkeWorldsModule: AdminModule = {
  id: 'arke-worlds', name: 'Arke Worlds', requires: [],
  navigation: [], panels: [], panelComponents: {},
  routes: [
  { screen: { id: "arke.overview", label: "Overview", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-overview" }, path: '/arke/overview', element: ArkeWorkspacePage, requires: [] },
  { screen: { id: "arke.families", label: "Families", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-family" }, path: '/arke/families', element: ArkeWorkspacePage, requires: ["groups"] },
  { screen: { id: "arke.children", label: "Child profiles", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-children" }, path: '/arke/children', element: ArkeWorkspacePage, requires: ["groups"] },
  { screen: { id: "arke.consent", label: "Consent & privacy", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-policies" }, path: '/arke/consent', element: ArkeWorkspacePage, requires: ["groups"] },
  { screen: { id: "arke.worlds", label: "Worlds", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-worlds" }, path: '/arke/worlds', element: ArkeWorkspacePage, requires: ["workspaces"] },
  { screen: { id: "arke.editions", label: "Story library", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-editions" }, path: '/arke/editions', element: ArkeWorkspacePage, requires: ["workspaces"] },
  { screen: { id: "arke.generation", label: "Generation jobs", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-jobs" }, path: '/arke/generation', element: ArkeWorkspacePage, requires: ["workspaces"] },
  { screen: { id: "arke.auto-stories", label: "Monthly stories", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-auto-story" }, path: '/arke/auto-stories', element: ArkeWorkspacePage, requires: ["workspaces", "subscriptions"] },
  { screen: { id: "arke.safety", label: "Safety review", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-compliance" }, path: '/arke/safety', element: ArkeWorkspacePage, requires: ["workspaces"] },
  { screen: { id: "arke.subscriptions", label: "Subscriptions", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-billing" }, path: '/arke/subscriptions', element: ArkeWorkspacePage, requires: ["subscriptions"] },
  { screen: { id: "arke.plans", label: "Plans & entitlements", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-categories" }, path: '/arke/plans', element: ArkeWorkspacePage, requires: ["subscriptions"] },
  { screen: { id: "arke.credits", label: "Credits & usage", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-credits" }, path: '/arke/credits', element: ArkeWorkspacePage, requires: ["subscriptions"] },
  { screen: { id: "arke.print", label: "Print & fulfilment", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-print" }, path: '/arke/print', element: ArkeWorkspacePage, requires: ["workspaces", "subscriptions"] },
  { screen: { id: "arke.submissions", label: "Creator submissions", permissions: { allOf: ["Publishing.Review"] }, policy: "PublishingReviewPolicy", icon: "menu-submissions" }, path: '/arke/submissions', element: PublishingPage, requires: ["publishing"] },
  { screen: { id: "arke.submission-detail", label: "Submission review", permissions: { allOf: ["Publishing.Review"] }, policy: "PublishingReviewPolicy", icon: "menu-submissions" }, path: '/arke/submissions/:id', element: PublishingPage, isDynamic: true, requires: ["publishing"] },
  { screen: { id: "arke.catalogue", label: "Player catalogue", permissions: { anyOf: ["Publishing.Review", "Publishing.Publish", "Publishing.Withdraw"] }, policy: "PublishingManagementPolicy", icon: "menu-catalogue" }, path: '/arke/catalogue', element: PublishingPage, requires: ["publishing"] },
  { screen: { id: "arke.releases", label: "Release readiness", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-release" }, path: '/arke/releases', element: ArkeWorkspacePage, requires: [] },
  { screen: { id: "arke.community", label: "Community & campaigns", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-community" }, path: '/arke/community', element: ArkeWorkspacePage, requires: [] },
  { screen: { id: "arke.support", label: "Support inbox", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-support" }, path: '/arke/support', element: ArkeWorkspacePage, requires: [] },
  { screen: { id: "arke.reports", label: "Reports", permissions: { authenticatedAdmin: true }, policy: "AdminPolicy", icon: "menu-usage" }, path: '/arke/reports', element: ArkeWorkspacePage, requires: [] },
  ],
  breadcrumbs: [{ pathPrefix: '/arke', trail: ['Arke Worlds'] }],
};
