import React from "react";
import { motion, useReducedMotion } from "framer-motion";
import {
  ArrowRight,
  BookOpen,
  Github,
  Layers,
  ShieldCheck,
  Workflow,
  Landmark,
  Route,
  Users,
  CreditCard,
  Receipt,
  FileText,
  CheckCircle2,
  Sparkles,
  LogIn,
  ChevronRight,
} from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";

/**
 * AONIK — Landing Page (GitBook-ish layout)
 * - White canvas, dotted grid + soft gradients
 * - GitBook-like header: nav left, auth + primary CTA right
 * - Hero: big headline + 2 CTAs + framed "product preview" + category chips
 * - Alternating feature rows (text + preview)
 */

const container = "mx-auto w-full max-w-7xl px-4 sm:px-6 lg:px-8";

function FadeInSection({
  children,
  delay = 0,
}: {
  children: React.ReactNode;
  delay?: number;
}) {
  const reduce = useReducedMotion();
  return (
    <motion.div
      initial={reduce ? { opacity: 1 } : { opacity: 0, y: 12 }}
      whileInView={reduce ? { opacity: 1 } : { opacity: 1, y: 0 }}
      viewport={{ once: true, margin: "-100px" }}
      transition={{ duration: 0.55, ease: "easeOut", delay }}
    >
      {children}
    </motion.div>
  );
}

function AonikMark() {
  return (
    <div className="flex items-center gap-2">
      <div
        aria-hidden
        className="h-9 w-9 rounded-2xl bg-neutral-900 text-neutral-50 grid place-items-center shadow-sm"
      >
        <span className="text-sm font-semibold tracking-tight">A</span>
      </div>
      <div className="leading-tight">
        <div className="text-sm font-semibold tracking-tight text-neutral-900">
          Aonik
        </div>
        <div className="text-xs text-neutral-500">Financial infrastructure</div>
      </div>
    </div>
  );
}

function Pill({
  children,
  icon,
}: {
  children: React.ReactNode;
  icon?: React.ReactNode;
}) {
  return (
    <div className="inline-flex items-center gap-2 rounded-full border border-neutral-200 bg-white px-3 py-1 text-xs text-neutral-700 shadow-sm">
      {icon}
      <span className="whitespace-nowrap">{children}</span>
    </div>
  );
}

function PreviewFrame({
  title = "Aonik primitives",
  subtitle = "Ledger-first • Audit-ready • Composable",
  chips = ["Orders", "Ledger", "Payments", "Compliance"],
}: {
  title?: string;
  subtitle?: string;
  chips?: string[];
}) {
  return (
    <div className="relative">
      {/* soft glow behind frame */}
      <div
        aria-hidden
        className="pointer-events-none absolute -inset-10 rounded-[40px] bg-gradient-to-b from-indigo-500/10 via-violet-500/5 to-transparent blur-2xl"
      />

      <div className="relative rounded-[28px] border border-neutral-200 bg-white shadow-[0_12px_40px_-20px_rgba(0,0,0,0.35)]">
        {/* fake browser bar */}
        <div className="flex items-center justify-between gap-3 border-b border-neutral-200 px-4 py-3">
          <div className="flex items-center gap-2">
            <div className="flex gap-1.5">
              <span className="h-2.5 w-2.5 rounded-full bg-neutral-300" />
              <span className="h-2.5 w-2.5 rounded-full bg-neutral-300" />
              <span className="h-2.5 w-2.5 rounded-full bg-neutral-300" />
            </div>
            <div className="hidden sm:block text-xs text-neutral-500">
              aonik.dev / platform
            </div>
          </div>

          <div className="text-xs text-neutral-500">Preview</div>
        </div>

        <div className="p-5 sm:p-6">
          <div className="flex items-start justify-between gap-4">
            <div>
              <div className="text-sm font-semibold tracking-tight text-neutral-900">
                {title}
              </div>
              <div className="mt-1 text-xs text-neutral-500">{subtitle}</div>
            </div>
            <div className="hidden sm:flex items-center gap-2 text-xs text-neutral-500">
              <ShieldCheck className="h-4 w-4" aria-hidden />
              <span>Audit-first</span>
            </div>
          </div>

          {/* “screenshot” body */}
          <div className="mt-4 grid gap-4 sm:grid-cols-12">
            {/* left rail */}
            <div className="sm:col-span-4 rounded-2xl border border-neutral-200 bg-neutral-50 p-3">
              <div className="space-y-2">
                {[
                  { label: "Identity & Parties", icon: Users },
                  { label: "Orders", icon: FileText },
                  { label: "Payments", icon: CreditCard },
                  { label: "Ledger", icon: Landmark },
                  { label: "Compliance", icon: ShieldCheck },
                ].map((i) => (
                  <div
                    key={i.label}
                    className="flex items-center gap-2 rounded-xl bg-white px-3 py-2 text-xs text-neutral-700 shadow-sm"
                  >
                    <i.icon className="h-4 w-4 text-indigo-600" aria-hidden />
                    <span className="truncate">{i.label}</span>
                  </div>
                ))}
              </div>
            </div>

            {/* main content */}
            <div className="sm:col-span-8 rounded-2xl border border-neutral-200 bg-white p-4">
              <div className="grid gap-3">
                <div className="rounded-2xl border border-neutral-200 bg-neutral-50 p-4">
                  <div className="flex items-center justify-between">
                    <div className="text-xs font-medium text-neutral-700">
                      Order → Payment → Ledger
                    </div>
                    <div className="text-xs text-neutral-500">
                      Provenance • IDs
                    </div>
                  </div>

                  <div className="mt-3 grid gap-2">
                    {[
                      {
                        k: "Order",
                        v: "Intent captured (why money moves)",
                        icon: FileText,
                      },
                      {
                        k: "Payment",
                        v: "Execution across rails (what happened)",
                        icon: CreditCard,
                      },
                      {
                        k: "Ledger",
                        v: "Double-entry proof (source of truth)",
                        icon: Landmark,
                      },
                    ].map((r) => (
                      <div
                        key={r.k}
                        className="flex items-start gap-3 rounded-xl bg-white px-3 py-2 shadow-sm"
                      >
                        <div className="mt-0.5 rounded-lg bg-indigo-600/10 p-2">
                          <r.icon
                            className="h-4 w-4 text-indigo-600"
                            aria-hidden
                          />
                        </div>
                        <div className="min-w-0">
                          <div className="text-xs font-medium text-neutral-800">
                            {r.k}
                          </div>
                          <div className="text-xs text-neutral-500">{r.v}</div>
                        </div>
                        <ChevronRight
                          className="ml-auto mt-2 h-4 w-4 text-neutral-300"
                          aria-hidden
                        />
                      </div>
                    ))}
                  </div>
                </div>

                <div className="grid gap-2 sm:grid-cols-2">
                  <div className="rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
                    <div className="flex items-center gap-2 text-xs font-medium text-neutral-800">
                      <Sparkles
                        className="h-4 w-4 text-indigo-600"
                        aria-hidden
                      />
                      AI governance
                    </div>
                    <div className="mt-2 text-xs text-neutral-500">
                      Agents propose; systems execute — approvals for high-risk.
                    </div>
                  </div>
                  <div className="rounded-2xl border border-neutral-200 bg-white p-4 shadow-sm">
                    <div className="flex items-center gap-2 text-xs font-medium text-neutral-800">
                      <Route className="h-4 w-4 text-indigo-600" aria-hidden />
                      Routing
                    </div>
                    <div className="mt-2 text-xs text-neutral-500">
                      Rules + connectors for real-world partner delivery.
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* category chips (GitBook-ish) */}
          <div className="mt-5 flex flex-wrap gap-2">
            {chips.map((c) => (
              <div
                key={c}
                className="rounded-full border border-neutral-200 bg-white px-3 py-1 text-xs text-neutral-600 shadow-sm"
              >
                {c}
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}

function FeatureRow({
  eyebrow,
  title,
  description,
  bullets,
  previewTitle,
  reverse = false,
}: {
  eyebrow: string;
  title: string;
  description: string;
  bullets: string[];
  previewTitle: string;
  reverse?: boolean;
}) {
  return (
    <div className="grid items-center gap-10 lg:grid-cols-12">
      <div className={`lg:col-span-5 ${reverse ? "lg:order-2" : ""}`}>
        <FadeInSection>
          <div className="text-xs font-semibold tracking-wide text-indigo-600">
            {eyebrow}
          </div>
          <h3 className="mt-2 text-2xl font-semibold tracking-tight text-neutral-900 sm:text-3xl">
            {title}
          </h3>
          <p className="mt-3 text-base leading-relaxed text-neutral-600">
            {description}
          </p>
          <div className="mt-5 space-y-2">
            {bullets.map((b) => (
              <div key={b} className="flex items-start gap-2 text-sm">
                <CheckCircle2
                  className="mt-0.5 h-4 w-4 text-indigo-600"
                  aria-hidden
                />
                <span className="text-neutral-700">{b}</span>
              </div>
            ))}
          </div>
        </FadeInSection>
      </div>

      <div className={`lg:col-span-7 ${reverse ? "lg:order-1" : ""}`}>
        <FadeInSection delay={0.08}>
          <PreviewFrame title={previewTitle} />
        </FadeInSection>
      </div>
    </div>
  );
}

export default function AonikLandingPage() {
  return (
    <div className="min-h-screen bg-white text-neutral-900">
      {/* Header (GitBook-ish) */}
      <header className="sticky top-0 z-50 border-b border-neutral-200 bg-white/70 backdrop-blur">
        <div className={`${container} h-16 flex items-center justify-between`}>
          <div className="flex items-center gap-8">
            <AonikMark />

            <nav className="hidden items-center gap-6 text-sm text-neutral-600 md:flex">
              <a className="hover:text-neutral-900" href="#platform">
                Platform
              </a>
              <a className="hover:text-neutral-900" href="#usecases">
                Use cases
              </a>
              <a className="hover:text-neutral-900" href="#open">
                Open-core
              </a>
              <a className="hover:text-neutral-900" href="#docs">
                Docs
              </a>
            </nav>
          </div>

          <div className="flex items-center gap-2">
            <Button variant="ghost" className="hidden md:inline-flex" asChild>
              <a href="#login" aria-label="Login">
                <LogIn className="mr-2 h-4 w-4" aria-hidden />
                Login
              </a>
            </Button>

            <Button variant="outline" className="hidden sm:inline-flex" asChild>
              <a href="#docs" aria-label="Read the docs">
                <BookOpen className="mr-2 h-4 w-4" aria-hidden />
                Read docs
              </a>
            </Button>

            <Button asChild>
              <a href="#early" aria-label="Join early access">
                Join early access
                <ArrowRight className="ml-2 h-4 w-4" aria-hidden />
              </a>
            </Button>
          </div>
        </div>
      </header>

      <main>
        {/* HERO (GitBook-ish) */}
        <section className="relative overflow-hidden">
          {/* dotted grid background */}
          <div
            aria-hidden
            className="pointer-events-none absolute inset-0 opacity-[0.55]"
            style={{
              backgroundImage:
                "radial-gradient(rgba(0,0,0,0.12) 1px, transparent 0)",
              backgroundSize: "26px 26px",
              backgroundPosition: "0 0",
            }}
          />
          {/* soft gradients */}
          <div aria-hidden className="pointer-events-none absolute inset-0">
            <div className="absolute -top-24 left-1/2 h-[26rem] w-[64rem] -translate-x-1/2 rounded-full bg-indigo-500/10 blur-3xl" />
            <div className="absolute -bottom-40 right-[-10rem] h-[28rem] w-[28rem] rounded-full bg-violet-500/10 blur-3xl" />
          </div>

          <div className={`${container} py-14 sm:py-18 lg:py-20`}>
            <div className="grid items-center gap-10 lg:grid-cols-12">
              <div className="lg:col-span-6">
                <FadeInSection>
                  <a
                    href="#open"
                    className="inline-flex items-center gap-2 rounded-full border border-neutral-200 bg-white px-3 py-1 text-xs text-neutral-700 shadow-sm hover:shadow-md transition"
                  >
                    <span className="rounded-full bg-indigo-600/10 px-2 py-0.5 font-medium text-indigo-700">
                      New
                    </span>
                    <span>
                      Open-core primitives for money movement + auditability
                    </span>
                    <ArrowRight
                      className="h-3.5 w-3.5 text-neutral-400"
                      aria-hidden
                    />
                  </a>

                  <h1 className="mt-6 text-4xl font-semibold tracking-tight text-neutral-900 sm:text-5xl">
                    Ledger-first{" "}
                    <span className="text-indigo-600">
                      financial infrastructure
                    </span>{" "}
                    for Africa + global scale.
                  </h1>

                  <p className="mt-5 max-w-xl text-base leading-relaxed text-neutral-600 sm:text-lg">
                    Aonik is a foundational platform for payments, remittances,
                    billing, and personal finance — built with clean primitives,
                    explicit intent, and audit-ready design.
                  </p>

                  <div className="mt-7 flex flex-col gap-3 sm:flex-row">
                    <Button size="lg" asChild>
                      <a href="#docs">
                        Start with the docs
                        <ArrowRight className="ml-2 h-4 w-4" aria-hidden />
                      </a>
                    </Button>
                    <Button size="lg" variant="outline" asChild>
                      <a href="#open">
                        View open-core model
                        <ChevronRight className="ml-2 h-4 w-4" aria-hidden />
                      </a>
                    </Button>
                  </div>

                  <div className="mt-8 flex flex-wrap gap-2">
                    <Pill
                      icon={
                        <Landmark
                          className="h-4 w-4 text-indigo-600"
                          aria-hidden
                        />
                      }
                    >
                      Ledger is the source of truth
                    </Pill>
                    <Pill
                      icon={
                        <FileText
                          className="h-4 w-4 text-indigo-600"
                          aria-hidden
                        />
                      }
                    >
                      Orders capture business intent
                    </Pill>
                    <Pill
                      icon={
                        <ShieldCheck
                          className="h-4 w-4 text-indigo-600"
                          aria-hidden
                        />
                      }
                    >
                      Audit-first by default
                    </Pill>
                  </div>

                  <div className="mt-8 flex items-center gap-3 text-sm text-neutral-600">
                    <Github className="h-4 w-4" aria-hidden />
                    <a
                      className="hover:text-neutral-900 underline underline-offset-4 decoration-neutral-300"
                      href="#repo"
                    >
                      Follow the project on GitHub
                    </a>
                  </div>
                </FadeInSection>
              </div>

              <div className="lg:col-span-6">
                <FadeInSection delay={0.1}>
                  <PreviewFrame
                    title="Aonik platform"
                    subtitle="Orders • Payments • Ledger • Compliance • AI controls"
                    chips={["Remittances", "Billing", "Payouts", "Personal finance"]}
                  />
                </FadeInSection>
              </div>
            </div>

            {/* “Category row” like GitBook */}
            <FadeInSection delay={0.15}>
              <div className="mt-10 flex flex-wrap items-center gap-2 text-xs text-neutral-600">
                {[
                  { label: "Payments & rails", icon: CreditCard },
                  { label: "Billing & invoicing", icon: Receipt },
                  { label: "Ledger & accounting", icon: Landmark },
                  { label: "Compliance & audit", icon: ShieldCheck },
                  { label: "AI governance", icon: Sparkles },
                ].map((x) => (
                  <div
                    key={x.label}
                    className="inline-flex items-center gap-2 rounded-full border border-neutral-200 bg-white px-3 py-2 shadow-sm"
                  >
                    <x.icon className="h-4 w-4 text-indigo-600" aria-hidden />
                    <span>{x.label}</span>
                  </div>
                ))}
              </div>
            </FadeInSection>
          </div>
        </section>

        {/* PLATFORM SUMMARY */}
        <section id="platform" className="border-t border-neutral-200 bg-white">
          <div className={`${container} py-14 sm:py-16`}>
            <FadeInSection>
              <div className="max-w-2xl">
                <div className="text-xs font-semibold tracking-wide text-indigo-600">
                  Platform
                </div>
                <h2 className="mt-2 text-2xl font-semibold tracking-tight text-neutral-900 sm:text-3xl">
                  Infrastructure that feels like a product
                </h2>
                <p className="mt-3 text-base leading-relaxed text-neutral-600">
                  Composable modules with explicit intent, provable outcomes, and
                  design constraints that match regulated reality.
                </p>
              </div>
            </FadeInSection>

            <div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
              {[
                {
                  icon: Layers,
                  title: "Composable primitives",
                  desc: "Use modules independently or together: identity, orders, payments, ledger, billing, routing.",
                },
                {
                  icon: Workflow,
                  title: "Operational clarity",
                  desc: "Orders orchestrate; payments execute; ledger proves — and every state change is explicit.",
                },
                {
                  icon: ShieldCheck,
                  title: "Audit + governance",
                  desc: "Policy-governed AI and traceable decisions; approvals are first-class for high-risk actions.",
                },
              ].map((i, idx) => (
                <FadeInSection key={i.title} delay={0.04 * idx}>
                  <div className="rounded-2xl border border-neutral-200 bg-white p-6 shadow-sm hover:shadow-md transition">
                    <div className="flex items-start gap-3">
                      <div className="rounded-xl bg-indigo-600/10 p-2">
                        <i.icon className="h-5 w-5 text-indigo-600" aria-hidden />
                      </div>
                      <div className="min-w-0">
                        <div className="text-sm font-semibold text-neutral-900">
                          {i.title}
                        </div>
                        <div className="mt-1 text-sm text-neutral-600">
                          {i.desc}
                        </div>
                      </div>
                    </div>
                  </div>
                </FadeInSection>
              ))}
            </div>
          </div>
        </section>

        {/* ALTERNATING FEATURE ROWS (GitBook style) */}
        <section className="border-t border-neutral-200 bg-neutral-50">
          <div className={`${container} py-14 sm:py-16 space-y-16`}>
            <FeatureRow
              eyebrow="Orders"
              title="Orders represent business intent — not payments"
              description="Capture why money moves, who is involved, and what must happen next. Payments and payouts fulfill intent; the ledger proves it."
              bullets={[
                "Single orchestration object across rails + partners",
                "Links parties, funding, fulfilment, compliance + ledger",
                "Supports both B2B billing and B2C personal finance flows",
              ]}
              previewTitle="Order-centric orchestration"
            />

            <FeatureRow
              eyebrow="Ledger"
              title="Double-entry accounting as the source of truth"
              description="A ledger that is immutable, auditable, and designed to support real settlement flows — not just reporting."
              bullets={[
                "Journal entries reference real sources (orders, payments, invoices)",
                "Clear invariants: debits/credits, statuses, reversals",
                "Built for multi-currency and regulated environments",
              ]}
              previewTitle="Ledger-first truth"
              reverse
            />

            <FeatureRow
              eyebrow="AI governance"
              title="Agents propose; systems execute"
              description="AI is powerful, but financially material actions must be governed. Every AI action is recorded, policy-checked, and auditable."
              bullets={[
                "AiRun provenance on material outputs",
                "Risk tier determines autonomy",
                "Human approval for high-risk actions (payments, postings, refunds)",
              ]}
              previewTitle="Policy-governed AI"
            />
          </div>
        </section>

        {/* USE CASES */}
        <section id="usecases" className="border-t border-neutral-200 bg-white">
          <div className={`${container} py-14 sm:py-16`}>
            <FadeInSection>
              <div className="max-w-2xl">
                <div className="text-xs font-semibold tracking-wide text-indigo-600">
                  Use cases
                </div>
                <h2 className="mt-2 text-2xl font-semibold tracking-tight text-neutral-900 sm:text-3xl">
                  Built for products, platforms, and rails
                </h2>
                <p className="mt-3 text-base leading-relaxed text-neutral-600">
                  Aonik stays broad without being vague — the primitives map to
                  real systems you already operate.
                </p>
              </div>
            </FadeInSection>

            <div className="mt-10 grid gap-4 lg:grid-cols-3">
              {[
                {
                  icon: Users,
                  title: "Consumer products",
                  desc: "Personal finance, budgets, bills, subscriptions — built on ledger + orders.",
                },
                {
                  icon: Receipt,
                  title: "Business platforms",
                  desc: "Billing, collections, invoicing, allocation — clean primitives for B2B workflows.",
                },
                {
                  icon: Route,
                  title: "Financial rails",
                  desc: "Remittances, payouts, routing + partners — with compliance + audit baked in.",
                },
              ].map((i, idx) => (
                <FadeInSection key={i.title} delay={0.05 * idx}>
                  <div className="rounded-2xl border border-neutral-200 bg-white p-6 shadow-sm hover:shadow-md transition">
                    <div className="flex items-start gap-3">
                      <div className="rounded-xl bg-indigo-600/10 p-2">
                        <i.icon className="h-5 w-5 text-indigo-600" aria-hidden />
                      </div>
                      <div>
                        <div className="text-sm font-semibold text-neutral-900">
                          {i.title}
                        </div>
                        <div className="mt-1 text-sm text-neutral-600">
                          {i.desc}
                        </div>
                      </div>
                    </div>
                  </div>
                </FadeInSection>
              ))}
            </div>
          </div>
        </section>

        {/* OPEN CORE */}
        <section id="open" className="border-t border-neutral-200 bg-neutral-50">
          <div className={`${container} py-14 sm:py-16`}>
            <FadeInSection>
              <div className="max-w-2xl">
                <div className="text-xs font-semibold tracking-wide text-indigo-600">
                  Open-core
                </div>
                <h2 className="mt-2 text-2xl font-semibold tracking-tight text-neutral-900 sm:text-3xl">
                  Open foundations. Production options.
                </h2>
                <p className="mt-3 text-base leading-relaxed text-neutral-600">
                  The core platform is open. Hosted and enterprise capabilities
                  can exist for teams that need managed operations and governance.
                </p>
              </div>
            </FadeInSection>

            <div className="mt-10 grid gap-6 md:grid-cols-2">
              <FadeInSection delay={0.05}>
                <Card className="h-full border-neutral-200 shadow-sm">
                  <CardHeader>
                    <CardTitle className="text-base">What’s open</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-3 text-sm text-neutral-600">
                    {[
                      "Core primitives and APIs",
                      "Reference integrations and patterns",
                      "Community contributions and transparency",
                    ].map((t) => (
                      <div key={t} className="flex items-start gap-2">
                        <CheckCircle2
                          className="mt-0.5 h-4 w-4 text-indigo-600"
                          aria-hidden
                        />
                        <span>{t}</span>
                      </div>
                    ))}
                    <div className="pt-2 flex gap-2">
                      <Button variant="outline" asChild>
                        <a href="#repo" aria-label="Follow on GitHub">
                          <Github className="mr-2 h-4 w-4" aria-hidden />
                          Follow on GitHub
                        </a>
                      </Button>
                      <Button variant="ghost" asChild>
                        <a href="#docs" aria-label="Read docs">
                          <BookOpen className="mr-2 h-4 w-4" aria-hidden />
                          Docs
                        </a>
                      </Button>
                    </div>
                    <div className="text-xs text-neutral-500">
                      Replace the GitHub link with your repo URL.
                    </div>
                  </CardContent>
                </Card>
              </FadeInSection>

              <FadeInSection delay={0.1}>
                <Card className="h-full border-neutral-200 shadow-sm">
                  <CardHeader>
                    <CardTitle className="text-base">
                      For teams shipping at scale
                    </CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-3 text-sm text-neutral-600">
                    {[
                      "Hosted deployments and managed upgrades",
                      "Enterprise governance, SLAs, and compliance workflows",
                      "Partner routing + operational tooling",
                    ].map((t) => (
                      <div key={t} className="flex items-start gap-2">
                        <CheckCircle2
                          className="mt-0.5 h-4 w-4 text-indigo-600"
                          aria-hidden
                        />
                        <span>{t}</span>
                      </div>
                    ))}
                    <div className="pt-2">
                      <Button asChild>
                        <a href="#early" aria-label="Join early access">
                          Join early access
                          <ArrowRight className="ml-2 h-4 w-4" aria-hidden />
                        </a>
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              </FadeInSection>
            </div>
          </div>
        </section>

        {/* DOCS */}
        <section id="docs" className="border-t border-neutral-200 bg-white">
          <div className={`${container} py-10`}>
            <FadeInSection>
              <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <div className="text-sm font-semibold text-neutral-900">
                    Documentation
                  </div>
                  <div className="mt-1 text-sm text-neutral-600">
                    Link your docs site here (e.g., docs.aonik.io). Keep the
                    landing page focused.
                  </div>
                </div>
                <Button variant="outline" asChild>
                  <a href="#docs-link" aria-label="Open documentation">
                    View documentation
                    <BookOpen className="ml-2 h-4 w-4" aria-hidden />
                  </a>
                </Button>
              </div>
            </FadeInSection>
          </div>
        </section>

        {/* FINAL CTA */}
        <section id="early" className="border-t border-neutral-200 bg-neutral-50">
          <div className={`${container} py-14 sm:py-16`}>
            <div className="grid gap-8 lg:grid-cols-12 lg:items-center">
              <div className="lg:col-span-7">
                <FadeInSection>
                  <h2 className="text-2xl font-semibold tracking-tight text-neutral-900 sm:text-3xl">
                    Build on foundations that won’t break later.
                  </h2>
                  <p className="mt-3 max-w-xl text-base leading-relaxed text-neutral-600">
                    Join early access for updates, design notes, and
                    opportunities to shape the platform. No spam. No fluff.
                  </p>

                  <div className="mt-6 flex flex-col gap-3 sm:flex-row">
                    <Button size="lg" asChild>
                      <a href="#community" aria-label="Join the community">
                        Join the community
                        <ArrowRight className="ml-2 h-4 w-4" aria-hidden />
                      </a>
                    </Button>
                    <Button size="lg" variant="outline" asChild>
                      <a href="#repo" aria-label="Follow project on GitHub">
                        Follow the project
                        <Github className="ml-2 h-4 w-4" aria-hidden />
                      </a>
                    </Button>
                  </div>
                </FadeInSection>
              </div>

              <div className="lg:col-span-5">
                <FadeInSection delay={0.1}>
                  <Card className="border-neutral-200 shadow-sm">
                    <CardHeader>
                      <CardTitle className="text-base">Get early access</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-3">
                      <div className="text-sm text-neutral-600">
                        Leave an email for launch notes and contributor updates.
                      </div>
                      <form
                        onSubmit={(e) => e.preventDefault()}
                        className="flex flex-col gap-2"
                        aria-label="Early access signup"
                      >
                        <label className="sr-only" htmlFor="email">
                          Email
                        </label>
                        <Input
                          id="email"
                          type="email"
                          placeholder="you@company.com"
                          className="bg-white"
                        />
                        <Button type="submit" className="w-full">
                          Request access
                          <ArrowRight className="ml-2 h-4 w-4" aria-hidden />
                        </Button>
                        <div className="text-xs text-neutral-500">
                          Replace this with your real form provider later.
                        </div>
                      </form>
                    </CardContent>
                  </Card>
                </FadeInSection>
              </div>
            </div>
          </div>
        </section>

        {/* FOOTER */}
        <footer className="border-t border-neutral-200 bg-white">
          <div className={`${container} py-10`}>
            <div className="flex flex-col gap-6 sm:flex-row sm:items-center sm:justify-between">
              <div className="flex items-center gap-3">
                <AonikMark />
                <div className="text-xs text-neutral-500">
                  © {new Date().getFullYear()} Aonik. Infrastructure-first.
                </div>
              </div>
              <div className="flex flex-wrap items-center gap-4 text-sm text-neutral-600">
                <a className="hover:text-neutral-900" href="#platform">
                  Platform
                </a>
                <a className="hover:text-neutral-900" href="#usecases">
                  Use cases
                </a>
                <a className="hover:text-neutral-900" href="#open">
                  Open-core
                </a>
                <a className="hover:text-neutral-900" href="#docs">
                  Docs
                </a>
              </div>
            </div>
          </div>
        </footer>
      </main>
    </div>
  );
}
