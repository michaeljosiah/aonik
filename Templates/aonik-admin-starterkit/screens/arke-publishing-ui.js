// Generated from the live publishing view and shared icons. Run scripts/build-publishing-design.mjs to regenerate.
var ArkePublishingUI = (() => {
  var __defProp = Object.defineProperty;
  var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
  var __getOwnPropNames = Object.getOwnPropertyNames;
  var __hasOwnProp = Object.prototype.hasOwnProperty;
  var __export = (target, all) => {
    for (var name in all)
      __defProp(target, name, { get: all[name], enumerable: true });
  };
  var __copyProps = (to, from, except, desc) => {
    if (from && typeof from === "object" || typeof from === "function") {
      for (let key of __getOwnPropNames(from))
        if (!__hasOwnProp.call(to, key) && key !== except)
          __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
    }
    return to;
  };
  var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

  // src/Aonik.AdminUi/src/pages/arke/publishing/PublishingDesignEntry.tsx
  var PublishingDesignEntry_exports = {};
  __export(PublishingDesignEntry_exports, {
    AonikTemplateIcon: () => AonikTemplateIcon,
    PublishingWorkspaceView: () => PublishingWorkspaceView
  });

  // canvas-react:react
  var { useEffect, useId, useRef, useState } = globalThis.React;

  // src/Aonik.AdminUi/src/pages/arke/publishing/presentation.ts
  var labels = { "in-review": "In review", "changes-requested": "Changes requested", "awaiting-upload": "Awaiting upload", "approved": "Approved", "published": "Published", "withdrawn": "Withdrawn", "held": "Held", "validating": "Verifying", "passed": "Verified", "pending": "Queued", "copying": "Delivering", "committed": "Complete", "uncertain": "Needs reconciliation", "dead-letter": "Needs attention", "retryable": "Retry available", "synced": "Up to date", "disabled": "Not enabled", "syncing": "Syncing", "approve": "Approved", "request-changes": "Changes requested", "reject": "Rejected", "world": "World edition", "guide": "Guide", "part": "Part", "production": "Production", "story": "Interactive story", "comic": "Graphic novel" };
  var statusLabel = (value) => labels[value] ?? value.replaceAll("-", " ");
  function reasonLabel(code) {
    const messages = { "queued": "Queued for a worker to reconcile.", "published": "Publication is committed.", "withdrawn": "Withdrawal is committed.", "baseline-imported": "Qualified baseline recorded.", "latest-head-confirmed": "Latest canonical head confirmed.", "already-published": "This revision has already been published.", "active-approval-required": "An active approval is required for this revision.", "part-base-capability-unavailable": "Qualify the base production and released chapters before appending a part.", "part-chapter-lineage-changed": "This part repeats a released chapter identifier or chapter number.", "publication-policy-changed": "The publication policy changed. An operator must reconcile the qualified configuration.", "publication-bytes-mismatch": "The public bytes differ from the approved package. Investigate before retrying.", "passing-validation-required": "The package needs complete, passing verification.", "review-presentation-unavailable": "This package needs fresh verification before review.", "submission-not-in-review": "This revision has already left the review queue.", "rights-evidence-required": "The creator needs to provide rights and attribution evidence.", "rights-asset-coverage-required": "Rights evidence does not cover every file in this package.", "artwork-evidence-required": "A verified cover must be selected.", "approval-material-changed": "The approval no longer matches current review material.", "publication-held": "A publication hold needs to be resolved first.", "delivery-disabled": "New publication is paused for this channel.", "delivery-not-configured": "The channel needs publication setup and qualification.", "baseline-required": "Initialise the qualified catalogue baseline before the first release.", "review-policy-changed": "The package must be checked against the current policy.", "publication-lineage-changed": "The public edition or part sequence has changed. Reload before proceeding.", "public-transport-uncertain": "Delivery could not be confirmed. Reconcile the same operation before trying another release.", "remote-ahead": "The legacy mirror has a newer serial. An operator must reconcile its authoritative baseline.", "same-serial-conflict": "The mirror has different content at the same serial. An operator must reconcile it.", "mirror-not-configured": "The optional mirror needs a qualified connection.", "attempt-limit": "Automatic attempts stopped. Review the cause before requesting another attempt.", "publication-claim-lost": "Another worker took over this operation. Refresh its status.", "approval-authority-changed": "An independent authorised editor must renew the approval.", "self-review-forbidden": "A different editor must review this submission.", "review-version-changed": "This review changed while it was open. Reload and check it again.", "review-material-changed": "The review snapshot changed. Reload and confirm the current revision.", "review-revision-changed": "The creator submitted a different revision. Open the new revision." };
    return code ? messages[code] ?? "A required check is incomplete. Review the recorded status before continuing." : "All required server checks are current.";
  }

  // canvas-react:react/jsx-runtime
  var Fragment = globalThis.React.Fragment;
  function jsx(type, props, key) {
    return globalThis.React.createElement(type, { ...props, key });
  }
  var jsxs = jsx;

  // src/Aonik.AdminUi/src/pages/arke/publishing/PublishingViews.tsx
  var date = (value) => value ? new Date(value).toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" }) : "\u2014";
  var bytes = (value) => value >= 1048576 ? (value / 1048576).toFixed(1) + " MB" : value >= 1024 ? (value / 1024).toFixed(1) + " KB" : value + " bytes";
  function Pill({ value }) {
    const tone = /^(approved|published|passed|committed|synced)$/.test(value) ? "good" : /^(rejected|withdrawn|dead-letter)$/.test(value) ? "bad" : /^(held|uncertain|retryable|changes-requested|in-review|pending|copying|validating)$/.test(value) ? "warn" : "";
    return /* @__PURE__ */ jsx("span", { className: "ap-pill", "data-tone": tone, children: statusLabel(value) });
  }
  function Card({ title, note, action, children }) {
    return /* @__PURE__ */ jsxs("section", { className: "ap-card", children: [
      /* @__PURE__ */ jsxs("div", { className: "ap-card-head", children: [
        /* @__PURE__ */ jsxs("div", { children: [
          /* @__PURE__ */ jsx("h2", { children: title }),
          note && /* @__PURE__ */ jsx("p", { children: note })
        ] }),
        action
      ] }),
      /* @__PURE__ */ jsx("div", { className: "ap-card-body", children })
    ] });
  }
  function Facts({ items }) {
    return /* @__PURE__ */ jsx("dl", { className: "ap-facts", children: items.map(([key, value]) => /* @__PURE__ */ jsxs("div", { children: [
      /* @__PURE__ */ jsx("dt", { children: key }),
      /* @__PURE__ */ jsx("dd", { children: value ?? "\u2014" })
    ] }, key)) });
  }
  function Hash({ value }) {
    return /* @__PURE__ */ jsx("code", { className: "ap-hash", children: value || "\u2014" });
  }
  function Banner({ title, children, tone = "warn" }) {
    return /* @__PURE__ */ jsx("div", { className: "ap-banner", "data-tone": tone, role: tone === "bad" ? "alert" : void 0, children: /* @__PURE__ */ jsxs("div", { children: [
      /* @__PURE__ */ jsx("strong", { children: title }),
      children && /* @__PURE__ */ jsx("p", { children })
    ] }) });
  }
  function Button({ children, onClick, disabled = false, primary = false, danger = false, icon, Icon }) {
    return /* @__PURE__ */ jsxs("button", { className: "ap-button" + (primary ? " ap-primary" : "") + (danger ? " ap-danger" : ""), disabled, onClick, type: "button", children: [
      icon && /* @__PURE__ */ jsx(Icon, { name: icon, size: 15 }),
      /* @__PURE__ */ jsx("span", { children })
    ] });
  }
  function Pager({ page, size, total, actions, Icon }) {
    return /* @__PURE__ */ jsxs("div", { className: "ap-pager", children: [
      /* @__PURE__ */ jsxs("span", { children: [
        total === 0 ? "No records" : `${(page - 1) * size + 1}\u2013${Math.min(page * size, total)} of ${total}`,
        " \xB7 Page ",
        page
      ] }),
      /* @__PURE__ */ jsxs("div", { className: "ap-actions", children: [
        /* @__PURE__ */ jsx(Button, { Icon, disabled: page === 1, onClick: () => actions.page(page - 1), children: "Previous" }),
        /* @__PURE__ */ jsx(Button, { Icon, disabled: page * size >= total, onClick: () => actions.page(page + 1), children: "Next" })
      ] })
    ] });
  }
  function PublishingWorkspaceView({ model: m, actions: a, Icon }) {
    const id = useId();
    const channel = m.channels.find((c) => c.key === m.channelKey);
    const review = m.review;
    const receipt = review?.submission;
    const r2 = receipt?.revision;
    const detail = m.view === "detail";
    const catalogue = m.view === "catalogue";
    const title = detail ? r2?.title ?? "Submission review" : catalogue ? "Player catalogue" : "Creator submissions";
    const subtitle = detail ? r2 ? `${r2.creatorDisplayName} \xB7 Revision ${r2.number} \xB7 ${r2.edition}` : "Review the exact package and its publication evidence." : catalogue ? "Approved editions, immutable pins and delivery status." : "Review finished public editions from Studio creators.";
    const ops = m.release?.operations ?? m.catalogue?.operations ?? [];
    return /* @__PURE__ */ jsxs("div", { className: "ap-page", "data-publishing-view": m.view, "aria-busy": m.loading, children: [
      m.example && /* @__PURE__ */ jsx("div", { className: "ap-example", children: "Fictional marketing data \xB7 Local editable preview \xB7 No publication or messages are sent" }),
      /* @__PURE__ */ jsxs("header", { className: "ap-heading", children: [
        /* @__PURE__ */ jsxs("div", { children: [
          /* @__PURE__ */ jsxs("p", { className: "ap-eyebrow", children: [
            "Arke Worlds ",
            /* @__PURE__ */ jsx("span", { "aria-hidden": "true", children: "/" }),
            " Publishing"
          ] }),
          /* @__PURE__ */ jsx("h1", { children: title }),
          /* @__PURE__ */ jsx("p", { className: "ap-subtitle", children: subtitle })
        ] }),
        /* @__PURE__ */ jsxs("div", { className: "ap-header-tools", children: [
          /* @__PURE__ */ jsx("label", { className: "ap-small ap-muted", htmlFor: id + "-channel", children: "Channel" }),
          /* @__PURE__ */ jsxs("select", { id: id + "-channel", className: "ap-select ap-channel", "aria-label": "Publishing channel", value: m.channelKey, disabled: m.loading || m.busy, onChange: (e) => a.channel(e.target.value), children: [
            m.channels.length === 0 && /* @__PURE__ */ jsx("option", { value: "", children: "No assigned channel" }),
            m.channels.map((c) => /* @__PURE__ */ jsx("option", { value: c.key, children: c.key }, c.key))
          ] }),
          /* @__PURE__ */ jsx(Button, { Icon, onClick: a.reload, disabled: m.loading || m.busy, icon: "arrows", children: "Refresh" })
        ] })
      ] }),
      /* @__PURE__ */ jsxs("div", { className: "ap-tabs", "aria-label": "Publishing workspace", children: [
        /* @__PURE__ */ jsx("button", { type: "button", "aria-current": !catalogue ? "page" : void 0, onClick: () => a.navigate("queue"), children: "Creator submissions" }),
        /* @__PURE__ */ jsx("button", { type: "button", "aria-current": catalogue ? "page" : void 0, onClick: () => a.navigate("catalogue"), children: "Player catalogue" })
      ] }),
      m.notice && /* @__PURE__ */ jsx("div", { className: "ap-banner ap-notice", role: "status", children: m.notice }),
      m.error ? /* @__PURE__ */ jsxs(Banner, { title: m.error.code === "self-review-forbidden" ? "An independent editor is required" : "This view is unavailable", tone: "bad", children: [
        /* @__PURE__ */ jsx("span", { children: m.error.message }),
        /* @__PURE__ */ jsxs("div", { className: "ap-actions", style: { marginTop: 12 }, children: [
          /* @__PURE__ */ jsx(Button, { Icon, onClick: a.reload, children: "Try again" }),
          detail && /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.navigate("queue"), children: "Back to submissions" })
        ] })
      ] }) : m.channels.length === 0 && !m.loading ? /* @__PURE__ */ jsx(Card, { title: "No publishing channel assigned", children: /* @__PURE__ */ jsx("p", { className: "ap-muted", children: "Ask your workspace owner to assign your review or publication permissions and a publisher channel." }) }) : m.loading && !m.queue && !m.review && !m.catalogue ? /* @__PURE__ */ jsx("section", { "aria-label": "Loading publishing records", className: "ap-card ap-card-body", children: [80, 55, 95, 70, 85].map((w, i) => /* @__PURE__ */ jsx("div", { className: "ap-skeleton", style: { width: w + "%" } }, i)) }) : /* @__PURE__ */ jsxs(Fragment, { children: [
        !detail && !catalogue && /* @__PURE__ */ jsxs(Fragment, { children: [
          /* @__PURE__ */ jsxs("div", { className: "ap-metrics", children: [
            /* @__PURE__ */ jsx(Metric, { label: "Submissions", value: m.queue?.totalCount ?? 0, note: "Matching your filters" }),
            /* @__PURE__ */ jsx(Metric, { label: "Channel", value: channel?.key ?? "\u2014", note: "Your assigned publisher scope" }),
            /* @__PURE__ */ jsx(Metric, { label: "Catalogue serial", value: channel?.serial ?? "\u2014", note: channel?.initialised ? "Committed public head" : "Baseline not initialised" }),
            /* @__PURE__ */ jsx(Metric, { label: "Legacy mirror", value: statusLabel(channel?.mirrorStatus ?? "disabled"), note: channel?.mirroredSerial != null ? "Confirmed serial " + channel.mirroredSerial : "Separate from canonical delivery" })
          ] }),
          !channel?.canReview ? /* @__PURE__ */ jsx(Banner, { title: "Review permission required", children: "Your channel access covers catalogue operations. An assigned editor must review submissions." }) : /* @__PURE__ */ jsxs("section", { className: "ap-card", children: [
            /* @__PURE__ */ jsxs("div", { className: "ap-toolbar", children: [
              /* @__PURE__ */ jsxs("label", { className: "ap-search", children: [
                /* @__PURE__ */ jsx(Icon, { name: "search", size: 16 }),
                /* @__PURE__ */ jsx("input", { "aria-label": "Search submissions", placeholder: "Search titles, creators or world IDs", value: m.query, onChange: (e) => a.query(e.target.value), maxLength: 200 })
              ] }),
              /* @__PURE__ */ jsxs("label", { className: "ap-row ap-small ap-muted", children: [
                "Status",
                /* @__PURE__ */ jsxs("select", { "aria-label": "Submission status", className: "ap-select", value: m.status, onChange: (e) => a.status(e.target.value), children: [
                  /* @__PURE__ */ jsx("option", { value: "", children: "All statuses" }),
                  ["in-review", "validating", "held", "changes-requested", "rejected", "approved"].map((s) => /* @__PURE__ */ jsx("option", { value: s, children: statusLabel(s) }, s))
                ] })
              ] })
            ] }),
            /* @__PURE__ */ jsx("div", { className: "ap-table-wrap", children: /* @__PURE__ */ jsxs("table", { className: "ap-table", children: [
              /* @__PURE__ */ jsx("thead", { children: /* @__PURE__ */ jsxs("tr", { children: [
                /* @__PURE__ */ jsx("th", { children: "Submission" }),
                /* @__PURE__ */ jsx("th", { children: "Creator" }),
                /* @__PURE__ */ jsx("th", { children: "Edition" }),
                /* @__PURE__ */ jsx("th", { children: "Received" }),
                /* @__PURE__ */ jsx("th", { children: "Verification" }),
                /* @__PURE__ */ jsx("th", { children: "Status" }),
                /* @__PURE__ */ jsx("th", { children: /* @__PURE__ */ jsx("span", { className: "ap-small", children: "Review" }) })
              ] }) }),
              /* @__PURE__ */ jsx("tbody", { children: m.queue?.items.map((item) => /* @__PURE__ */ jsxs("tr", { children: [
                /* @__PURE__ */ jsxs("td", { children: [
                  /* @__PURE__ */ jsx("button", { className: "ap-record-link", onClick: () => a.navigate("detail", item.id), children: item.revision.title }),
                  /* @__PURE__ */ jsx("small", { children: item.revision.worldId })
                ] }),
                /* @__PURE__ */ jsx("td", { children: item.revision.creatorDisplayName }),
                /* @__PURE__ */ jsxs("td", { children: [
                  item.revision.edition,
                  /* @__PURE__ */ jsxs("small", { children: [
                    "Revision ",
                    item.revision.number
                  ] })
                ] }),
                /* @__PURE__ */ jsx("td", { children: date(item.createdAt) }),
                /* @__PURE__ */ jsx("td", { children: /* @__PURE__ */ jsx(Pill, { value: item.revision.packageVerified ? "passed" : "pending" }) }),
                /* @__PURE__ */ jsx("td", { children: /* @__PURE__ */ jsx(Pill, { value: item.status }) }),
                /* @__PURE__ */ jsx("td", { children: /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.navigate("detail", item.id), children: "Open review" }) })
              ] }, item.id)) })
            ] }) }),
            m.queue?.items.length === 0 && /* @__PURE__ */ jsxs("div", { className: "ap-empty", children: [
              /* @__PURE__ */ jsx(Icon, { name: "menu-submissions", size: 25 }),
              /* @__PURE__ */ jsx("h2", { children: "No submissions match this view" }),
              /* @__PURE__ */ jsx("p", { children: "Adjust your filters or wait for a creator to submit a verified edition." })
            ] }),
            /* @__PURE__ */ jsx(Pager, { page: m.page, size: m.queue?.pageSize ?? 25, total: m.queue?.totalCount ?? 0, actions: a, Icon })
          ] })
        ] }),
        detail && review && r2 && receipt && /* @__PURE__ */ jsxs(Fragment, { children: [
          /* @__PURE__ */ jsxs("div", { className: "ap-between", style: { marginBottom: 18 }, children: [
            /* @__PURE__ */ jsxs("div", { className: "ap-actions", children: [
              /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.navigate("queue"), children: "\u2190 Submissions" }),
              /* @__PURE__ */ jsx(Pill, { value: receipt.status }),
              /* @__PURE__ */ jsx("span", { className: "ap-muted ap-small", children: r2.worldId })
            ] }),
            /* @__PURE__ */ jsxs("div", { className: "ap-actions", children: [
              channel?.canReview && /* @__PURE__ */ jsxs(Fragment, { children: [
                /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.open("request-changes"), disabled: !!review.blockingCode || m.busy, children: "Request changes" }),
                /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.open("reject"), disabled: !!review.blockingCode || m.busy, danger: true, children: "Reject" }),
                /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.open("approve"), disabled: !!review.blockingCode || !!review.approvalBlockingCode || m.busy, primary: true, icon: "check", children: "Approve revision" })
              ] }),
              receipt.status === "approved" && channel?.canPublish && /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.open("publish"), disabled: !m.release || !!m.release.blockingCode || m.busy, primary: true, icon: "menu-release", children: "Publish edition" })
            ] })
          ] }),
          review.blockingCode && review.blockingCode !== "submission-not-in-review" && /* @__PURE__ */ jsx(Banner, { title: "This revision cannot advance", children: reasonLabel(review.blockingCode) }),
          !review.blockingCode && review.approvalBlockingCode && /* @__PURE__ */ jsxs(Banner, { title: "Approval needs more evidence", children: [
            reasonLabel(review.approvalBlockingCode),
            " The verified revision can still be returned for changes."
          ] }),
          /* @__PURE__ */ jsx("div", { className: "ap-tabs", role: "tablist", "aria-label": "Submission detail", children: ["Overview", "Package & preview", "Validation", "Rights & audience", "History", "Publication"].map((tab, index, list) => /* @__PURE__ */ jsx("button", { id: id + "-tab-" + index, role: "tab", type: "button", "aria-selected": m.tab === tab, "aria-controls": id + "-panel", tabIndex: m.tab === tab ? 0 : -1, onClick: () => a.tab(tab), onKeyDown: (e) => {
            if (["ArrowLeft", "ArrowRight", "Home", "End"].includes(e.key)) {
              e.preventDefault();
              const target = e.key === "Home" ? 0 : e.key === "End" ? list.length - 1 : (index + (e.key === "ArrowRight" ? 1 : list.length - 1)) % list.length;
              a.tab(list[target]);
              document.getElementById(id + "-tab-" + target)?.focus();
            }
          }, children: tab }, tab)) }),
          /* @__PURE__ */ jsxs("div", { id: id + "-panel", role: "tabpanel", "aria-labelledby": id + "-tab-" + ["Overview", "Package & preview", "Validation", "Rights & audience", "History", "Publication"].indexOf(m.tab), children: [
            (m.tab === "Overview" || m.tab === "Package & preview") && /* @__PURE__ */ jsxs("div", { className: "ap-grid", children: [
              /* @__PURE__ */ jsxs("div", { className: "ap-stack", children: [
                /* @__PURE__ */ jsxs(Card, { title: m.tab === "Overview" ? "Edition overview" : "Exact package", note: "The creator\u2019s frozen revision. Changes require another upload and review.", children: [
                  /* @__PURE__ */ jsx("p", { className: "ap-muted", children: r2.summary || "No summary provided." }),
                  /* @__PURE__ */ jsx(Facts, { items: [["Creator", r2.creatorDisplayName], ["World", r2.worldId], ["Edition", r2.edition], ["Revision", String(r2.number)], ["Language", r2.language], ["Package", r2.packageFileName + " \xB7 " + bytes(r2.declaredPackageBytes)], ["Audience", r2.editorialMaterial?.audience ?? "Not supplied"], ["Age rating", r2.editorialMaterial?.ageRating ?? "Not supplied"]] }),
                  /* @__PURE__ */ jsxs("div", { className: "ap-actions", style: { marginTop: 18 }, children: [
                    /* @__PURE__ */ jsx(Button, { Icon, onClick: a.download, disabled: !review.packageAvailable, icon: "download", children: "Download private package" }),
                    /* @__PURE__ */ jsx("span", { className: "ap-small ap-muted", children: "Private access is recorded." })
                  ] })
                ] }),
                /* @__PURE__ */ jsx(Card, { title: "Review identity", note: "This is the exact material your decision will bind.", children: /* @__PURE__ */ jsx(Facts, { items: [["Archive SHA-256", /* @__PURE__ */ jsx(Hash, { value: r2.declaredPackageSha256 })], ["Review digest", /* @__PURE__ */ jsx(Hash, { value: review.snapshot?.digest })], ["Player fingerprint", /* @__PURE__ */ jsx(Hash, { value: review.validation?.package?.fingerprint })]] }) })
              ] }),
              /* @__PURE__ */ jsxs("div", { className: "ap-stack", children: [
                /* @__PURE__ */ jsxs(Card, { title: "Private artwork", note: "Verified image only \xB7 Documents and media are downloaded separately.", children: [
                  m.artworkUrl ? /* @__PURE__ */ jsx("img", { className: "ap-cover", src: m.artworkUrl, alt: "Private verified cover for " + r2.title }) : /* @__PURE__ */ jsxs("div", { className: "ap-cover-empty", children: [
                    /* @__PURE__ */ jsx(Icon, { name: "menu-editions", size: 30 }),
                    /* @__PURE__ */ jsx("span", { children: m.artworkError || "No verified artwork preview available" })
                  ] }),
                  /* @__PURE__ */ jsx(Facts, { items: [["Attribution", r2.editorialMaterial?.attribution ?? "Not supplied"], ["Release type", statusLabel(r2.editorialMaterial?.releaseKind ?? "Not supplied")]] })
                ] }),
                /* @__PURE__ */ jsxs(Card, { title: "Before you decide", children: [
                  /* @__PURE__ */ jsx(Check, { Icon, title: "Check the complete edition", text: "Open the package and review its content, attribution and audience suitability." }),
                  /* @__PURE__ */ jsx(Check, { Icon, title: "Review rights evidence", text: "Inspect the referenced evidence for every file, including narration and artwork." }),
                  /* @__PURE__ */ jsx(Check, { Icon, title: "Approval is a separate step", text: "The approved edition enters Player only after publication delivery checks complete." })
                ] })
              ] })
            ] }),
            m.tab === "Validation" && /* @__PURE__ */ jsxs("div", { className: "ap-stack", children: [
              /* @__PURE__ */ jsx(Card, { title: "Package verification", action: /* @__PURE__ */ jsx(Pill, { value: review.validation?.status ?? "pending" }), children: /* @__PURE__ */ jsx(Facts, { items: [["Result", reasonLabel(review.validation?.status === "passed" ? null : review.validation?.code)], ["Complete asset checks", review.validation?.package?.passed ? "All inventoried files verified" : "Awaiting passing report"], ["Checked", date(review.validation?.completedAt ?? null)], ["Schema", review.validation?.package?.presentation?.schemaVersion ?? "Unknown"], ["Reader commit", /* @__PURE__ */ jsx(Hash, { value: review.validation?.readerCommit })], ["Verifier image", /* @__PURE__ */ jsx(Hash, { value: review.validation?.verifierImage })]] }) }),
              /* @__PURE__ */ jsxs("section", { className: "ap-card", children: [
                /* @__PURE__ */ jsx("div", { className: "ap-card-head", children: /* @__PURE__ */ jsxs("div", { children: [
                  /* @__PURE__ */ jsx("h2", { children: "Verified asset inventory" }),
                  /* @__PURE__ */ jsx("p", { children: "Every listed file is bound to the exact package and review." })
                ] }) }),
                /* @__PURE__ */ jsx("div", { className: "ap-table-wrap", children: /* @__PURE__ */ jsxs("table", { className: "ap-table", children: [
                  /* @__PURE__ */ jsx("thead", { children: /* @__PURE__ */ jsxs("tr", { children: [
                    /* @__PURE__ */ jsx("th", { children: "Asset" }),
                    /* @__PURE__ */ jsx("th", { children: "Type" }),
                    /* @__PURE__ */ jsx("th", { children: "Size" }),
                    /* @__PURE__ */ jsx("th", { children: "SHA-256" })
                  ] }) }),
                  /* @__PURE__ */ jsx("tbody", { children: review.validation?.package?.assets.map((asset) => /* @__PURE__ */ jsxs("tr", { children: [
                    /* @__PURE__ */ jsxs("td", { children: [
                      asset.id,
                      /* @__PURE__ */ jsx("small", { children: asset.path })
                    ] }),
                    /* @__PURE__ */ jsx("td", { children: asset.mediaType }),
                    /* @__PURE__ */ jsx("td", { children: bytes(asset.byteLength) }),
                    /* @__PURE__ */ jsx("td", { children: /* @__PURE__ */ jsx(Hash, { value: asset.sha256 }) })
                  ] }, asset.id)) })
                ] }) })
              ] })
            ] }),
            m.tab === "Rights & audience" && /* @__PURE__ */ jsxs("div", { className: "ap-grid", children: [
              /* @__PURE__ */ jsxs("div", { className: "ap-stack", children: [
                /* @__PURE__ */ jsx(Card, { title: "Attribution and audience", children: /* @__PURE__ */ jsx(Facts, { items: [["Creator attribution", r2.creatorDisplayName], ["Public attribution", r2.editorialMaterial?.attribution ?? "Missing"], ["Audience", r2.editorialMaterial?.audience ?? "Missing"], ["Age rating", r2.editorialMaterial?.ageRating ?? "Missing"], ["Release type", statusLabel(r2.editorialMaterial?.releaseKind ?? "Missing")], ["Distribution statement", r2.rightsDeclarationVersion], ["Creator grant", r2.distributionPermissionGranted ? "Explicitly granted" : "Not granted"]] }) }),
                /* @__PURE__ */ jsx(Card, { title: "Rights evidence", note: "Private references must be inspected by the editor. A creator\u2019s assertion does not replace editorial review.", children: r2.editorialMaterial?.rightsEvidence.length ? r2.editorialMaterial.rightsEvidence.map((e, i) => /* @__PURE__ */ jsxs("div", { style: { marginBottom: 20 }, children: [
                  /* @__PURE__ */ jsx("h3", { children: e.rightsBasis }),
                  /* @__PURE__ */ jsx("p", { className: "ap-muted", children: e.evidenceReference }),
                  /* @__PURE__ */ jsx("div", { className: "ap-actions", children: e.assetIds.map((asset) => /* @__PURE__ */ jsx("span", { className: "ap-pill", children: asset }, asset)) })
                ] }, i)) : /* @__PURE__ */ jsx("p", { className: "ap-muted", children: "No rights evidence was supplied. Request a revised submission." }) })
              ] }),
              /* @__PURE__ */ jsxs(Card, { title: "Decision boundaries", children: [
                /* @__PURE__ */ jsx(Check, { Icon, title: "A different human reviews", text: "Originating users and everyone linked to the creator party are excluded." }),
                /* @__PURE__ */ jsx(Check, { Icon, title: "Evidence stays frozen", text: "Changing rights, artwork, metadata or bytes requires a new revision." }),
                /* @__PURE__ */ jsx(Check, { Icon, title: "Only reviewed public material", text: "Private Studio worlds and authoring history remain outside the public package." })
              ] })
            ] }),
            m.tab === "History" && /* @__PURE__ */ jsx(Card, { title: "Editorial history", note: "Internal notes are visible only to authorised staff. Creators receive the separate creator message.", children: review.decisions.length ? /* @__PURE__ */ jsx("ol", { className: "ap-history", children: review.decisions.map((d) => /* @__PURE__ */ jsxs("li", { children: [
              /* @__PURE__ */ jsxs("div", { className: "ap-between", children: [
                /* @__PURE__ */ jsxs("h3", { children: [
                  statusLabel(d.decision),
                  " \xB7 Revision ",
                  d.revisionId === r2.id ? r2.number : d.revisionId.slice(0, 8)
                ] }),
                d.isActive && /* @__PURE__ */ jsx(Pill, { value: d.decision === "approve" ? "approved" : d.decision === "request-changes" ? "changes-requested" : "rejected" })
              ] }),
              /* @__PURE__ */ jsxs("small", { children: [
                date(d.decidedAt),
                " \xB7 Policy ",
                d.policyVersion
              ] }),
              /* @__PURE__ */ jsxs("p", { children: [
                /* @__PURE__ */ jsx("strong", { children: "Creator message" }),
                /* @__PURE__ */ jsx("br", {}),
                d.creatorMessage || "No additional message"
              ] }),
              /* @__PURE__ */ jsxs("p", { className: "ap-internal-note", children: [
                /* @__PURE__ */ jsx("strong", { children: "Internal assessment" }),
                /* @__PURE__ */ jsx("br", {}),
                d.internalReason
              ] }),
              /* @__PURE__ */ jsxs("details", { style: { marginTop: 10 }, children: [
                /* @__PURE__ */ jsx("summary", { className: "ap-small ap-muted", children: "Decision provenance" }),
                /* @__PURE__ */ jsx(Facts, { items: [["Reviewer user", d.actorUserId], ["Reviewer party", d.actorPartyId], ["Review digest", /* @__PURE__ */ jsx(Hash, { value: d.reviewDigest })]] })
              ] })
            ] }, d.id)) }) : /* @__PURE__ */ jsx("div", { className: "ap-empty", children: "No editorial decision has been recorded." }) }),
            m.tab === "History" && /* @__PURE__ */ jsx("div", { style: { marginTop: 20 }, children: /* @__PURE__ */ jsx(Card, { title: "Package and verification activity", note: "Recorded events for this revision.", children: review.activity?.length ? /* @__PURE__ */ jsx("ol", { className: "ap-history", children: review.activity.map((event, index) => /* @__PURE__ */ jsxs("li", { children: [
              /* @__PURE__ */ jsxs("div", { className: "ap-between", children: [
                /* @__PURE__ */ jsx("h3", { children: event.kind }),
                /* @__PURE__ */ jsx(Pill, { value: event.status })
              ] }),
              /* @__PURE__ */ jsx("small", { children: date(event.at) }),
              /* @__PURE__ */ jsx("p", { children: event.description })
            ] }, index)) }) : /* @__PURE__ */ jsx("p", { className: "ap-muted", children: "No upload or verification event is available for this revision." }) }) }),
            m.tab === "Publication" && /* @__PURE__ */ jsxs("div", { className: "ap-stack", children: [
              m.release?.holdReason && /* @__PURE__ */ jsx(Banner, { title: "Future publication is held", children: m.release.holdReason }),
              /* @__PURE__ */ jsxs(Card, { title: "Publication readiness", action: /* @__PURE__ */ jsx(Pill, { value: receipt.status }), children: [
                /* @__PURE__ */ jsx(Facts, { items: [["Approved revision", review.decisions.find((d) => d.isActive && d.decision === "approve") ? r2.edition + " \xB7 Revision " + r2.number : "No active approval"], ["Channel", receipt.channelKey], ["Canonical serial", m.release?.serial ?? "Baseline not initialised"], ["Readiness", reasonLabel(m.release?.blockingCode)]] }),
                /* @__PURE__ */ jsxs("div", { className: "ap-actions", style: { marginTop: 20 }, children: [
                  channel?.canPublish && receipt.status === "approved" && /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.open("publish"), disabled: !m.release || !!m.release.blockingCode, primary: true, icon: "menu-release", children: "Publish approved edition" }),
                  channel?.canWithdraw && /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.open(m.release?.holdReason ? "unhold" : "hold"), icon: "menu-policies", children: m.release?.holdReason ? "Review hold removal" : "Hold future publication" }),
                  /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.navigate("catalogue"), children: "Open Player catalogue" })
                ] })
              ] }),
              /* @__PURE__ */ jsx(Operations, { rows: ops, actions: a, Icon, canPublish: !!channel?.canPublish, canWithdraw: !!channel?.canWithdraw })
            ] })
          ] })
        ] }),
        catalogue && /* @__PURE__ */ jsxs(Fragment, { children: [
          /* @__PURE__ */ jsxs("div", { className: "ap-metrics", children: [
            /* @__PURE__ */ jsx(Metric, { label: "Canonical serial", value: m.catalogue?.channel.serial ?? channel?.serial ?? "\u2014", note: "Only committed snapshots are served" }),
            /* @__PURE__ */ jsx(Metric, { label: "Edition records", value: m.catalogue?.totalEditions ?? 0, note: "Including retained history" }),
            /* @__PURE__ */ jsx(Metric, { label: "Legacy mirror", value: statusLabel(channel?.mirrorStatus ?? "disabled"), note: channel?.mirroredSerial != null ? "Confirmed serial " + channel.mirroredSerial : "Optional compatibility delivery" }),
            /* @__PURE__ */ jsx(Metric, { label: "New publication", value: channel?.deliveryEnabled ? "Enabled" : "Paused", note: "Withdrawal remains separately authorised" })
          ] }),
          !channel?.initialised && /* @__PURE__ */ jsxs(Banner, { title: "Qualified baseline required", children: [
            "Preserve the authoritative Player catalogue before the first release. Your workspace owner must qualify its baseline and namespaces.",
            channel?.canPublish && channel.readinessCode === "baseline-required" && /* @__PURE__ */ jsx("div", { style: { marginTop: 12 }, children: /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.open("initialise"), primary: true, children: "Initialise qualified baseline" }) })
          ] }),
          channel?.mirrorStatus && ["held", "retryable", "pending"].includes(channel.mirrorStatus) && /* @__PURE__ */ jsxs(Banner, { title: "Legacy mirror needs attention", children: [
            reasonLabel(channel.mirrorCode),
            " Canonical publication is recorded separately.",
            channel.canPublish && /* @__PURE__ */ jsx("div", { style: { marginTop: 12 }, children: /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.open("mirror"), children: "Reconcile latest mirror" }) })
          ] }),
          !!m.catalogue?.approved?.length && /* @__PURE__ */ jsx(Card, { title: "Approved for release", note: "Up to 25 approved revisions in your publisher scope. Delivery is a separate confirmed action.", children: /* @__PURE__ */ jsx("div", { className: "ap-stack", children: m.catalogue.approved.map((item) => /* @__PURE__ */ jsxs("div", { className: "ap-between", children: [
            /* @__PURE__ */ jsxs("div", { children: [
              /* @__PURE__ */ jsx("strong", { children: item.metadata.title }),
              /* @__PURE__ */ jsxs("p", { className: "ap-muted ap-small", style: { margin: 0 }, children: [
                item.metadata.creatorDisplayName,
                " \xB7 ",
                item.metadata.edition,
                " \xB7 Revision ",
                item.metadata.number
              ] }),
              item.holdReason && /* @__PURE__ */ jsxs("p", { className: "ap-small", children: [
                "Held: ",
                item.holdReason
              ] })
            ] }),
            /* @__PURE__ */ jsxs("div", { className: "ap-actions", children: [
              item.holdReason && channel?.canWithdraw && /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.open("unhold", item), children: "Review hold removal" }),
              /* @__PURE__ */ jsx(Button, { Icon, onClick: () => a.open("publish", item), disabled: !!item.holdReason || !channel?.deliveryEnabled || m.busy, primary: true, children: "Review release" })
            ] })
          ] }, item.receiptId)) }) }),
          /* @__PURE__ */ jsxs("section", { className: "ap-card", children: [
            /* @__PURE__ */ jsxs("div", { className: "ap-card-head", children: [
              /* @__PURE__ */ jsxs("div", { children: [
                /* @__PURE__ */ jsx("h2", { children: "Player editions" }),
                /* @__PURE__ */ jsx("p", { children: "Public pins, review provenance and retained withdrawal history." })
              ] }),
              channel?.publicEnabled && channel.initialised && channel.publicCatalogueUrl?.startsWith("https://") && /* @__PURE__ */ jsx("a", { target: "_blank", rel: "noreferrer", href: channel.publicCatalogueUrl ?? void 0, onClick: (e) => {
                if (m.example) e.preventDefault();
              }, children: "Catalogue JSON \u2197" })
            ] }),
            /* @__PURE__ */ jsx("div", { className: "ap-table-wrap", children: /* @__PURE__ */ jsxs("table", { className: "ap-table", children: [
              /* @__PURE__ */ jsx("thead", { children: /* @__PURE__ */ jsxs("tr", { children: [
                /* @__PURE__ */ jsx("th", { children: "Edition" }),
                /* @__PURE__ */ jsx("th", { children: "Type" }),
                /* @__PURE__ */ jsx("th", { children: "Approval" }),
                /* @__PURE__ */ jsx("th", { children: "Immutable archive" }),
                /* @__PURE__ */ jsx("th", { children: "State" }),
                /* @__PURE__ */ jsx("th", { children: "Actions" })
              ] }) }),
              /* @__PURE__ */ jsx("tbody", { children: m.catalogue?.editions.map((e) => /* @__PURE__ */ jsxs("tr", { children: [
                /* @__PURE__ */ jsxs("td", { children: [
                  e.submissionId && channel?.canReview ? /* @__PURE__ */ jsx("button", { className: "ap-record-link", onClick: () => a.navigate("detail", e.submissionId), children: e.title }) : /* @__PURE__ */ jsx("strong", { children: e.title }),
                  /* @__PURE__ */ jsx("small", { children: e.worldId })
                ] }),
                /* @__PURE__ */ jsxs("td", { children: [
                  statusLabel(e.kind),
                  /* @__PURE__ */ jsx("small", { children: e.slot })
                ] }),
                /* @__PURE__ */ jsxs("td", { children: [
                  e.isLegacy ? "Qualified legacy baseline" : e.decisionId?.slice(0, 8) ?? "\u2014",
                  /* @__PURE__ */ jsx("small", { children: e.revisionId?.slice(0, 8) })
                ] }),
                /* @__PURE__ */ jsxs("td", { children: [
                  /* @__PURE__ */ jsx(Hash, { value: e.packageSha256 }),
                  /* @__PURE__ */ jsxs("details", { children: [
                    /* @__PURE__ */ jsxs("summary", { className: "ap-small ap-muted", children: [
                      "Immutable pin \xB7 Serial ",
                      e.publishedSerial
                    ] }),
                    /* @__PURE__ */ jsx("pre", { className: "ap-code", children: e.pinJson })
                  ] })
                ] }),
                /* @__PURE__ */ jsx("td", { children: /* @__PURE__ */ jsx(Pill, { value: e.isCurrent ? "published" : e.withdrawnAt ? "withdrawn" : "superseded" }) }),
                /* @__PURE__ */ jsx("td", { children: channel?.canWithdraw && e.isCurrent && /* @__PURE__ */ jsx(Button, { Icon, danger: true, onClick: () => a.open("withdraw", e), children: "Withdraw" }) })
              ] }, e.id)) })
            ] }) }),
            m.catalogue?.editions.length === 0 && /* @__PURE__ */ jsxs("div", { className: "ap-empty", children: [
              /* @__PURE__ */ jsx(Icon, { name: "menu-catalogue", size: 27 }),
              /* @__PURE__ */ jsx("h2", { children: "No editions have been published" }),
              /* @__PURE__ */ jsx("p", { children: "Approved submissions appear after their delivery operation commits." })
            ] }),
            /* @__PURE__ */ jsx(Pager, { page: m.page, size: m.catalogue?.pageSize ?? 25, total: m.catalogue?.totalEditions ?? 0, actions: a, Icon })
          ] }),
          /* @__PURE__ */ jsx("div", { style: { marginTop: 22 }, children: /* @__PURE__ */ jsx(Operations, { rows: ops, actions: a, Icon, canPublish: !!channel?.canPublish, canWithdraw: !!channel?.canWithdraw }) })
        ] })
      ] }),
      m.dialog && /* @__PURE__ */ jsx(DecisionDialog, { model: m, actions: a, Icon })
    ] });
  }
  function Metric({ label, value, note }) {
    return /* @__PURE__ */ jsxs("div", { className: "ap-metric", children: [
      /* @__PURE__ */ jsx("small", { children: label }),
      /* @__PURE__ */ jsx("strong", { style: { fontSize: typeof value === "string" && value.length > 14 ? 15 : void 0 }, children: value }),
      /* @__PURE__ */ jsx("span", { children: note })
    ] });
  }
  function Check({ Icon, title, text }) {
    return /* @__PURE__ */ jsxs("div", { className: "ap-check-row", children: [
      /* @__PURE__ */ jsx(Icon, { name: "check", size: 17 }),
      /* @__PURE__ */ jsxs("div", { children: [
        /* @__PURE__ */ jsx("strong", { children: title }),
        /* @__PURE__ */ jsx("p", { children: text })
      ] })
    ] });
  }
  function Operations({ rows, actions, Icon, canPublish, canWithdraw }) {
    return /* @__PURE__ */ jsxs("section", { className: "ap-card", children: [
      /* @__PURE__ */ jsx("div", { className: "ap-card-head", children: /* @__PURE__ */ jsxs("div", { children: [
        /* @__PURE__ */ jsx("h2", { children: "Publication activity" }),
        /* @__PURE__ */ jsx("p", { children: "Queued, completed and uncertain operations come from the service." })
      ] }) }),
      /* @__PURE__ */ jsx("div", { className: "ap-table-wrap", children: /* @__PURE__ */ jsxs("table", { className: "ap-table", children: [
        /* @__PURE__ */ jsx("thead", { children: /* @__PURE__ */ jsxs("tr", { children: [
          /* @__PURE__ */ jsx("th", { children: "Operation" }),
          /* @__PURE__ */ jsx("th", { children: "Requested" }),
          /* @__PURE__ */ jsx("th", { children: "Status" }),
          /* @__PURE__ */ jsx("th", { children: "Result" }),
          /* @__PURE__ */ jsx("th", { children: "Action" })
        ] }) }),
        /* @__PURE__ */ jsx("tbody", { children: rows.map((o) => /* @__PURE__ */ jsxs("tr", { children: [
          /* @__PURE__ */ jsxs("td", { children: [
            statusLabel(o.kind),
            /* @__PURE__ */ jsx("small", { children: o.id.slice(0, 8) })
          ] }),
          /* @__PURE__ */ jsxs("td", { children: [
            date(o.createdAt),
            /* @__PURE__ */ jsxs("small", { children: [
              o.attempts,
              " delivery attempt",
              o.attempts === 1 ? "" : "s"
            ] })
          ] }),
          /* @__PURE__ */ jsx("td", { children: /* @__PURE__ */ jsx(Pill, { value: o.status }) }),
          /* @__PURE__ */ jsxs("td", { children: [
            o.committedSerial != null ? "Committed serial " + o.committedSerial : reasonLabel(o.code),
            o.reason && /* @__PURE__ */ jsx("small", { children: o.reason })
          ] }),
          /* @__PURE__ */ jsx("td", { children: o.canRetry && (o.kind === "withdraw" ? canWithdraw : canPublish) && /* @__PURE__ */ jsx(Button, { Icon, onClick: () => actions.open("retry", o), children: "Review retry" }) })
        ] }, o.id)) })
      ] }) }),
      rows.length === 0 && /* @__PURE__ */ jsx("div", { className: "ap-empty", children: "No publication operation has been recorded." })
    ] });
  }
  var actionTitles = { approve: "Approve this exact revision", "request-changes": "Request a revised submission", reject: "Reject this revision", publish: "Publish the approved edition", withdraw: "Withdraw from discovery", retry: "Reconcile this operation", initialise: "Initialise the qualified baseline", mirror: "Reconcile the legacy mirror", hold: "Hold future publication", unhold: "Remove the publication hold" };
  function DecisionDialog({ model: m, actions: a, Icon }) {
    const d = m.dialog;
    const id = useId();
    const ref = useRef(null);
    const [v, set] = useState({ reason: "", creatorMessage: "", rights: false, attribution: false, audience: false, ack: false, replace: false });
    useEffect(() => {
      if (m.example) return;
      const dialog = ref.current;
      const before = document.activeElement;
      dialog?.showModal();
      dialog?.querySelector("textarea")?.focus();
      return () => {
        dialog?.close();
        if (before?.isConnected) before.focus();
      };
    }, [m.example]);
    const change = (key, value) => set((old) => ({ ...old, [key]: value }));
    const locked = m.busy || !!d.uncertain;
    const approval = d.kind === "approve";
    const editorial = ["approve", "request-changes", "reject"].includes(d.kind);
    const replacement = d.kind === "publish" && !!d.release?.currentSlotFingerprint;
    const allowed = !m.busy && !d.stale && v.reason.trim().length > 0 && (d.kind === "request-changes" || d.kind === "reject" ? v.creatorMessage.trim().length > 0 : true) && (approval ? v.rights && v.attribution && v.audience : v.ack) && (!replacement || v.replace);
    const fields = /* @__PURE__ */ jsxs(Fragment, { children: [
      /* @__PURE__ */ jsxs("div", { className: "ap-dialog-head", children: [
        /* @__PURE__ */ jsxs("div", { children: [
          /* @__PURE__ */ jsx("h2", { id: id + "-title", children: actionTitles[d.kind] }),
          /* @__PURE__ */ jsx("p", { children: d.title })
        ] }),
        /* @__PURE__ */ jsx("button", { type: "button", className: "ap-icon-button", "aria-label": "Close confirmation", disabled: m.busy, onClick: a.close, children: /* @__PURE__ */ jsx(Icon, { name: "close", size: 19 }) })
      ] }),
      /* @__PURE__ */ jsxs("div", { className: "ap-dialog-body", children: [
        /* @__PURE__ */ jsx("div", { className: "ap-dialog-facts", children: /* @__PURE__ */ jsx(Facts, { items: [["Edition", d.revision?.edition ?? d.edition?.worldId ?? d.channel?.key ?? d.operation?.channelKey], ["Revision", d.revision ? String(d.revision.number) : d.operation?.revisionId?.slice(0, 8) ?? "\u2014"], ["Exact digest", /* @__PURE__ */ jsx(Hash, { value: d.digest ?? d.edition?.packageSha256 ?? d.channel?.baselineSha256 })]] }) }),
        d.kind === "withdraw" && /* @__PURE__ */ jsxs(Banner, { title: "Existing copies may remain", children: [
          "This removes future discovery. Cached catalogue links, downloaded files and offline installations cannot be recalled.",
          d.edition?.kind === "part" && " Withdrawing a part removes its base edition and the part sequence together to avoid a broken sequence."
        ] }),
        d.kind === "publish" && /* @__PURE__ */ jsx(Banner, { title: "The service publishes the reviewed bytes", children: "The catalogue changes only after immutable assets pass public delivery checks. A queued request is not yet a published edition." }),
        d.kind === "retry" && /* @__PURE__ */ jsx(Banner, { title: "Reconcile the existing operation", children: "The same operation identity and retained assets are reused. Its recorded approval and current channel policy will be checked again." }),
        d.kind === "initialise" && /* @__PURE__ */ jsx(Banner, { title: "Preserve the authoritative catalogue", children: "Only the server-configured, qualified baseline and ownership records will be imported. This does not publish a new submission." }),
        d.kind === "hold" && /* @__PURE__ */ jsx(Banner, { title: "This blocks future publication", children: "An existing public entry stays listed until an authorised withdrawal commits." }),
        d.kind === "mirror" && /* @__PURE__ */ jsx(Banner, { title: "Reconcile the latest committed head", children: "The mirror cannot overwrite a higher serial. Conflicting remote changes need operator review." }),
        d.stale && /* @__PURE__ */ jsxs(Banner, { title: "The review changed", tone: "bad", children: [
          "Close this confirmation, reload and review the current revision before deciding.",
          /* @__PURE__ */ jsx("div", { style: { marginTop: 12 }, children: /* @__PURE__ */ jsx(Button, { Icon, onClick: () => {
            a.close();
            a.reload();
          }, children: "Reload current review" }) })
        ] }),
        m.dialogError && /* @__PURE__ */ jsx(Banner, { title: "The request was not confirmed", tone: "bad", children: m.dialogError }),
        /* @__PURE__ */ jsxs("label", { className: "ap-field", children: [
          /* @__PURE__ */ jsx("span", { children: editorial ? "Internal assessment" : "Reason" }),
          /* @__PURE__ */ jsx("textarea", { className: "ap-textarea", "aria-label": editorial ? "Internal assessment" : "Action reason", value: v.reason, maxLength: editorial ? 4e3 : 2e3, disabled: locked, onChange: (e) => change("reason", e.target.value), autoFocus: true }),
          /* @__PURE__ */ jsx("small", { children: editorial ? "Visible only to authorised staff." : "Recorded with this operation." })
        ] }),
        editorial && /* @__PURE__ */ jsxs("label", { className: "ap-field", children: [
          /* @__PURE__ */ jsxs("span", { children: [
            "Message to creator",
            d.kind === "approve" ? " (optional)" : ""
          ] }),
          /* @__PURE__ */ jsx("textarea", { className: "ap-textarea", "aria-label": "Message to creator", maxLength: 2e3, value: v.creatorMessage, disabled: locked, onChange: (e) => change("creatorMessage", e.target.value) }),
          /* @__PURE__ */ jsx("small", { children: "The creator receives this message rather than the internal assessment." })
        ] }),
        approval ? /* @__PURE__ */ jsx(Fragment, { children: [["rights", "I reviewed the distribution rights and evidence for every file."], ["attribution", "I reviewed the creator, artwork and voice attribution."], ["audience", "I reviewed the complete edition for its stated audience and age rating."]].map(([key, text]) => /* @__PURE__ */ jsxs("label", { className: "ap-checkbox", children: [
          /* @__PURE__ */ jsx("input", { type: "checkbox", checked: !!v[key], disabled: locked, onChange: (e) => change(key, e.target.checked) }),
          /* @__PURE__ */ jsx("span", { children: text })
        ] }, key)) }) : /* @__PURE__ */ jsxs("label", { className: "ap-checkbox", children: [
          /* @__PURE__ */ jsx("input", { type: "checkbox", checked: v.ack, disabled: locked, onChange: (e) => change("ack", e.target.checked) }),
          /* @__PURE__ */ jsx("span", { children: d.kind === "withdraw" ? "I understand the withdrawal scope and that existing copies may remain." : "I confirm this action for the displayed revision, channel or operation." })
        ] }),
        replacement && /* @__PURE__ */ jsxs("label", { className: "ap-checkbox", children: [
          /* @__PURE__ */ jsx("input", { type: "checkbox", checked: v.replace, disabled: locked, onChange: (e) => change("replace", e.target.checked) }),
          /* @__PURE__ */ jsx("span", { children: "I confirm replacing the current public package and its previous part sequence." })
        ] })
      ] }),
      /* @__PURE__ */ jsxs("div", { className: "ap-dialog-foot", children: [
        /* @__PURE__ */ jsx(Button, { Icon, disabled: m.busy, onClick: a.close, children: "Cancel" }),
        /* @__PURE__ */ jsx(Button, { Icon, primary: true, disabled: !allowed, onClick: () => a.confirm(v), children: m.busy ? "Recording\u2026" : d.uncertain ? "Retry same request" : d.kind === "approve" ? "Approve revision" : d.kind === "publish" ? "Queue publication" : d.kind === "withdraw" ? "Queue withdrawal" : d.kind === "retry" ? "Retry same operation" : "Confirm " + (d.kind === "request-changes" ? "request for changes" : d.kind === "reject" ? "rejection" : "action") })
      ] })
    ] });
    return m.example ? /* @__PURE__ */ jsx("div", { className: "ap-design-dialog-backdrop", children: /* @__PURE__ */ jsx("section", { className: "ap-dialog", role: "dialog", "aria-modal": "true", "aria-labelledby": id + "-title", children: fields }) }) : /* @__PURE__ */ jsx("dialog", { className: "ap-dialog", ref, "aria-labelledby": id + "-title", onCancel: (e) => {
      e.preventDefault();
      if (!m.busy) a.close();
    }, children: fields });
  }

  // src/Aonik.AdminUi/node_modules/clsx/dist/clsx.mjs
  function r(e) {
    var t, f, n = "";
    if ("string" == typeof e || "number" == typeof e) n += e;
    else if ("object" == typeof e) if (Array.isArray(e)) {
      var o = e.length;
      for (t = 0; t < o; t++) e[t] && (f = r(e[t])) && (n && (n += " "), n += f);
    } else for (f in e) e[f] && (n && (n += " "), n += f);
    return n;
  }
  function clsx() {
    for (var e, t, f = 0, n = "", o = arguments.length; f < o; f++) (e = arguments[f]) && (t = r(e)) && (n && (n += " "), n += t);
    return n;
  }

  // src/Aonik.AdminUi/node_modules/tailwind-merge/dist/bundle-mjs.mjs
  var concatArrays = (array1, array2) => {
    const combinedArray = new Array(array1.length + array2.length);
    for (let i = 0; i < array1.length; i++) {
      combinedArray[i] = array1[i];
    }
    for (let i = 0; i < array2.length; i++) {
      combinedArray[array1.length + i] = array2[i];
    }
    return combinedArray;
  };
  var createClassValidatorObject = (classGroupId, validator) => ({
    classGroupId,
    validator
  });
  var createClassPartObject = (nextPart = /* @__PURE__ */ new Map(), validators = null, classGroupId) => ({
    nextPart,
    validators,
    classGroupId
  });
  var CLASS_PART_SEPARATOR = "-";
  var EMPTY_CONFLICTS = [];
  var ARBITRARY_PROPERTY_PREFIX = "arbitrary..";
  var createClassGroupUtils = (config) => {
    const classMap = createClassMap(config);
    const {
      conflictingClassGroups,
      conflictingClassGroupModifiers
    } = config;
    const getClassGroupId = (className) => {
      if (className.startsWith("[") && className.endsWith("]")) {
        return getGroupIdForArbitraryProperty(className);
      }
      const classParts = className.split(CLASS_PART_SEPARATOR);
      const startIndex = classParts[0] === "" && classParts.length > 1 ? 1 : 0;
      return getGroupRecursive(classParts, startIndex, classMap);
    };
    const getConflictingClassGroupIds = (classGroupId, hasPostfixModifier) => {
      if (hasPostfixModifier) {
        const modifierConflicts = conflictingClassGroupModifiers[classGroupId];
        const baseConflicts = conflictingClassGroups[classGroupId];
        if (modifierConflicts) {
          if (baseConflicts) {
            return concatArrays(baseConflicts, modifierConflicts);
          }
          return modifierConflicts;
        }
        return baseConflicts || EMPTY_CONFLICTS;
      }
      return conflictingClassGroups[classGroupId] || EMPTY_CONFLICTS;
    };
    return {
      getClassGroupId,
      getConflictingClassGroupIds
    };
  };
  var getGroupRecursive = (classParts, startIndex, classPartObject) => {
    const classPathsLength = classParts.length - startIndex;
    if (classPathsLength === 0) {
      return classPartObject.classGroupId;
    }
    const currentClassPart = classParts[startIndex];
    const nextClassPartObject = classPartObject.nextPart.get(currentClassPart);
    if (nextClassPartObject) {
      const result = getGroupRecursive(classParts, startIndex + 1, nextClassPartObject);
      if (result) return result;
    }
    const validators = classPartObject.validators;
    if (validators === null) {
      return void 0;
    }
    const classRest = startIndex === 0 ? classParts.join(CLASS_PART_SEPARATOR) : classParts.slice(startIndex).join(CLASS_PART_SEPARATOR);
    const validatorsLength = validators.length;
    for (let i = 0; i < validatorsLength; i++) {
      const validatorObj = validators[i];
      if (validatorObj.validator(classRest)) {
        return validatorObj.classGroupId;
      }
    }
    return void 0;
  };
  var getGroupIdForArbitraryProperty = (className) => className.slice(1, -1).indexOf(":") === -1 ? void 0 : (() => {
    const content = className.slice(1, -1);
    const colonIndex = content.indexOf(":");
    const property = content.slice(0, colonIndex);
    return property ? ARBITRARY_PROPERTY_PREFIX + property : void 0;
  })();
  var createClassMap = (config) => {
    const {
      theme,
      classGroups
    } = config;
    return processClassGroups(classGroups, theme);
  };
  var processClassGroups = (classGroups, theme) => {
    const classMap = createClassPartObject();
    for (const classGroupId in classGroups) {
      const group = classGroups[classGroupId];
      processClassesRecursively(group, classMap, classGroupId, theme);
    }
    return classMap;
  };
  var processClassesRecursively = (classGroup, classPartObject, classGroupId, theme) => {
    const len = classGroup.length;
    for (let i = 0; i < len; i++) {
      const classDefinition = classGroup[i];
      processClassDefinition(classDefinition, classPartObject, classGroupId, theme);
    }
  };
  var processClassDefinition = (classDefinition, classPartObject, classGroupId, theme) => {
    if (typeof classDefinition === "string") {
      processStringDefinition(classDefinition, classPartObject, classGroupId);
      return;
    }
    if (typeof classDefinition === "function") {
      processFunctionDefinition(classDefinition, classPartObject, classGroupId, theme);
      return;
    }
    processObjectDefinition(classDefinition, classPartObject, classGroupId, theme);
  };
  var processStringDefinition = (classDefinition, classPartObject, classGroupId) => {
    const classPartObjectToEdit = classDefinition === "" ? classPartObject : getPart(classPartObject, classDefinition);
    classPartObjectToEdit.classGroupId = classGroupId;
  };
  var processFunctionDefinition = (classDefinition, classPartObject, classGroupId, theme) => {
    if (isThemeGetter(classDefinition)) {
      processClassesRecursively(classDefinition(theme), classPartObject, classGroupId, theme);
      return;
    }
    if (classPartObject.validators === null) {
      classPartObject.validators = [];
    }
    classPartObject.validators.push(createClassValidatorObject(classGroupId, classDefinition));
  };
  var processObjectDefinition = (classDefinition, classPartObject, classGroupId, theme) => {
    const entries = Object.entries(classDefinition);
    const len = entries.length;
    for (let i = 0; i < len; i++) {
      const [key, value] = entries[i];
      processClassesRecursively(value, getPart(classPartObject, key), classGroupId, theme);
    }
  };
  var getPart = (classPartObject, path) => {
    let current = classPartObject;
    const parts = path.split(CLASS_PART_SEPARATOR);
    const len = parts.length;
    for (let i = 0; i < len; i++) {
      const part = parts[i];
      let next = current.nextPart.get(part);
      if (!next) {
        next = createClassPartObject();
        current.nextPart.set(part, next);
      }
      current = next;
    }
    return current;
  };
  var isThemeGetter = (func) => "isThemeGetter" in func && func.isThemeGetter === true;
  var createLruCache = (maxCacheSize) => {
    if (maxCacheSize < 1) {
      return {
        get: () => void 0,
        set: () => {
        }
      };
    }
    let cacheSize = 0;
    let cache = /* @__PURE__ */ Object.create(null);
    let previousCache = /* @__PURE__ */ Object.create(null);
    const update = (key, value) => {
      cache[key] = value;
      cacheSize++;
      if (cacheSize > maxCacheSize) {
        cacheSize = 0;
        previousCache = cache;
        cache = /* @__PURE__ */ Object.create(null);
      }
    };
    return {
      get(key) {
        let value = cache[key];
        if (value !== void 0) {
          return value;
        }
        if ((value = previousCache[key]) !== void 0) {
          update(key, value);
          return value;
        }
      },
      set(key, value) {
        if (key in cache) {
          cache[key] = value;
        } else {
          update(key, value);
        }
      }
    };
  };
  var IMPORTANT_MODIFIER = "!";
  var MODIFIER_SEPARATOR = ":";
  var EMPTY_MODIFIERS = [];
  var createResultObject = (modifiers, hasImportantModifier, baseClassName, maybePostfixModifierPosition, isExternal) => ({
    modifiers,
    hasImportantModifier,
    baseClassName,
    maybePostfixModifierPosition,
    isExternal
  });
  var createParseClassName = (config) => {
    const {
      prefix,
      experimentalParseClassName
    } = config;
    let parseClassName = (className) => {
      const modifiers = [];
      let bracketDepth = 0;
      let parenDepth = 0;
      let modifierStart = 0;
      let postfixModifierPosition;
      const len = className.length;
      for (let index = 0; index < len; index++) {
        const currentCharacter = className[index];
        if (bracketDepth === 0 && parenDepth === 0) {
          if (currentCharacter === MODIFIER_SEPARATOR) {
            modifiers.push(className.slice(modifierStart, index));
            modifierStart = index + 1;
            continue;
          }
          if (currentCharacter === "/") {
            postfixModifierPosition = index;
            continue;
          }
        }
        if (currentCharacter === "[") bracketDepth++;
        else if (currentCharacter === "]") bracketDepth--;
        else if (currentCharacter === "(") parenDepth++;
        else if (currentCharacter === ")") parenDepth--;
      }
      const baseClassNameWithImportantModifier = modifiers.length === 0 ? className : className.slice(modifierStart);
      let baseClassName = baseClassNameWithImportantModifier;
      let hasImportantModifier = false;
      if (baseClassNameWithImportantModifier.endsWith(IMPORTANT_MODIFIER)) {
        baseClassName = baseClassNameWithImportantModifier.slice(0, -1);
        hasImportantModifier = true;
      } else if (
        /**
         * In Tailwind CSS v3 the important modifier was at the start of the base class name. This is still supported for legacy reasons.
         * @see https://github.com/dcastil/tailwind-merge/issues/513#issuecomment-2614029864
         */
        baseClassNameWithImportantModifier.startsWith(IMPORTANT_MODIFIER)
      ) {
        baseClassName = baseClassNameWithImportantModifier.slice(1);
        hasImportantModifier = true;
      }
      const maybePostfixModifierPosition = postfixModifierPosition && postfixModifierPosition > modifierStart ? postfixModifierPosition - modifierStart : void 0;
      return createResultObject(modifiers, hasImportantModifier, baseClassName, maybePostfixModifierPosition);
    };
    if (prefix) {
      const fullPrefix = prefix + MODIFIER_SEPARATOR;
      const parseClassNameOriginal = parseClassName;
      parseClassName = (className) => className.startsWith(fullPrefix) ? parseClassNameOriginal(className.slice(fullPrefix.length)) : createResultObject(EMPTY_MODIFIERS, false, className, void 0, true);
    }
    if (experimentalParseClassName) {
      const parseClassNameOriginal = parseClassName;
      parseClassName = (className) => experimentalParseClassName({
        className,
        parseClassName: parseClassNameOriginal
      });
    }
    return parseClassName;
  };
  var createSortModifiers = (config) => {
    const modifierWeights = /* @__PURE__ */ new Map();
    config.orderSensitiveModifiers.forEach((mod, index) => {
      modifierWeights.set(mod, 1e6 + index);
    });
    return (modifiers) => {
      const result = [];
      let currentSegment = [];
      for (let i = 0; i < modifiers.length; i++) {
        const modifier = modifiers[i];
        const isArbitrary = modifier[0] === "[";
        const isOrderSensitive = modifierWeights.has(modifier);
        if (isArbitrary || isOrderSensitive) {
          if (currentSegment.length > 0) {
            currentSegment.sort();
            result.push(...currentSegment);
            currentSegment = [];
          }
          result.push(modifier);
        } else {
          currentSegment.push(modifier);
        }
      }
      if (currentSegment.length > 0) {
        currentSegment.sort();
        result.push(...currentSegment);
      }
      return result;
    };
  };
  var createConfigUtils = (config) => ({
    cache: createLruCache(config.cacheSize),
    parseClassName: createParseClassName(config),
    sortModifiers: createSortModifiers(config),
    ...createClassGroupUtils(config)
  });
  var SPLIT_CLASSES_REGEX = /\s+/;
  var mergeClassList = (classList, configUtils) => {
    const {
      parseClassName,
      getClassGroupId,
      getConflictingClassGroupIds,
      sortModifiers
    } = configUtils;
    const classGroupsInConflict = [];
    const classNames = classList.trim().split(SPLIT_CLASSES_REGEX);
    let result = "";
    for (let index = classNames.length - 1; index >= 0; index -= 1) {
      const originalClassName = classNames[index];
      const {
        isExternal,
        modifiers,
        hasImportantModifier,
        baseClassName,
        maybePostfixModifierPosition
      } = parseClassName(originalClassName);
      if (isExternal) {
        result = originalClassName + (result.length > 0 ? " " + result : result);
        continue;
      }
      let hasPostfixModifier = !!maybePostfixModifierPosition;
      let classGroupId = getClassGroupId(hasPostfixModifier ? baseClassName.substring(0, maybePostfixModifierPosition) : baseClassName);
      if (!classGroupId) {
        if (!hasPostfixModifier) {
          result = originalClassName + (result.length > 0 ? " " + result : result);
          continue;
        }
        classGroupId = getClassGroupId(baseClassName);
        if (!classGroupId) {
          result = originalClassName + (result.length > 0 ? " " + result : result);
          continue;
        }
        hasPostfixModifier = false;
      }
      const variantModifier = modifiers.length === 0 ? "" : modifiers.length === 1 ? modifiers[0] : sortModifiers(modifiers).join(":");
      const modifierId = hasImportantModifier ? variantModifier + IMPORTANT_MODIFIER : variantModifier;
      const classId = modifierId + classGroupId;
      if (classGroupsInConflict.indexOf(classId) > -1) {
        continue;
      }
      classGroupsInConflict.push(classId);
      const conflictGroups = getConflictingClassGroupIds(classGroupId, hasPostfixModifier);
      for (let i = 0; i < conflictGroups.length; ++i) {
        const group = conflictGroups[i];
        classGroupsInConflict.push(modifierId + group);
      }
      result = originalClassName + (result.length > 0 ? " " + result : result);
    }
    return result;
  };
  var twJoin = (...classLists) => {
    let index = 0;
    let argument;
    let resolvedValue;
    let string = "";
    while (index < classLists.length) {
      if (argument = classLists[index++]) {
        if (resolvedValue = toValue(argument)) {
          string && (string += " ");
          string += resolvedValue;
        }
      }
    }
    return string;
  };
  var toValue = (mix) => {
    if (typeof mix === "string") {
      return mix;
    }
    let resolvedValue;
    let string = "";
    for (let k = 0; k < mix.length; k++) {
      if (mix[k]) {
        if (resolvedValue = toValue(mix[k])) {
          string && (string += " ");
          string += resolvedValue;
        }
      }
    }
    return string;
  };
  var createTailwindMerge = (createConfigFirst, ...createConfigRest) => {
    let configUtils;
    let cacheGet;
    let cacheSet;
    let functionToCall;
    const initTailwindMerge = (classList) => {
      const config = createConfigRest.reduce((previousConfig, createConfigCurrent) => createConfigCurrent(previousConfig), createConfigFirst());
      configUtils = createConfigUtils(config);
      cacheGet = configUtils.cache.get;
      cacheSet = configUtils.cache.set;
      functionToCall = tailwindMerge;
      return tailwindMerge(classList);
    };
    const tailwindMerge = (classList) => {
      const cachedResult = cacheGet(classList);
      if (cachedResult) {
        return cachedResult;
      }
      const result = mergeClassList(classList, configUtils);
      cacheSet(classList, result);
      return result;
    };
    functionToCall = initTailwindMerge;
    return (...args) => functionToCall(twJoin(...args));
  };
  var fallbackThemeArr = [];
  var fromTheme = (key) => {
    const themeGetter = (theme) => theme[key] || fallbackThemeArr;
    themeGetter.isThemeGetter = true;
    return themeGetter;
  };
  var arbitraryValueRegex = /^\[(?:(\w[\w-]*):)?(.+)\]$/i;
  var arbitraryVariableRegex = /^\((?:(\w[\w-]*):)?(.+)\)$/i;
  var fractionRegex = /^\d+\/\d+$/;
  var tshirtUnitRegex = /^(\d+(\.\d+)?)?(xs|sm|md|lg|xl)$/;
  var lengthUnitRegex = /\d+(%|px|r?em|[sdl]?v([hwib]|min|max)|pt|pc|in|cm|mm|cap|ch|ex|r?lh|cq(w|h|i|b|min|max))|\b(calc|min|max|clamp)\(.+\)|^0$/;
  var colorFunctionRegex = /^(rgba?|hsla?|hwb|(ok)?(lab|lch)|color-mix)\(.+\)$/;
  var shadowRegex = /^(inset_)?-?((\d+)?\.?(\d+)[a-z]+|0)_-?((\d+)?\.?(\d+)[a-z]+|0)/;
  var imageRegex = /^(url|image|image-set|cross-fade|element|(repeating-)?(linear|radial|conic)-gradient)\(.+\)$/;
  var isFraction = (value) => fractionRegex.test(value);
  var isNumber = (value) => !!value && !Number.isNaN(Number(value));
  var isInteger = (value) => !!value && Number.isInteger(Number(value));
  var isPercent = (value) => value.endsWith("%") && isNumber(value.slice(0, -1));
  var isTshirtSize = (value) => tshirtUnitRegex.test(value);
  var isAny = () => true;
  var isLengthOnly = (value) => (
    // `colorFunctionRegex` check is necessary because color functions can have percentages in them which which would be incorrectly classified as lengths.
    // For example, `hsl(0 0% 0%)` would be classified as a length without this check.
    // I could also use lookbehind assertion in `lengthUnitRegex` but that isn't supported widely enough.
    lengthUnitRegex.test(value) && !colorFunctionRegex.test(value)
  );
  var isNever = () => false;
  var isShadow = (value) => shadowRegex.test(value);
  var isImage = (value) => imageRegex.test(value);
  var isAnyNonArbitrary = (value) => !isArbitraryValue(value) && !isArbitraryVariable(value);
  var isArbitrarySize = (value) => getIsArbitraryValue(value, isLabelSize, isNever);
  var isArbitraryValue = (value) => arbitraryValueRegex.test(value);
  var isArbitraryLength = (value) => getIsArbitraryValue(value, isLabelLength, isLengthOnly);
  var isArbitraryNumber = (value) => getIsArbitraryValue(value, isLabelNumber, isNumber);
  var isArbitraryPosition = (value) => getIsArbitraryValue(value, isLabelPosition, isNever);
  var isArbitraryImage = (value) => getIsArbitraryValue(value, isLabelImage, isImage);
  var isArbitraryShadow = (value) => getIsArbitraryValue(value, isLabelShadow, isShadow);
  var isArbitraryVariable = (value) => arbitraryVariableRegex.test(value);
  var isArbitraryVariableLength = (value) => getIsArbitraryVariable(value, isLabelLength);
  var isArbitraryVariableFamilyName = (value) => getIsArbitraryVariable(value, isLabelFamilyName);
  var isArbitraryVariablePosition = (value) => getIsArbitraryVariable(value, isLabelPosition);
  var isArbitraryVariableSize = (value) => getIsArbitraryVariable(value, isLabelSize);
  var isArbitraryVariableImage = (value) => getIsArbitraryVariable(value, isLabelImage);
  var isArbitraryVariableShadow = (value) => getIsArbitraryVariable(value, isLabelShadow, true);
  var getIsArbitraryValue = (value, testLabel, testValue) => {
    const result = arbitraryValueRegex.exec(value);
    if (result) {
      if (result[1]) {
        return testLabel(result[1]);
      }
      return testValue(result[2]);
    }
    return false;
  };
  var getIsArbitraryVariable = (value, testLabel, shouldMatchNoLabel = false) => {
    const result = arbitraryVariableRegex.exec(value);
    if (result) {
      if (result[1]) {
        return testLabel(result[1]);
      }
      return shouldMatchNoLabel;
    }
    return false;
  };
  var isLabelPosition = (label) => label === "position" || label === "percentage";
  var isLabelImage = (label) => label === "image" || label === "url";
  var isLabelSize = (label) => label === "length" || label === "size" || label === "bg-size";
  var isLabelLength = (label) => label === "length";
  var isLabelNumber = (label) => label === "number";
  var isLabelFamilyName = (label) => label === "family-name";
  var isLabelShadow = (label) => label === "shadow";
  var getDefaultConfig = () => {
    const themeColor = fromTheme("color");
    const themeFont = fromTheme("font");
    const themeText = fromTheme("text");
    const themeFontWeight = fromTheme("font-weight");
    const themeTracking = fromTheme("tracking");
    const themeLeading = fromTheme("leading");
    const themeBreakpoint = fromTheme("breakpoint");
    const themeContainer = fromTheme("container");
    const themeSpacing = fromTheme("spacing");
    const themeRadius = fromTheme("radius");
    const themeShadow = fromTheme("shadow");
    const themeInsetShadow = fromTheme("inset-shadow");
    const themeTextShadow = fromTheme("text-shadow");
    const themeDropShadow = fromTheme("drop-shadow");
    const themeBlur = fromTheme("blur");
    const themePerspective = fromTheme("perspective");
    const themeAspect = fromTheme("aspect");
    const themeEase = fromTheme("ease");
    const themeAnimate = fromTheme("animate");
    const scaleBreak = () => ["auto", "avoid", "all", "avoid-page", "page", "left", "right", "column"];
    const scalePosition = () => [
      "center",
      "top",
      "bottom",
      "left",
      "right",
      "top-left",
      // Deprecated since Tailwind CSS v4.1.0, see https://github.com/tailwindlabs/tailwindcss/pull/17378
      "left-top",
      "top-right",
      // Deprecated since Tailwind CSS v4.1.0, see https://github.com/tailwindlabs/tailwindcss/pull/17378
      "right-top",
      "bottom-right",
      // Deprecated since Tailwind CSS v4.1.0, see https://github.com/tailwindlabs/tailwindcss/pull/17378
      "right-bottom",
      "bottom-left",
      // Deprecated since Tailwind CSS v4.1.0, see https://github.com/tailwindlabs/tailwindcss/pull/17378
      "left-bottom"
    ];
    const scalePositionWithArbitrary = () => [...scalePosition(), isArbitraryVariable, isArbitraryValue];
    const scaleOverflow = () => ["auto", "hidden", "clip", "visible", "scroll"];
    const scaleOverscroll = () => ["auto", "contain", "none"];
    const scaleUnambiguousSpacing = () => [isArbitraryVariable, isArbitraryValue, themeSpacing];
    const scaleInset = () => [isFraction, "full", "auto", ...scaleUnambiguousSpacing()];
    const scaleGridTemplateColsRows = () => [isInteger, "none", "subgrid", isArbitraryVariable, isArbitraryValue];
    const scaleGridColRowStartAndEnd = () => ["auto", {
      span: ["full", isInteger, isArbitraryVariable, isArbitraryValue]
    }, isInteger, isArbitraryVariable, isArbitraryValue];
    const scaleGridColRowStartOrEnd = () => [isInteger, "auto", isArbitraryVariable, isArbitraryValue];
    const scaleGridAutoColsRows = () => ["auto", "min", "max", "fr", isArbitraryVariable, isArbitraryValue];
    const scaleAlignPrimaryAxis = () => ["start", "end", "center", "between", "around", "evenly", "stretch", "baseline", "center-safe", "end-safe"];
    const scaleAlignSecondaryAxis = () => ["start", "end", "center", "stretch", "center-safe", "end-safe"];
    const scaleMargin = () => ["auto", ...scaleUnambiguousSpacing()];
    const scaleSizing = () => [isFraction, "auto", "full", "dvw", "dvh", "lvw", "lvh", "svw", "svh", "min", "max", "fit", ...scaleUnambiguousSpacing()];
    const scaleColor = () => [themeColor, isArbitraryVariable, isArbitraryValue];
    const scaleBgPosition = () => [...scalePosition(), isArbitraryVariablePosition, isArbitraryPosition, {
      position: [isArbitraryVariable, isArbitraryValue]
    }];
    const scaleBgRepeat = () => ["no-repeat", {
      repeat: ["", "x", "y", "space", "round"]
    }];
    const scaleBgSize = () => ["auto", "cover", "contain", isArbitraryVariableSize, isArbitrarySize, {
      size: [isArbitraryVariable, isArbitraryValue]
    }];
    const scaleGradientStopPosition = () => [isPercent, isArbitraryVariableLength, isArbitraryLength];
    const scaleRadius = () => [
      // Deprecated since Tailwind CSS v4.0.0
      "",
      "none",
      "full",
      themeRadius,
      isArbitraryVariable,
      isArbitraryValue
    ];
    const scaleBorderWidth = () => ["", isNumber, isArbitraryVariableLength, isArbitraryLength];
    const scaleLineStyle = () => ["solid", "dashed", "dotted", "double"];
    const scaleBlendMode = () => ["normal", "multiply", "screen", "overlay", "darken", "lighten", "color-dodge", "color-burn", "hard-light", "soft-light", "difference", "exclusion", "hue", "saturation", "color", "luminosity"];
    const scaleMaskImagePosition = () => [isNumber, isPercent, isArbitraryVariablePosition, isArbitraryPosition];
    const scaleBlur = () => [
      // Deprecated since Tailwind CSS v4.0.0
      "",
      "none",
      themeBlur,
      isArbitraryVariable,
      isArbitraryValue
    ];
    const scaleRotate = () => ["none", isNumber, isArbitraryVariable, isArbitraryValue];
    const scaleScale = () => ["none", isNumber, isArbitraryVariable, isArbitraryValue];
    const scaleSkew = () => [isNumber, isArbitraryVariable, isArbitraryValue];
    const scaleTranslate = () => [isFraction, "full", ...scaleUnambiguousSpacing()];
    return {
      cacheSize: 500,
      theme: {
        animate: ["spin", "ping", "pulse", "bounce"],
        aspect: ["video"],
        blur: [isTshirtSize],
        breakpoint: [isTshirtSize],
        color: [isAny],
        container: [isTshirtSize],
        "drop-shadow": [isTshirtSize],
        ease: ["in", "out", "in-out"],
        font: [isAnyNonArbitrary],
        "font-weight": ["thin", "extralight", "light", "normal", "medium", "semibold", "bold", "extrabold", "black"],
        "inset-shadow": [isTshirtSize],
        leading: ["none", "tight", "snug", "normal", "relaxed", "loose"],
        perspective: ["dramatic", "near", "normal", "midrange", "distant", "none"],
        radius: [isTshirtSize],
        shadow: [isTshirtSize],
        spacing: ["px", isNumber],
        text: [isTshirtSize],
        "text-shadow": [isTshirtSize],
        tracking: ["tighter", "tight", "normal", "wide", "wider", "widest"]
      },
      classGroups: {
        // --------------
        // --- Layout ---
        // --------------
        /**
         * Aspect Ratio
         * @see https://tailwindcss.com/docs/aspect-ratio
         */
        aspect: [{
          aspect: ["auto", "square", isFraction, isArbitraryValue, isArbitraryVariable, themeAspect]
        }],
        /**
         * Container
         * @see https://tailwindcss.com/docs/container
         * @deprecated since Tailwind CSS v4.0.0
         */
        container: ["container"],
        /**
         * Columns
         * @see https://tailwindcss.com/docs/columns
         */
        columns: [{
          columns: [isNumber, isArbitraryValue, isArbitraryVariable, themeContainer]
        }],
        /**
         * Break After
         * @see https://tailwindcss.com/docs/break-after
         */
        "break-after": [{
          "break-after": scaleBreak()
        }],
        /**
         * Break Before
         * @see https://tailwindcss.com/docs/break-before
         */
        "break-before": [{
          "break-before": scaleBreak()
        }],
        /**
         * Break Inside
         * @see https://tailwindcss.com/docs/break-inside
         */
        "break-inside": [{
          "break-inside": ["auto", "avoid", "avoid-page", "avoid-column"]
        }],
        /**
         * Box Decoration Break
         * @see https://tailwindcss.com/docs/box-decoration-break
         */
        "box-decoration": [{
          "box-decoration": ["slice", "clone"]
        }],
        /**
         * Box Sizing
         * @see https://tailwindcss.com/docs/box-sizing
         */
        box: [{
          box: ["border", "content"]
        }],
        /**
         * Display
         * @see https://tailwindcss.com/docs/display
         */
        display: ["block", "inline-block", "inline", "flex", "inline-flex", "table", "inline-table", "table-caption", "table-cell", "table-column", "table-column-group", "table-footer-group", "table-header-group", "table-row-group", "table-row", "flow-root", "grid", "inline-grid", "contents", "list-item", "hidden"],
        /**
         * Screen Reader Only
         * @see https://tailwindcss.com/docs/display#screen-reader-only
         */
        sr: ["sr-only", "not-sr-only"],
        /**
         * Floats
         * @see https://tailwindcss.com/docs/float
         */
        float: [{
          float: ["right", "left", "none", "start", "end"]
        }],
        /**
         * Clear
         * @see https://tailwindcss.com/docs/clear
         */
        clear: [{
          clear: ["left", "right", "both", "none", "start", "end"]
        }],
        /**
         * Isolation
         * @see https://tailwindcss.com/docs/isolation
         */
        isolation: ["isolate", "isolation-auto"],
        /**
         * Object Fit
         * @see https://tailwindcss.com/docs/object-fit
         */
        "object-fit": [{
          object: ["contain", "cover", "fill", "none", "scale-down"]
        }],
        /**
         * Object Position
         * @see https://tailwindcss.com/docs/object-position
         */
        "object-position": [{
          object: scalePositionWithArbitrary()
        }],
        /**
         * Overflow
         * @see https://tailwindcss.com/docs/overflow
         */
        overflow: [{
          overflow: scaleOverflow()
        }],
        /**
         * Overflow X
         * @see https://tailwindcss.com/docs/overflow
         */
        "overflow-x": [{
          "overflow-x": scaleOverflow()
        }],
        /**
         * Overflow Y
         * @see https://tailwindcss.com/docs/overflow
         */
        "overflow-y": [{
          "overflow-y": scaleOverflow()
        }],
        /**
         * Overscroll Behavior
         * @see https://tailwindcss.com/docs/overscroll-behavior
         */
        overscroll: [{
          overscroll: scaleOverscroll()
        }],
        /**
         * Overscroll Behavior X
         * @see https://tailwindcss.com/docs/overscroll-behavior
         */
        "overscroll-x": [{
          "overscroll-x": scaleOverscroll()
        }],
        /**
         * Overscroll Behavior Y
         * @see https://tailwindcss.com/docs/overscroll-behavior
         */
        "overscroll-y": [{
          "overscroll-y": scaleOverscroll()
        }],
        /**
         * Position
         * @see https://tailwindcss.com/docs/position
         */
        position: ["static", "fixed", "absolute", "relative", "sticky"],
        /**
         * Top / Right / Bottom / Left
         * @see https://tailwindcss.com/docs/top-right-bottom-left
         */
        inset: [{
          inset: scaleInset()
        }],
        /**
         * Right / Left
         * @see https://tailwindcss.com/docs/top-right-bottom-left
         */
        "inset-x": [{
          "inset-x": scaleInset()
        }],
        /**
         * Top / Bottom
         * @see https://tailwindcss.com/docs/top-right-bottom-left
         */
        "inset-y": [{
          "inset-y": scaleInset()
        }],
        /**
         * Start
         * @see https://tailwindcss.com/docs/top-right-bottom-left
         */
        start: [{
          start: scaleInset()
        }],
        /**
         * End
         * @see https://tailwindcss.com/docs/top-right-bottom-left
         */
        end: [{
          end: scaleInset()
        }],
        /**
         * Top
         * @see https://tailwindcss.com/docs/top-right-bottom-left
         */
        top: [{
          top: scaleInset()
        }],
        /**
         * Right
         * @see https://tailwindcss.com/docs/top-right-bottom-left
         */
        right: [{
          right: scaleInset()
        }],
        /**
         * Bottom
         * @see https://tailwindcss.com/docs/top-right-bottom-left
         */
        bottom: [{
          bottom: scaleInset()
        }],
        /**
         * Left
         * @see https://tailwindcss.com/docs/top-right-bottom-left
         */
        left: [{
          left: scaleInset()
        }],
        /**
         * Visibility
         * @see https://tailwindcss.com/docs/visibility
         */
        visibility: ["visible", "invisible", "collapse"],
        /**
         * Z-Index
         * @see https://tailwindcss.com/docs/z-index
         */
        z: [{
          z: [isInteger, "auto", isArbitraryVariable, isArbitraryValue]
        }],
        // ------------------------
        // --- Flexbox and Grid ---
        // ------------------------
        /**
         * Flex Basis
         * @see https://tailwindcss.com/docs/flex-basis
         */
        basis: [{
          basis: [isFraction, "full", "auto", themeContainer, ...scaleUnambiguousSpacing()]
        }],
        /**
         * Flex Direction
         * @see https://tailwindcss.com/docs/flex-direction
         */
        "flex-direction": [{
          flex: ["row", "row-reverse", "col", "col-reverse"]
        }],
        /**
         * Flex Wrap
         * @see https://tailwindcss.com/docs/flex-wrap
         */
        "flex-wrap": [{
          flex: ["nowrap", "wrap", "wrap-reverse"]
        }],
        /**
         * Flex
         * @see https://tailwindcss.com/docs/flex
         */
        flex: [{
          flex: [isNumber, isFraction, "auto", "initial", "none", isArbitraryValue]
        }],
        /**
         * Flex Grow
         * @see https://tailwindcss.com/docs/flex-grow
         */
        grow: [{
          grow: ["", isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Flex Shrink
         * @see https://tailwindcss.com/docs/flex-shrink
         */
        shrink: [{
          shrink: ["", isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Order
         * @see https://tailwindcss.com/docs/order
         */
        order: [{
          order: [isInteger, "first", "last", "none", isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Grid Template Columns
         * @see https://tailwindcss.com/docs/grid-template-columns
         */
        "grid-cols": [{
          "grid-cols": scaleGridTemplateColsRows()
        }],
        /**
         * Grid Column Start / End
         * @see https://tailwindcss.com/docs/grid-column
         */
        "col-start-end": [{
          col: scaleGridColRowStartAndEnd()
        }],
        /**
         * Grid Column Start
         * @see https://tailwindcss.com/docs/grid-column
         */
        "col-start": [{
          "col-start": scaleGridColRowStartOrEnd()
        }],
        /**
         * Grid Column End
         * @see https://tailwindcss.com/docs/grid-column
         */
        "col-end": [{
          "col-end": scaleGridColRowStartOrEnd()
        }],
        /**
         * Grid Template Rows
         * @see https://tailwindcss.com/docs/grid-template-rows
         */
        "grid-rows": [{
          "grid-rows": scaleGridTemplateColsRows()
        }],
        /**
         * Grid Row Start / End
         * @see https://tailwindcss.com/docs/grid-row
         */
        "row-start-end": [{
          row: scaleGridColRowStartAndEnd()
        }],
        /**
         * Grid Row Start
         * @see https://tailwindcss.com/docs/grid-row
         */
        "row-start": [{
          "row-start": scaleGridColRowStartOrEnd()
        }],
        /**
         * Grid Row End
         * @see https://tailwindcss.com/docs/grid-row
         */
        "row-end": [{
          "row-end": scaleGridColRowStartOrEnd()
        }],
        /**
         * Grid Auto Flow
         * @see https://tailwindcss.com/docs/grid-auto-flow
         */
        "grid-flow": [{
          "grid-flow": ["row", "col", "dense", "row-dense", "col-dense"]
        }],
        /**
         * Grid Auto Columns
         * @see https://tailwindcss.com/docs/grid-auto-columns
         */
        "auto-cols": [{
          "auto-cols": scaleGridAutoColsRows()
        }],
        /**
         * Grid Auto Rows
         * @see https://tailwindcss.com/docs/grid-auto-rows
         */
        "auto-rows": [{
          "auto-rows": scaleGridAutoColsRows()
        }],
        /**
         * Gap
         * @see https://tailwindcss.com/docs/gap
         */
        gap: [{
          gap: scaleUnambiguousSpacing()
        }],
        /**
         * Gap X
         * @see https://tailwindcss.com/docs/gap
         */
        "gap-x": [{
          "gap-x": scaleUnambiguousSpacing()
        }],
        /**
         * Gap Y
         * @see https://tailwindcss.com/docs/gap
         */
        "gap-y": [{
          "gap-y": scaleUnambiguousSpacing()
        }],
        /**
         * Justify Content
         * @see https://tailwindcss.com/docs/justify-content
         */
        "justify-content": [{
          justify: [...scaleAlignPrimaryAxis(), "normal"]
        }],
        /**
         * Justify Items
         * @see https://tailwindcss.com/docs/justify-items
         */
        "justify-items": [{
          "justify-items": [...scaleAlignSecondaryAxis(), "normal"]
        }],
        /**
         * Justify Self
         * @see https://tailwindcss.com/docs/justify-self
         */
        "justify-self": [{
          "justify-self": ["auto", ...scaleAlignSecondaryAxis()]
        }],
        /**
         * Align Content
         * @see https://tailwindcss.com/docs/align-content
         */
        "align-content": [{
          content: ["normal", ...scaleAlignPrimaryAxis()]
        }],
        /**
         * Align Items
         * @see https://tailwindcss.com/docs/align-items
         */
        "align-items": [{
          items: [...scaleAlignSecondaryAxis(), {
            baseline: ["", "last"]
          }]
        }],
        /**
         * Align Self
         * @see https://tailwindcss.com/docs/align-self
         */
        "align-self": [{
          self: ["auto", ...scaleAlignSecondaryAxis(), {
            baseline: ["", "last"]
          }]
        }],
        /**
         * Place Content
         * @see https://tailwindcss.com/docs/place-content
         */
        "place-content": [{
          "place-content": scaleAlignPrimaryAxis()
        }],
        /**
         * Place Items
         * @see https://tailwindcss.com/docs/place-items
         */
        "place-items": [{
          "place-items": [...scaleAlignSecondaryAxis(), "baseline"]
        }],
        /**
         * Place Self
         * @see https://tailwindcss.com/docs/place-self
         */
        "place-self": [{
          "place-self": ["auto", ...scaleAlignSecondaryAxis()]
        }],
        // Spacing
        /**
         * Padding
         * @see https://tailwindcss.com/docs/padding
         */
        p: [{
          p: scaleUnambiguousSpacing()
        }],
        /**
         * Padding X
         * @see https://tailwindcss.com/docs/padding
         */
        px: [{
          px: scaleUnambiguousSpacing()
        }],
        /**
         * Padding Y
         * @see https://tailwindcss.com/docs/padding
         */
        py: [{
          py: scaleUnambiguousSpacing()
        }],
        /**
         * Padding Start
         * @see https://tailwindcss.com/docs/padding
         */
        ps: [{
          ps: scaleUnambiguousSpacing()
        }],
        /**
         * Padding End
         * @see https://tailwindcss.com/docs/padding
         */
        pe: [{
          pe: scaleUnambiguousSpacing()
        }],
        /**
         * Padding Top
         * @see https://tailwindcss.com/docs/padding
         */
        pt: [{
          pt: scaleUnambiguousSpacing()
        }],
        /**
         * Padding Right
         * @see https://tailwindcss.com/docs/padding
         */
        pr: [{
          pr: scaleUnambiguousSpacing()
        }],
        /**
         * Padding Bottom
         * @see https://tailwindcss.com/docs/padding
         */
        pb: [{
          pb: scaleUnambiguousSpacing()
        }],
        /**
         * Padding Left
         * @see https://tailwindcss.com/docs/padding
         */
        pl: [{
          pl: scaleUnambiguousSpacing()
        }],
        /**
         * Margin
         * @see https://tailwindcss.com/docs/margin
         */
        m: [{
          m: scaleMargin()
        }],
        /**
         * Margin X
         * @see https://tailwindcss.com/docs/margin
         */
        mx: [{
          mx: scaleMargin()
        }],
        /**
         * Margin Y
         * @see https://tailwindcss.com/docs/margin
         */
        my: [{
          my: scaleMargin()
        }],
        /**
         * Margin Start
         * @see https://tailwindcss.com/docs/margin
         */
        ms: [{
          ms: scaleMargin()
        }],
        /**
         * Margin End
         * @see https://tailwindcss.com/docs/margin
         */
        me: [{
          me: scaleMargin()
        }],
        /**
         * Margin Top
         * @see https://tailwindcss.com/docs/margin
         */
        mt: [{
          mt: scaleMargin()
        }],
        /**
         * Margin Right
         * @see https://tailwindcss.com/docs/margin
         */
        mr: [{
          mr: scaleMargin()
        }],
        /**
         * Margin Bottom
         * @see https://tailwindcss.com/docs/margin
         */
        mb: [{
          mb: scaleMargin()
        }],
        /**
         * Margin Left
         * @see https://tailwindcss.com/docs/margin
         */
        ml: [{
          ml: scaleMargin()
        }],
        /**
         * Space Between X
         * @see https://tailwindcss.com/docs/margin#adding-space-between-children
         */
        "space-x": [{
          "space-x": scaleUnambiguousSpacing()
        }],
        /**
         * Space Between X Reverse
         * @see https://tailwindcss.com/docs/margin#adding-space-between-children
         */
        "space-x-reverse": ["space-x-reverse"],
        /**
         * Space Between Y
         * @see https://tailwindcss.com/docs/margin#adding-space-between-children
         */
        "space-y": [{
          "space-y": scaleUnambiguousSpacing()
        }],
        /**
         * Space Between Y Reverse
         * @see https://tailwindcss.com/docs/margin#adding-space-between-children
         */
        "space-y-reverse": ["space-y-reverse"],
        // --------------
        // --- Sizing ---
        // --------------
        /**
         * Size
         * @see https://tailwindcss.com/docs/width#setting-both-width-and-height
         */
        size: [{
          size: scaleSizing()
        }],
        /**
         * Width
         * @see https://tailwindcss.com/docs/width
         */
        w: [{
          w: [themeContainer, "screen", ...scaleSizing()]
        }],
        /**
         * Min-Width
         * @see https://tailwindcss.com/docs/min-width
         */
        "min-w": [{
          "min-w": [
            themeContainer,
            "screen",
            /** Deprecated. @see https://github.com/tailwindlabs/tailwindcss.com/issues/2027#issuecomment-2620152757 */
            "none",
            ...scaleSizing()
          ]
        }],
        /**
         * Max-Width
         * @see https://tailwindcss.com/docs/max-width
         */
        "max-w": [{
          "max-w": [
            themeContainer,
            "screen",
            "none",
            /** Deprecated since Tailwind CSS v4.0.0. @see https://github.com/tailwindlabs/tailwindcss.com/issues/2027#issuecomment-2620152757 */
            "prose",
            /** Deprecated since Tailwind CSS v4.0.0. @see https://github.com/tailwindlabs/tailwindcss.com/issues/2027#issuecomment-2620152757 */
            {
              screen: [themeBreakpoint]
            },
            ...scaleSizing()
          ]
        }],
        /**
         * Height
         * @see https://tailwindcss.com/docs/height
         */
        h: [{
          h: ["screen", "lh", ...scaleSizing()]
        }],
        /**
         * Min-Height
         * @see https://tailwindcss.com/docs/min-height
         */
        "min-h": [{
          "min-h": ["screen", "lh", "none", ...scaleSizing()]
        }],
        /**
         * Max-Height
         * @see https://tailwindcss.com/docs/max-height
         */
        "max-h": [{
          "max-h": ["screen", "lh", ...scaleSizing()]
        }],
        // ------------------
        // --- Typography ---
        // ------------------
        /**
         * Font Size
         * @see https://tailwindcss.com/docs/font-size
         */
        "font-size": [{
          text: ["base", themeText, isArbitraryVariableLength, isArbitraryLength]
        }],
        /**
         * Font Smoothing
         * @see https://tailwindcss.com/docs/font-smoothing
         */
        "font-smoothing": ["antialiased", "subpixel-antialiased"],
        /**
         * Font Style
         * @see https://tailwindcss.com/docs/font-style
         */
        "font-style": ["italic", "not-italic"],
        /**
         * Font Weight
         * @see https://tailwindcss.com/docs/font-weight
         */
        "font-weight": [{
          font: [themeFontWeight, isArbitraryVariable, isArbitraryNumber]
        }],
        /**
         * Font Stretch
         * @see https://tailwindcss.com/docs/font-stretch
         */
        "font-stretch": [{
          "font-stretch": ["ultra-condensed", "extra-condensed", "condensed", "semi-condensed", "normal", "semi-expanded", "expanded", "extra-expanded", "ultra-expanded", isPercent, isArbitraryValue]
        }],
        /**
         * Font Family
         * @see https://tailwindcss.com/docs/font-family
         */
        "font-family": [{
          font: [isArbitraryVariableFamilyName, isArbitraryValue, themeFont]
        }],
        /**
         * Font Variant Numeric
         * @see https://tailwindcss.com/docs/font-variant-numeric
         */
        "fvn-normal": ["normal-nums"],
        /**
         * Font Variant Numeric
         * @see https://tailwindcss.com/docs/font-variant-numeric
         */
        "fvn-ordinal": ["ordinal"],
        /**
         * Font Variant Numeric
         * @see https://tailwindcss.com/docs/font-variant-numeric
         */
        "fvn-slashed-zero": ["slashed-zero"],
        /**
         * Font Variant Numeric
         * @see https://tailwindcss.com/docs/font-variant-numeric
         */
        "fvn-figure": ["lining-nums", "oldstyle-nums"],
        /**
         * Font Variant Numeric
         * @see https://tailwindcss.com/docs/font-variant-numeric
         */
        "fvn-spacing": ["proportional-nums", "tabular-nums"],
        /**
         * Font Variant Numeric
         * @see https://tailwindcss.com/docs/font-variant-numeric
         */
        "fvn-fraction": ["diagonal-fractions", "stacked-fractions"],
        /**
         * Letter Spacing
         * @see https://tailwindcss.com/docs/letter-spacing
         */
        tracking: [{
          tracking: [themeTracking, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Line Clamp
         * @see https://tailwindcss.com/docs/line-clamp
         */
        "line-clamp": [{
          "line-clamp": [isNumber, "none", isArbitraryVariable, isArbitraryNumber]
        }],
        /**
         * Line Height
         * @see https://tailwindcss.com/docs/line-height
         */
        leading: [{
          leading: [
            /** Deprecated since Tailwind CSS v4.0.0. @see https://github.com/tailwindlabs/tailwindcss.com/issues/2027#issuecomment-2620152757 */
            themeLeading,
            ...scaleUnambiguousSpacing()
          ]
        }],
        /**
         * List Style Image
         * @see https://tailwindcss.com/docs/list-style-image
         */
        "list-image": [{
          "list-image": ["none", isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * List Style Position
         * @see https://tailwindcss.com/docs/list-style-position
         */
        "list-style-position": [{
          list: ["inside", "outside"]
        }],
        /**
         * List Style Type
         * @see https://tailwindcss.com/docs/list-style-type
         */
        "list-style-type": [{
          list: ["disc", "decimal", "none", isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Text Alignment
         * @see https://tailwindcss.com/docs/text-align
         */
        "text-alignment": [{
          text: ["left", "center", "right", "justify", "start", "end"]
        }],
        /**
         * Placeholder Color
         * @deprecated since Tailwind CSS v3.0.0
         * @see https://v3.tailwindcss.com/docs/placeholder-color
         */
        "placeholder-color": [{
          placeholder: scaleColor()
        }],
        /**
         * Text Color
         * @see https://tailwindcss.com/docs/text-color
         */
        "text-color": [{
          text: scaleColor()
        }],
        /**
         * Text Decoration
         * @see https://tailwindcss.com/docs/text-decoration
         */
        "text-decoration": ["underline", "overline", "line-through", "no-underline"],
        /**
         * Text Decoration Style
         * @see https://tailwindcss.com/docs/text-decoration-style
         */
        "text-decoration-style": [{
          decoration: [...scaleLineStyle(), "wavy"]
        }],
        /**
         * Text Decoration Thickness
         * @see https://tailwindcss.com/docs/text-decoration-thickness
         */
        "text-decoration-thickness": [{
          decoration: [isNumber, "from-font", "auto", isArbitraryVariable, isArbitraryLength]
        }],
        /**
         * Text Decoration Color
         * @see https://tailwindcss.com/docs/text-decoration-color
         */
        "text-decoration-color": [{
          decoration: scaleColor()
        }],
        /**
         * Text Underline Offset
         * @see https://tailwindcss.com/docs/text-underline-offset
         */
        "underline-offset": [{
          "underline-offset": [isNumber, "auto", isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Text Transform
         * @see https://tailwindcss.com/docs/text-transform
         */
        "text-transform": ["uppercase", "lowercase", "capitalize", "normal-case"],
        /**
         * Text Overflow
         * @see https://tailwindcss.com/docs/text-overflow
         */
        "text-overflow": ["truncate", "text-ellipsis", "text-clip"],
        /**
         * Text Wrap
         * @see https://tailwindcss.com/docs/text-wrap
         */
        "text-wrap": [{
          text: ["wrap", "nowrap", "balance", "pretty"]
        }],
        /**
         * Text Indent
         * @see https://tailwindcss.com/docs/text-indent
         */
        indent: [{
          indent: scaleUnambiguousSpacing()
        }],
        /**
         * Vertical Alignment
         * @see https://tailwindcss.com/docs/vertical-align
         */
        "vertical-align": [{
          align: ["baseline", "top", "middle", "bottom", "text-top", "text-bottom", "sub", "super", isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Whitespace
         * @see https://tailwindcss.com/docs/whitespace
         */
        whitespace: [{
          whitespace: ["normal", "nowrap", "pre", "pre-line", "pre-wrap", "break-spaces"]
        }],
        /**
         * Word Break
         * @see https://tailwindcss.com/docs/word-break
         */
        break: [{
          break: ["normal", "words", "all", "keep"]
        }],
        /**
         * Overflow Wrap
         * @see https://tailwindcss.com/docs/overflow-wrap
         */
        wrap: [{
          wrap: ["break-word", "anywhere", "normal"]
        }],
        /**
         * Hyphens
         * @see https://tailwindcss.com/docs/hyphens
         */
        hyphens: [{
          hyphens: ["none", "manual", "auto"]
        }],
        /**
         * Content
         * @see https://tailwindcss.com/docs/content
         */
        content: [{
          content: ["none", isArbitraryVariable, isArbitraryValue]
        }],
        // -------------------
        // --- Backgrounds ---
        // -------------------
        /**
         * Background Attachment
         * @see https://tailwindcss.com/docs/background-attachment
         */
        "bg-attachment": [{
          bg: ["fixed", "local", "scroll"]
        }],
        /**
         * Background Clip
         * @see https://tailwindcss.com/docs/background-clip
         */
        "bg-clip": [{
          "bg-clip": ["border", "padding", "content", "text"]
        }],
        /**
         * Background Origin
         * @see https://tailwindcss.com/docs/background-origin
         */
        "bg-origin": [{
          "bg-origin": ["border", "padding", "content"]
        }],
        /**
         * Background Position
         * @see https://tailwindcss.com/docs/background-position
         */
        "bg-position": [{
          bg: scaleBgPosition()
        }],
        /**
         * Background Repeat
         * @see https://tailwindcss.com/docs/background-repeat
         */
        "bg-repeat": [{
          bg: scaleBgRepeat()
        }],
        /**
         * Background Size
         * @see https://tailwindcss.com/docs/background-size
         */
        "bg-size": [{
          bg: scaleBgSize()
        }],
        /**
         * Background Image
         * @see https://tailwindcss.com/docs/background-image
         */
        "bg-image": [{
          bg: ["none", {
            linear: [{
              to: ["t", "tr", "r", "br", "b", "bl", "l", "tl"]
            }, isInteger, isArbitraryVariable, isArbitraryValue],
            radial: ["", isArbitraryVariable, isArbitraryValue],
            conic: [isInteger, isArbitraryVariable, isArbitraryValue]
          }, isArbitraryVariableImage, isArbitraryImage]
        }],
        /**
         * Background Color
         * @see https://tailwindcss.com/docs/background-color
         */
        "bg-color": [{
          bg: scaleColor()
        }],
        /**
         * Gradient Color Stops From Position
         * @see https://tailwindcss.com/docs/gradient-color-stops
         */
        "gradient-from-pos": [{
          from: scaleGradientStopPosition()
        }],
        /**
         * Gradient Color Stops Via Position
         * @see https://tailwindcss.com/docs/gradient-color-stops
         */
        "gradient-via-pos": [{
          via: scaleGradientStopPosition()
        }],
        /**
         * Gradient Color Stops To Position
         * @see https://tailwindcss.com/docs/gradient-color-stops
         */
        "gradient-to-pos": [{
          to: scaleGradientStopPosition()
        }],
        /**
         * Gradient Color Stops From
         * @see https://tailwindcss.com/docs/gradient-color-stops
         */
        "gradient-from": [{
          from: scaleColor()
        }],
        /**
         * Gradient Color Stops Via
         * @see https://tailwindcss.com/docs/gradient-color-stops
         */
        "gradient-via": [{
          via: scaleColor()
        }],
        /**
         * Gradient Color Stops To
         * @see https://tailwindcss.com/docs/gradient-color-stops
         */
        "gradient-to": [{
          to: scaleColor()
        }],
        // ---------------
        // --- Borders ---
        // ---------------
        /**
         * Border Radius
         * @see https://tailwindcss.com/docs/border-radius
         */
        rounded: [{
          rounded: scaleRadius()
        }],
        /**
         * Border Radius Start
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-s": [{
          "rounded-s": scaleRadius()
        }],
        /**
         * Border Radius End
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-e": [{
          "rounded-e": scaleRadius()
        }],
        /**
         * Border Radius Top
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-t": [{
          "rounded-t": scaleRadius()
        }],
        /**
         * Border Radius Right
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-r": [{
          "rounded-r": scaleRadius()
        }],
        /**
         * Border Radius Bottom
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-b": [{
          "rounded-b": scaleRadius()
        }],
        /**
         * Border Radius Left
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-l": [{
          "rounded-l": scaleRadius()
        }],
        /**
         * Border Radius Start Start
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-ss": [{
          "rounded-ss": scaleRadius()
        }],
        /**
         * Border Radius Start End
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-se": [{
          "rounded-se": scaleRadius()
        }],
        /**
         * Border Radius End End
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-ee": [{
          "rounded-ee": scaleRadius()
        }],
        /**
         * Border Radius End Start
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-es": [{
          "rounded-es": scaleRadius()
        }],
        /**
         * Border Radius Top Left
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-tl": [{
          "rounded-tl": scaleRadius()
        }],
        /**
         * Border Radius Top Right
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-tr": [{
          "rounded-tr": scaleRadius()
        }],
        /**
         * Border Radius Bottom Right
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-br": [{
          "rounded-br": scaleRadius()
        }],
        /**
         * Border Radius Bottom Left
         * @see https://tailwindcss.com/docs/border-radius
         */
        "rounded-bl": [{
          "rounded-bl": scaleRadius()
        }],
        /**
         * Border Width
         * @see https://tailwindcss.com/docs/border-width
         */
        "border-w": [{
          border: scaleBorderWidth()
        }],
        /**
         * Border Width X
         * @see https://tailwindcss.com/docs/border-width
         */
        "border-w-x": [{
          "border-x": scaleBorderWidth()
        }],
        /**
         * Border Width Y
         * @see https://tailwindcss.com/docs/border-width
         */
        "border-w-y": [{
          "border-y": scaleBorderWidth()
        }],
        /**
         * Border Width Start
         * @see https://tailwindcss.com/docs/border-width
         */
        "border-w-s": [{
          "border-s": scaleBorderWidth()
        }],
        /**
         * Border Width End
         * @see https://tailwindcss.com/docs/border-width
         */
        "border-w-e": [{
          "border-e": scaleBorderWidth()
        }],
        /**
         * Border Width Top
         * @see https://tailwindcss.com/docs/border-width
         */
        "border-w-t": [{
          "border-t": scaleBorderWidth()
        }],
        /**
         * Border Width Right
         * @see https://tailwindcss.com/docs/border-width
         */
        "border-w-r": [{
          "border-r": scaleBorderWidth()
        }],
        /**
         * Border Width Bottom
         * @see https://tailwindcss.com/docs/border-width
         */
        "border-w-b": [{
          "border-b": scaleBorderWidth()
        }],
        /**
         * Border Width Left
         * @see https://tailwindcss.com/docs/border-width
         */
        "border-w-l": [{
          "border-l": scaleBorderWidth()
        }],
        /**
         * Divide Width X
         * @see https://tailwindcss.com/docs/border-width#between-children
         */
        "divide-x": [{
          "divide-x": scaleBorderWidth()
        }],
        /**
         * Divide Width X Reverse
         * @see https://tailwindcss.com/docs/border-width#between-children
         */
        "divide-x-reverse": ["divide-x-reverse"],
        /**
         * Divide Width Y
         * @see https://tailwindcss.com/docs/border-width#between-children
         */
        "divide-y": [{
          "divide-y": scaleBorderWidth()
        }],
        /**
         * Divide Width Y Reverse
         * @see https://tailwindcss.com/docs/border-width#between-children
         */
        "divide-y-reverse": ["divide-y-reverse"],
        /**
         * Border Style
         * @see https://tailwindcss.com/docs/border-style
         */
        "border-style": [{
          border: [...scaleLineStyle(), "hidden", "none"]
        }],
        /**
         * Divide Style
         * @see https://tailwindcss.com/docs/border-style#setting-the-divider-style
         */
        "divide-style": [{
          divide: [...scaleLineStyle(), "hidden", "none"]
        }],
        /**
         * Border Color
         * @see https://tailwindcss.com/docs/border-color
         */
        "border-color": [{
          border: scaleColor()
        }],
        /**
         * Border Color X
         * @see https://tailwindcss.com/docs/border-color
         */
        "border-color-x": [{
          "border-x": scaleColor()
        }],
        /**
         * Border Color Y
         * @see https://tailwindcss.com/docs/border-color
         */
        "border-color-y": [{
          "border-y": scaleColor()
        }],
        /**
         * Border Color S
         * @see https://tailwindcss.com/docs/border-color
         */
        "border-color-s": [{
          "border-s": scaleColor()
        }],
        /**
         * Border Color E
         * @see https://tailwindcss.com/docs/border-color
         */
        "border-color-e": [{
          "border-e": scaleColor()
        }],
        /**
         * Border Color Top
         * @see https://tailwindcss.com/docs/border-color
         */
        "border-color-t": [{
          "border-t": scaleColor()
        }],
        /**
         * Border Color Right
         * @see https://tailwindcss.com/docs/border-color
         */
        "border-color-r": [{
          "border-r": scaleColor()
        }],
        /**
         * Border Color Bottom
         * @see https://tailwindcss.com/docs/border-color
         */
        "border-color-b": [{
          "border-b": scaleColor()
        }],
        /**
         * Border Color Left
         * @see https://tailwindcss.com/docs/border-color
         */
        "border-color-l": [{
          "border-l": scaleColor()
        }],
        /**
         * Divide Color
         * @see https://tailwindcss.com/docs/divide-color
         */
        "divide-color": [{
          divide: scaleColor()
        }],
        /**
         * Outline Style
         * @see https://tailwindcss.com/docs/outline-style
         */
        "outline-style": [{
          outline: [...scaleLineStyle(), "none", "hidden"]
        }],
        /**
         * Outline Offset
         * @see https://tailwindcss.com/docs/outline-offset
         */
        "outline-offset": [{
          "outline-offset": [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Outline Width
         * @see https://tailwindcss.com/docs/outline-width
         */
        "outline-w": [{
          outline: ["", isNumber, isArbitraryVariableLength, isArbitraryLength]
        }],
        /**
         * Outline Color
         * @see https://tailwindcss.com/docs/outline-color
         */
        "outline-color": [{
          outline: scaleColor()
        }],
        // ---------------
        // --- Effects ---
        // ---------------
        /**
         * Box Shadow
         * @see https://tailwindcss.com/docs/box-shadow
         */
        shadow: [{
          shadow: [
            // Deprecated since Tailwind CSS v4.0.0
            "",
            "none",
            themeShadow,
            isArbitraryVariableShadow,
            isArbitraryShadow
          ]
        }],
        /**
         * Box Shadow Color
         * @see https://tailwindcss.com/docs/box-shadow#setting-the-shadow-color
         */
        "shadow-color": [{
          shadow: scaleColor()
        }],
        /**
         * Inset Box Shadow
         * @see https://tailwindcss.com/docs/box-shadow#adding-an-inset-shadow
         */
        "inset-shadow": [{
          "inset-shadow": ["none", themeInsetShadow, isArbitraryVariableShadow, isArbitraryShadow]
        }],
        /**
         * Inset Box Shadow Color
         * @see https://tailwindcss.com/docs/box-shadow#setting-the-inset-shadow-color
         */
        "inset-shadow-color": [{
          "inset-shadow": scaleColor()
        }],
        /**
         * Ring Width
         * @see https://tailwindcss.com/docs/box-shadow#adding-a-ring
         */
        "ring-w": [{
          ring: scaleBorderWidth()
        }],
        /**
         * Ring Width Inset
         * @see https://v3.tailwindcss.com/docs/ring-width#inset-rings
         * @deprecated since Tailwind CSS v4.0.0
         * @see https://github.com/tailwindlabs/tailwindcss/blob/v4.0.0/packages/tailwindcss/src/utilities.ts#L4158
         */
        "ring-w-inset": ["ring-inset"],
        /**
         * Ring Color
         * @see https://tailwindcss.com/docs/box-shadow#setting-the-ring-color
         */
        "ring-color": [{
          ring: scaleColor()
        }],
        /**
         * Ring Offset Width
         * @see https://v3.tailwindcss.com/docs/ring-offset-width
         * @deprecated since Tailwind CSS v4.0.0
         * @see https://github.com/tailwindlabs/tailwindcss/blob/v4.0.0/packages/tailwindcss/src/utilities.ts#L4158
         */
        "ring-offset-w": [{
          "ring-offset": [isNumber, isArbitraryLength]
        }],
        /**
         * Ring Offset Color
         * @see https://v3.tailwindcss.com/docs/ring-offset-color
         * @deprecated since Tailwind CSS v4.0.0
         * @see https://github.com/tailwindlabs/tailwindcss/blob/v4.0.0/packages/tailwindcss/src/utilities.ts#L4158
         */
        "ring-offset-color": [{
          "ring-offset": scaleColor()
        }],
        /**
         * Inset Ring Width
         * @see https://tailwindcss.com/docs/box-shadow#adding-an-inset-ring
         */
        "inset-ring-w": [{
          "inset-ring": scaleBorderWidth()
        }],
        /**
         * Inset Ring Color
         * @see https://tailwindcss.com/docs/box-shadow#setting-the-inset-ring-color
         */
        "inset-ring-color": [{
          "inset-ring": scaleColor()
        }],
        /**
         * Text Shadow
         * @see https://tailwindcss.com/docs/text-shadow
         */
        "text-shadow": [{
          "text-shadow": ["none", themeTextShadow, isArbitraryVariableShadow, isArbitraryShadow]
        }],
        /**
         * Text Shadow Color
         * @see https://tailwindcss.com/docs/text-shadow#setting-the-shadow-color
         */
        "text-shadow-color": [{
          "text-shadow": scaleColor()
        }],
        /**
         * Opacity
         * @see https://tailwindcss.com/docs/opacity
         */
        opacity: [{
          opacity: [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Mix Blend Mode
         * @see https://tailwindcss.com/docs/mix-blend-mode
         */
        "mix-blend": [{
          "mix-blend": [...scaleBlendMode(), "plus-darker", "plus-lighter"]
        }],
        /**
         * Background Blend Mode
         * @see https://tailwindcss.com/docs/background-blend-mode
         */
        "bg-blend": [{
          "bg-blend": scaleBlendMode()
        }],
        /**
         * Mask Clip
         * @see https://tailwindcss.com/docs/mask-clip
         */
        "mask-clip": [{
          "mask-clip": ["border", "padding", "content", "fill", "stroke", "view"]
        }, "mask-no-clip"],
        /**
         * Mask Composite
         * @see https://tailwindcss.com/docs/mask-composite
         */
        "mask-composite": [{
          mask: ["add", "subtract", "intersect", "exclude"]
        }],
        /**
         * Mask Image
         * @see https://tailwindcss.com/docs/mask-image
         */
        "mask-image-linear-pos": [{
          "mask-linear": [isNumber]
        }],
        "mask-image-linear-from-pos": [{
          "mask-linear-from": scaleMaskImagePosition()
        }],
        "mask-image-linear-to-pos": [{
          "mask-linear-to": scaleMaskImagePosition()
        }],
        "mask-image-linear-from-color": [{
          "mask-linear-from": scaleColor()
        }],
        "mask-image-linear-to-color": [{
          "mask-linear-to": scaleColor()
        }],
        "mask-image-t-from-pos": [{
          "mask-t-from": scaleMaskImagePosition()
        }],
        "mask-image-t-to-pos": [{
          "mask-t-to": scaleMaskImagePosition()
        }],
        "mask-image-t-from-color": [{
          "mask-t-from": scaleColor()
        }],
        "mask-image-t-to-color": [{
          "mask-t-to": scaleColor()
        }],
        "mask-image-r-from-pos": [{
          "mask-r-from": scaleMaskImagePosition()
        }],
        "mask-image-r-to-pos": [{
          "mask-r-to": scaleMaskImagePosition()
        }],
        "mask-image-r-from-color": [{
          "mask-r-from": scaleColor()
        }],
        "mask-image-r-to-color": [{
          "mask-r-to": scaleColor()
        }],
        "mask-image-b-from-pos": [{
          "mask-b-from": scaleMaskImagePosition()
        }],
        "mask-image-b-to-pos": [{
          "mask-b-to": scaleMaskImagePosition()
        }],
        "mask-image-b-from-color": [{
          "mask-b-from": scaleColor()
        }],
        "mask-image-b-to-color": [{
          "mask-b-to": scaleColor()
        }],
        "mask-image-l-from-pos": [{
          "mask-l-from": scaleMaskImagePosition()
        }],
        "mask-image-l-to-pos": [{
          "mask-l-to": scaleMaskImagePosition()
        }],
        "mask-image-l-from-color": [{
          "mask-l-from": scaleColor()
        }],
        "mask-image-l-to-color": [{
          "mask-l-to": scaleColor()
        }],
        "mask-image-x-from-pos": [{
          "mask-x-from": scaleMaskImagePosition()
        }],
        "mask-image-x-to-pos": [{
          "mask-x-to": scaleMaskImagePosition()
        }],
        "mask-image-x-from-color": [{
          "mask-x-from": scaleColor()
        }],
        "mask-image-x-to-color": [{
          "mask-x-to": scaleColor()
        }],
        "mask-image-y-from-pos": [{
          "mask-y-from": scaleMaskImagePosition()
        }],
        "mask-image-y-to-pos": [{
          "mask-y-to": scaleMaskImagePosition()
        }],
        "mask-image-y-from-color": [{
          "mask-y-from": scaleColor()
        }],
        "mask-image-y-to-color": [{
          "mask-y-to": scaleColor()
        }],
        "mask-image-radial": [{
          "mask-radial": [isArbitraryVariable, isArbitraryValue]
        }],
        "mask-image-radial-from-pos": [{
          "mask-radial-from": scaleMaskImagePosition()
        }],
        "mask-image-radial-to-pos": [{
          "mask-radial-to": scaleMaskImagePosition()
        }],
        "mask-image-radial-from-color": [{
          "mask-radial-from": scaleColor()
        }],
        "mask-image-radial-to-color": [{
          "mask-radial-to": scaleColor()
        }],
        "mask-image-radial-shape": [{
          "mask-radial": ["circle", "ellipse"]
        }],
        "mask-image-radial-size": [{
          "mask-radial": [{
            closest: ["side", "corner"],
            farthest: ["side", "corner"]
          }]
        }],
        "mask-image-radial-pos": [{
          "mask-radial-at": scalePosition()
        }],
        "mask-image-conic-pos": [{
          "mask-conic": [isNumber]
        }],
        "mask-image-conic-from-pos": [{
          "mask-conic-from": scaleMaskImagePosition()
        }],
        "mask-image-conic-to-pos": [{
          "mask-conic-to": scaleMaskImagePosition()
        }],
        "mask-image-conic-from-color": [{
          "mask-conic-from": scaleColor()
        }],
        "mask-image-conic-to-color": [{
          "mask-conic-to": scaleColor()
        }],
        /**
         * Mask Mode
         * @see https://tailwindcss.com/docs/mask-mode
         */
        "mask-mode": [{
          mask: ["alpha", "luminance", "match"]
        }],
        /**
         * Mask Origin
         * @see https://tailwindcss.com/docs/mask-origin
         */
        "mask-origin": [{
          "mask-origin": ["border", "padding", "content", "fill", "stroke", "view"]
        }],
        /**
         * Mask Position
         * @see https://tailwindcss.com/docs/mask-position
         */
        "mask-position": [{
          mask: scaleBgPosition()
        }],
        /**
         * Mask Repeat
         * @see https://tailwindcss.com/docs/mask-repeat
         */
        "mask-repeat": [{
          mask: scaleBgRepeat()
        }],
        /**
         * Mask Size
         * @see https://tailwindcss.com/docs/mask-size
         */
        "mask-size": [{
          mask: scaleBgSize()
        }],
        /**
         * Mask Type
         * @see https://tailwindcss.com/docs/mask-type
         */
        "mask-type": [{
          "mask-type": ["alpha", "luminance"]
        }],
        /**
         * Mask Image
         * @see https://tailwindcss.com/docs/mask-image
         */
        "mask-image": [{
          mask: ["none", isArbitraryVariable, isArbitraryValue]
        }],
        // ---------------
        // --- Filters ---
        // ---------------
        /**
         * Filter
         * @see https://tailwindcss.com/docs/filter
         */
        filter: [{
          filter: [
            // Deprecated since Tailwind CSS v3.0.0
            "",
            "none",
            isArbitraryVariable,
            isArbitraryValue
          ]
        }],
        /**
         * Blur
         * @see https://tailwindcss.com/docs/blur
         */
        blur: [{
          blur: scaleBlur()
        }],
        /**
         * Brightness
         * @see https://tailwindcss.com/docs/brightness
         */
        brightness: [{
          brightness: [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Contrast
         * @see https://tailwindcss.com/docs/contrast
         */
        contrast: [{
          contrast: [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Drop Shadow
         * @see https://tailwindcss.com/docs/drop-shadow
         */
        "drop-shadow": [{
          "drop-shadow": [
            // Deprecated since Tailwind CSS v4.0.0
            "",
            "none",
            themeDropShadow,
            isArbitraryVariableShadow,
            isArbitraryShadow
          ]
        }],
        /**
         * Drop Shadow Color
         * @see https://tailwindcss.com/docs/filter-drop-shadow#setting-the-shadow-color
         */
        "drop-shadow-color": [{
          "drop-shadow": scaleColor()
        }],
        /**
         * Grayscale
         * @see https://tailwindcss.com/docs/grayscale
         */
        grayscale: [{
          grayscale: ["", isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Hue Rotate
         * @see https://tailwindcss.com/docs/hue-rotate
         */
        "hue-rotate": [{
          "hue-rotate": [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Invert
         * @see https://tailwindcss.com/docs/invert
         */
        invert: [{
          invert: ["", isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Saturate
         * @see https://tailwindcss.com/docs/saturate
         */
        saturate: [{
          saturate: [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Sepia
         * @see https://tailwindcss.com/docs/sepia
         */
        sepia: [{
          sepia: ["", isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Backdrop Filter
         * @see https://tailwindcss.com/docs/backdrop-filter
         */
        "backdrop-filter": [{
          "backdrop-filter": [
            // Deprecated since Tailwind CSS v3.0.0
            "",
            "none",
            isArbitraryVariable,
            isArbitraryValue
          ]
        }],
        /**
         * Backdrop Blur
         * @see https://tailwindcss.com/docs/backdrop-blur
         */
        "backdrop-blur": [{
          "backdrop-blur": scaleBlur()
        }],
        /**
         * Backdrop Brightness
         * @see https://tailwindcss.com/docs/backdrop-brightness
         */
        "backdrop-brightness": [{
          "backdrop-brightness": [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Backdrop Contrast
         * @see https://tailwindcss.com/docs/backdrop-contrast
         */
        "backdrop-contrast": [{
          "backdrop-contrast": [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Backdrop Grayscale
         * @see https://tailwindcss.com/docs/backdrop-grayscale
         */
        "backdrop-grayscale": [{
          "backdrop-grayscale": ["", isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Backdrop Hue Rotate
         * @see https://tailwindcss.com/docs/backdrop-hue-rotate
         */
        "backdrop-hue-rotate": [{
          "backdrop-hue-rotate": [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Backdrop Invert
         * @see https://tailwindcss.com/docs/backdrop-invert
         */
        "backdrop-invert": [{
          "backdrop-invert": ["", isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Backdrop Opacity
         * @see https://tailwindcss.com/docs/backdrop-opacity
         */
        "backdrop-opacity": [{
          "backdrop-opacity": [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Backdrop Saturate
         * @see https://tailwindcss.com/docs/backdrop-saturate
         */
        "backdrop-saturate": [{
          "backdrop-saturate": [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Backdrop Sepia
         * @see https://tailwindcss.com/docs/backdrop-sepia
         */
        "backdrop-sepia": [{
          "backdrop-sepia": ["", isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        // --------------
        // --- Tables ---
        // --------------
        /**
         * Border Collapse
         * @see https://tailwindcss.com/docs/border-collapse
         */
        "border-collapse": [{
          border: ["collapse", "separate"]
        }],
        /**
         * Border Spacing
         * @see https://tailwindcss.com/docs/border-spacing
         */
        "border-spacing": [{
          "border-spacing": scaleUnambiguousSpacing()
        }],
        /**
         * Border Spacing X
         * @see https://tailwindcss.com/docs/border-spacing
         */
        "border-spacing-x": [{
          "border-spacing-x": scaleUnambiguousSpacing()
        }],
        /**
         * Border Spacing Y
         * @see https://tailwindcss.com/docs/border-spacing
         */
        "border-spacing-y": [{
          "border-spacing-y": scaleUnambiguousSpacing()
        }],
        /**
         * Table Layout
         * @see https://tailwindcss.com/docs/table-layout
         */
        "table-layout": [{
          table: ["auto", "fixed"]
        }],
        /**
         * Caption Side
         * @see https://tailwindcss.com/docs/caption-side
         */
        caption: [{
          caption: ["top", "bottom"]
        }],
        // ---------------------------------
        // --- Transitions and Animation ---
        // ---------------------------------
        /**
         * Transition Property
         * @see https://tailwindcss.com/docs/transition-property
         */
        transition: [{
          transition: ["", "all", "colors", "opacity", "shadow", "transform", "none", isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Transition Behavior
         * @see https://tailwindcss.com/docs/transition-behavior
         */
        "transition-behavior": [{
          transition: ["normal", "discrete"]
        }],
        /**
         * Transition Duration
         * @see https://tailwindcss.com/docs/transition-duration
         */
        duration: [{
          duration: [isNumber, "initial", isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Transition Timing Function
         * @see https://tailwindcss.com/docs/transition-timing-function
         */
        ease: [{
          ease: ["linear", "initial", themeEase, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Transition Delay
         * @see https://tailwindcss.com/docs/transition-delay
         */
        delay: [{
          delay: [isNumber, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Animation
         * @see https://tailwindcss.com/docs/animation
         */
        animate: [{
          animate: ["none", themeAnimate, isArbitraryVariable, isArbitraryValue]
        }],
        // ------------------
        // --- Transforms ---
        // ------------------
        /**
         * Backface Visibility
         * @see https://tailwindcss.com/docs/backface-visibility
         */
        backface: [{
          backface: ["hidden", "visible"]
        }],
        /**
         * Perspective
         * @see https://tailwindcss.com/docs/perspective
         */
        perspective: [{
          perspective: [themePerspective, isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Perspective Origin
         * @see https://tailwindcss.com/docs/perspective-origin
         */
        "perspective-origin": [{
          "perspective-origin": scalePositionWithArbitrary()
        }],
        /**
         * Rotate
         * @see https://tailwindcss.com/docs/rotate
         */
        rotate: [{
          rotate: scaleRotate()
        }],
        /**
         * Rotate X
         * @see https://tailwindcss.com/docs/rotate
         */
        "rotate-x": [{
          "rotate-x": scaleRotate()
        }],
        /**
         * Rotate Y
         * @see https://tailwindcss.com/docs/rotate
         */
        "rotate-y": [{
          "rotate-y": scaleRotate()
        }],
        /**
         * Rotate Z
         * @see https://tailwindcss.com/docs/rotate
         */
        "rotate-z": [{
          "rotate-z": scaleRotate()
        }],
        /**
         * Scale
         * @see https://tailwindcss.com/docs/scale
         */
        scale: [{
          scale: scaleScale()
        }],
        /**
         * Scale X
         * @see https://tailwindcss.com/docs/scale
         */
        "scale-x": [{
          "scale-x": scaleScale()
        }],
        /**
         * Scale Y
         * @see https://tailwindcss.com/docs/scale
         */
        "scale-y": [{
          "scale-y": scaleScale()
        }],
        /**
         * Scale Z
         * @see https://tailwindcss.com/docs/scale
         */
        "scale-z": [{
          "scale-z": scaleScale()
        }],
        /**
         * Scale 3D
         * @see https://tailwindcss.com/docs/scale
         */
        "scale-3d": ["scale-3d"],
        /**
         * Skew
         * @see https://tailwindcss.com/docs/skew
         */
        skew: [{
          skew: scaleSkew()
        }],
        /**
         * Skew X
         * @see https://tailwindcss.com/docs/skew
         */
        "skew-x": [{
          "skew-x": scaleSkew()
        }],
        /**
         * Skew Y
         * @see https://tailwindcss.com/docs/skew
         */
        "skew-y": [{
          "skew-y": scaleSkew()
        }],
        /**
         * Transform
         * @see https://tailwindcss.com/docs/transform
         */
        transform: [{
          transform: [isArbitraryVariable, isArbitraryValue, "", "none", "gpu", "cpu"]
        }],
        /**
         * Transform Origin
         * @see https://tailwindcss.com/docs/transform-origin
         */
        "transform-origin": [{
          origin: scalePositionWithArbitrary()
        }],
        /**
         * Transform Style
         * @see https://tailwindcss.com/docs/transform-style
         */
        "transform-style": [{
          transform: ["3d", "flat"]
        }],
        /**
         * Translate
         * @see https://tailwindcss.com/docs/translate
         */
        translate: [{
          translate: scaleTranslate()
        }],
        /**
         * Translate X
         * @see https://tailwindcss.com/docs/translate
         */
        "translate-x": [{
          "translate-x": scaleTranslate()
        }],
        /**
         * Translate Y
         * @see https://tailwindcss.com/docs/translate
         */
        "translate-y": [{
          "translate-y": scaleTranslate()
        }],
        /**
         * Translate Z
         * @see https://tailwindcss.com/docs/translate
         */
        "translate-z": [{
          "translate-z": scaleTranslate()
        }],
        /**
         * Translate None
         * @see https://tailwindcss.com/docs/translate
         */
        "translate-none": ["translate-none"],
        // ---------------------
        // --- Interactivity ---
        // ---------------------
        /**
         * Accent Color
         * @see https://tailwindcss.com/docs/accent-color
         */
        accent: [{
          accent: scaleColor()
        }],
        /**
         * Appearance
         * @see https://tailwindcss.com/docs/appearance
         */
        appearance: [{
          appearance: ["none", "auto"]
        }],
        /**
         * Caret Color
         * @see https://tailwindcss.com/docs/just-in-time-mode#caret-color-utilities
         */
        "caret-color": [{
          caret: scaleColor()
        }],
        /**
         * Color Scheme
         * @see https://tailwindcss.com/docs/color-scheme
         */
        "color-scheme": [{
          scheme: ["normal", "dark", "light", "light-dark", "only-dark", "only-light"]
        }],
        /**
         * Cursor
         * @see https://tailwindcss.com/docs/cursor
         */
        cursor: [{
          cursor: ["auto", "default", "pointer", "wait", "text", "move", "help", "not-allowed", "none", "context-menu", "progress", "cell", "crosshair", "vertical-text", "alias", "copy", "no-drop", "grab", "grabbing", "all-scroll", "col-resize", "row-resize", "n-resize", "e-resize", "s-resize", "w-resize", "ne-resize", "nw-resize", "se-resize", "sw-resize", "ew-resize", "ns-resize", "nesw-resize", "nwse-resize", "zoom-in", "zoom-out", isArbitraryVariable, isArbitraryValue]
        }],
        /**
         * Field Sizing
         * @see https://tailwindcss.com/docs/field-sizing
         */
        "field-sizing": [{
          "field-sizing": ["fixed", "content"]
        }],
        /**
         * Pointer Events
         * @see https://tailwindcss.com/docs/pointer-events
         */
        "pointer-events": [{
          "pointer-events": ["auto", "none"]
        }],
        /**
         * Resize
         * @see https://tailwindcss.com/docs/resize
         */
        resize: [{
          resize: ["none", "", "y", "x"]
        }],
        /**
         * Scroll Behavior
         * @see https://tailwindcss.com/docs/scroll-behavior
         */
        "scroll-behavior": [{
          scroll: ["auto", "smooth"]
        }],
        /**
         * Scroll Margin
         * @see https://tailwindcss.com/docs/scroll-margin
         */
        "scroll-m": [{
          "scroll-m": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Margin X
         * @see https://tailwindcss.com/docs/scroll-margin
         */
        "scroll-mx": [{
          "scroll-mx": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Margin Y
         * @see https://tailwindcss.com/docs/scroll-margin
         */
        "scroll-my": [{
          "scroll-my": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Margin Start
         * @see https://tailwindcss.com/docs/scroll-margin
         */
        "scroll-ms": [{
          "scroll-ms": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Margin End
         * @see https://tailwindcss.com/docs/scroll-margin
         */
        "scroll-me": [{
          "scroll-me": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Margin Top
         * @see https://tailwindcss.com/docs/scroll-margin
         */
        "scroll-mt": [{
          "scroll-mt": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Margin Right
         * @see https://tailwindcss.com/docs/scroll-margin
         */
        "scroll-mr": [{
          "scroll-mr": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Margin Bottom
         * @see https://tailwindcss.com/docs/scroll-margin
         */
        "scroll-mb": [{
          "scroll-mb": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Margin Left
         * @see https://tailwindcss.com/docs/scroll-margin
         */
        "scroll-ml": [{
          "scroll-ml": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Padding
         * @see https://tailwindcss.com/docs/scroll-padding
         */
        "scroll-p": [{
          "scroll-p": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Padding X
         * @see https://tailwindcss.com/docs/scroll-padding
         */
        "scroll-px": [{
          "scroll-px": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Padding Y
         * @see https://tailwindcss.com/docs/scroll-padding
         */
        "scroll-py": [{
          "scroll-py": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Padding Start
         * @see https://tailwindcss.com/docs/scroll-padding
         */
        "scroll-ps": [{
          "scroll-ps": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Padding End
         * @see https://tailwindcss.com/docs/scroll-padding
         */
        "scroll-pe": [{
          "scroll-pe": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Padding Top
         * @see https://tailwindcss.com/docs/scroll-padding
         */
        "scroll-pt": [{
          "scroll-pt": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Padding Right
         * @see https://tailwindcss.com/docs/scroll-padding
         */
        "scroll-pr": [{
          "scroll-pr": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Padding Bottom
         * @see https://tailwindcss.com/docs/scroll-padding
         */
        "scroll-pb": [{
          "scroll-pb": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Padding Left
         * @see https://tailwindcss.com/docs/scroll-padding
         */
        "scroll-pl": [{
          "scroll-pl": scaleUnambiguousSpacing()
        }],
        /**
         * Scroll Snap Align
         * @see https://tailwindcss.com/docs/scroll-snap-align
         */
        "snap-align": [{
          snap: ["start", "end", "center", "align-none"]
        }],
        /**
         * Scroll Snap Stop
         * @see https://tailwindcss.com/docs/scroll-snap-stop
         */
        "snap-stop": [{
          snap: ["normal", "always"]
        }],
        /**
         * Scroll Snap Type
         * @see https://tailwindcss.com/docs/scroll-snap-type
         */
        "snap-type": [{
          snap: ["none", "x", "y", "both"]
        }],
        /**
         * Scroll Snap Type Strictness
         * @see https://tailwindcss.com/docs/scroll-snap-type
         */
        "snap-strictness": [{
          snap: ["mandatory", "proximity"]
        }],
        /**
         * Touch Action
         * @see https://tailwindcss.com/docs/touch-action
         */
        touch: [{
          touch: ["auto", "none", "manipulation"]
        }],
        /**
         * Touch Action X
         * @see https://tailwindcss.com/docs/touch-action
         */
        "touch-x": [{
          "touch-pan": ["x", "left", "right"]
        }],
        /**
         * Touch Action Y
         * @see https://tailwindcss.com/docs/touch-action
         */
        "touch-y": [{
          "touch-pan": ["y", "up", "down"]
        }],
        /**
         * Touch Action Pinch Zoom
         * @see https://tailwindcss.com/docs/touch-action
         */
        "touch-pz": ["touch-pinch-zoom"],
        /**
         * User Select
         * @see https://tailwindcss.com/docs/user-select
         */
        select: [{
          select: ["none", "text", "all", "auto"]
        }],
        /**
         * Will Change
         * @see https://tailwindcss.com/docs/will-change
         */
        "will-change": [{
          "will-change": ["auto", "scroll", "contents", "transform", isArbitraryVariable, isArbitraryValue]
        }],
        // -----------
        // --- SVG ---
        // -----------
        /**
         * Fill
         * @see https://tailwindcss.com/docs/fill
         */
        fill: [{
          fill: ["none", ...scaleColor()]
        }],
        /**
         * Stroke Width
         * @see https://tailwindcss.com/docs/stroke-width
         */
        "stroke-w": [{
          stroke: [isNumber, isArbitraryVariableLength, isArbitraryLength, isArbitraryNumber]
        }],
        /**
         * Stroke
         * @see https://tailwindcss.com/docs/stroke
         */
        stroke: [{
          stroke: ["none", ...scaleColor()]
        }],
        // ---------------------
        // --- Accessibility ---
        // ---------------------
        /**
         * Forced Color Adjust
         * @see https://tailwindcss.com/docs/forced-color-adjust
         */
        "forced-color-adjust": [{
          "forced-color-adjust": ["auto", "none"]
        }]
      },
      conflictingClassGroups: {
        overflow: ["overflow-x", "overflow-y"],
        overscroll: ["overscroll-x", "overscroll-y"],
        inset: ["inset-x", "inset-y", "start", "end", "top", "right", "bottom", "left"],
        "inset-x": ["right", "left"],
        "inset-y": ["top", "bottom"],
        flex: ["basis", "grow", "shrink"],
        gap: ["gap-x", "gap-y"],
        p: ["px", "py", "ps", "pe", "pt", "pr", "pb", "pl"],
        px: ["pr", "pl"],
        py: ["pt", "pb"],
        m: ["mx", "my", "ms", "me", "mt", "mr", "mb", "ml"],
        mx: ["mr", "ml"],
        my: ["mt", "mb"],
        size: ["w", "h"],
        "font-size": ["leading"],
        "fvn-normal": ["fvn-ordinal", "fvn-slashed-zero", "fvn-figure", "fvn-spacing", "fvn-fraction"],
        "fvn-ordinal": ["fvn-normal"],
        "fvn-slashed-zero": ["fvn-normal"],
        "fvn-figure": ["fvn-normal"],
        "fvn-spacing": ["fvn-normal"],
        "fvn-fraction": ["fvn-normal"],
        "line-clamp": ["display", "overflow"],
        rounded: ["rounded-s", "rounded-e", "rounded-t", "rounded-r", "rounded-b", "rounded-l", "rounded-ss", "rounded-se", "rounded-ee", "rounded-es", "rounded-tl", "rounded-tr", "rounded-br", "rounded-bl"],
        "rounded-s": ["rounded-ss", "rounded-es"],
        "rounded-e": ["rounded-se", "rounded-ee"],
        "rounded-t": ["rounded-tl", "rounded-tr"],
        "rounded-r": ["rounded-tr", "rounded-br"],
        "rounded-b": ["rounded-br", "rounded-bl"],
        "rounded-l": ["rounded-tl", "rounded-bl"],
        "border-spacing": ["border-spacing-x", "border-spacing-y"],
        "border-w": ["border-w-x", "border-w-y", "border-w-s", "border-w-e", "border-w-t", "border-w-r", "border-w-b", "border-w-l"],
        "border-w-x": ["border-w-r", "border-w-l"],
        "border-w-y": ["border-w-t", "border-w-b"],
        "border-color": ["border-color-x", "border-color-y", "border-color-s", "border-color-e", "border-color-t", "border-color-r", "border-color-b", "border-color-l"],
        "border-color-x": ["border-color-r", "border-color-l"],
        "border-color-y": ["border-color-t", "border-color-b"],
        translate: ["translate-x", "translate-y", "translate-none"],
        "translate-none": ["translate", "translate-x", "translate-y", "translate-z"],
        "scroll-m": ["scroll-mx", "scroll-my", "scroll-ms", "scroll-me", "scroll-mt", "scroll-mr", "scroll-mb", "scroll-ml"],
        "scroll-mx": ["scroll-mr", "scroll-ml"],
        "scroll-my": ["scroll-mt", "scroll-mb"],
        "scroll-p": ["scroll-px", "scroll-py", "scroll-ps", "scroll-pe", "scroll-pt", "scroll-pr", "scroll-pb", "scroll-pl"],
        "scroll-px": ["scroll-pr", "scroll-pl"],
        "scroll-py": ["scroll-pt", "scroll-pb"],
        touch: ["touch-x", "touch-y", "touch-pz"],
        "touch-x": ["touch"],
        "touch-y": ["touch"],
        "touch-pz": ["touch"]
      },
      conflictingClassGroupModifiers: {
        "font-size": ["leading"]
      },
      orderSensitiveModifiers: ["*", "**", "after", "backdrop", "before", "details-content", "file", "first-letter", "first-line", "marker", "placeholder", "selection"]
    };
  };
  var twMerge = /* @__PURE__ */ createTailwindMerge(getDefaultConfig);

  // src/Aonik.AdminUi/src/lib/utils.ts
  function cn(...inputs) {
    return twMerge(clsx(inputs));
  }

  // src/Aonik.AdminUi/src/lib/menuIcons.generated.ts
  var menuIconPaths = {
    "family": '<circle cx="8" cy="6" r="3"/><circle cx="17" cy="8" r="2"/><path d="M2 21v-5a6 6 0 0 1 12 0v5m2-7a5 5 0 0 1 6 5v2"/>',
    "children": '<circle cx="12" cy="8" r="4"/><path d="M5 21v-2a7 7 0 0 1 14 0v2M9 7h.01M15 7h.01m-5 3c1 1 3 1 4 0M12 4l2-2"/>',
    "worlds": '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c-5 5-5 13 0 18 5-5 5-13 0-18M5 7h14M5 17h14"/>',
    "editions": '<path d="M3 4h7l2 2 2-2h7v16h-7l-2 2-2-2H3zM12 6v16M6 9h3m-3 4h3m6-4h3m-3 4h3"/>',
    "auto-story": '<path d="M4 5h6l2 2 2-2h6v15h-6l-2 2-2-2H4zM12 7v15M18 2v5m-2.5-2.5h5"/>',
    "credits": '<ellipse cx="10" cy="6" rx="7" ry="3"/><path d="M3 6v5c0 2 3 3 7 3m7-8v4M3 11v5c0 2 3 3 7 3"/><circle cx="17" cy="17" r="5"/><path d="M17 14v6m-2-3h4"/>',
    "print": '<path d="M6 8V3h12v5M6 17H3V8h18v9h-3M6 13h12v9H6zM17 11h.01M9 17h6m-6 3h4"/>',
    "submissions": '<path d="M5 3h10l4 4v14H5zM14 3v5h5M12 17V10m-3 3 3-3 3 3"/>',
    "catalogue": '<rect x="3" y="3" width="7" height="18" rx="1"/><rect x="14" y="3" width="7" height="18" rx="1"/><path d="M6 7h1m-1 4h1m10-4h1m-1 4h1M3 17h7m4 0h7"/>',
    "release": '<path d="M5 21V3m0 1h14l-3 5 3 5H5"/><path d="m10 8 2 2 3-4"/>',
    "community": '<circle cx="7" cy="7" r="3"/><path d="M1 21v-3a6 6 0 0 1 10-4M14 3h9v9h-5l-4 3V3zM17 6h3m-3 3h3M15 21h6"/>',
    "support": '<path d="M4 13v-3a8 8 0 0 1 16 0v3M4 12H2v7h4v-7zM20 12h2v7h-4v-7zM20 19a3 3 0 0 1-3 3h-4"/>',
    "email": '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/>',
    "inbox": '<path d="m5 4-3 10v6h20v-6L19 4zM2 14h6l2 3h4l2-3h6"/>',
    "knowledge": '<path d="M12 5C9 3 5 3 2 4v15c3-1 7-1 10 1 3-2 7-2 10-1V4c-3-1-7-1-10 1zM12 5v15M5 8h3m-3 4h3m8-4h3m-3 4h3"/>',
    "search": '<circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/>',
    "home": '<path d="m3 10 9-7 9 7M5 9v12h5v-7h4v7h5V9"/>',
    "workspace": '<rect x="3" y="3" width="18" height="18" rx="2"/><path d="M3 8h18M9 8v13"/>',
    "overview": '<rect x="3" y="3" width="7" height="10" rx="1.5"/><rect x="14" y="3" width="7" height="6" rx="1.5"/><rect x="3" y="17" width="7" height="4" rx="1.5"/><rect x="14" y="13" width="7" height="8" rx="1.5"/>',
    "orders": '<path d="M6 3h12v18l-3-2-3 2-3-2-3 2zM9 8h6M9 12h6M9 16h3"/>',
    "new-order": '<path d="M13 3H6v18h12v-7M9 8h3M9 12h3M17 3v6m-3-3h6"/>',
    "transfer-orders": '<path d="M6 3h9l3 3v4M6 3v18h4M9 7h3M9 11h3M13 14h8m-3-3 3 3-3 3M21 20h-8m3-3-3 3 3 3"/>',
    "beneficiaries": '<circle cx="8" cy="7" r="3"/><path d="M2 21v-3a6 6 0 0 1 10-4M14 11h8v10h-8zM16 15h4m-4 3h2"/>',
    "mobile-money": '<rect x="5" y="2" width="12" height="20" rx="2"/><path d="M9 5h4m-2 14h.01M10 11h11m-3-3 3 3-3 3"/>',
    "customers": '<circle cx="9" cy="7" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3m1-17a3 3 0 0 1 0 6m3 11v-3a6 6 0 0 0-2-4"/>',
    "enquiries": '<rect x="3" y="4" width="18" height="14" rx="2"/><path d="m3 6 9 6 9-6M8 18l-3 3v-3"/>',
    "approvals": '<rect x="5" y="5" width="14" height="16" rx="2"/><rect x="9" y="3" width="6" height="4" rx="1"/><path d="m8 14 3 3 5-6"/>',
    "bill-payments": '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 10h18m-8 4h5m-9 0h.01"/>',
    "billers": '<path d="M4 21V5h10v16M14 9h6v12M2 21h20M8 9h2m-2 4h2m-2 4h2m9-4h.01M17 17h.01"/>',
    "categories": '<path d="M3 3h8l10 10-8 8L3 11z"/><circle cx="7.5" cy="7.5" r="1"/>',
    "remittances": '<circle cx="12" cy="12" r="9"/><path d="M4 9h11m-3-3 3 3-3 3m8 3H9m3-3-3 3 3 3"/>',
    "corridors": '<circle cx="5" cy="5" r="2"/><circle cx="19" cy="19" r="2"/><path d="M7 5h9a4 4 0 0 1 0 8H8a3 3 0 0 0 0 6h9"/>',
    "partners": '<circle cx="5" cy="5" r="2"/><circle cx="19" cy="5" r="2"/><circle cx="12" cy="19" r="2"/><path d="M7 5h10M6 7l5 10m7-10-5 10"/>',
    "fx": '<path d="M3 7h17m-4-4 4 4-4 4M21 17H4m4-4-4 4 4 4M7 3v1m10 16v1"/>',
    "billing": '<path d="M3 6h18v14H3zM3 10h18M7 3v5m10-5v5M7 14h4m-4 3h7"/>',
    "invoices": '<path d="M6 3h9l4 4v14H6zM14 3v5h5M9 12h7m-7 4h4"/>',
    "accounts": '<path d="m3 8 9-5 9 5H3zm2 4v5m5-5v5m4-5v5m5-5v5M3 21h18M5 18h14"/>',
    "collections": '<circle cx="9" cy="9" r="5"/><path d="M9 6v6m-2-4h3a1 1 0 0 1 0 2H8m7-1a6 6 0 1 1-6 6m7-2v6m-3-3 3 3 3-3"/>',
    "ledger": '<path d="M5 3h14v18H7a2 2 0 0 1-2-2V3zm0 14h14M9 7h6m-6 4h6"/>',
    "chart-of-accounts": '<rect x="9" y="3" width="6" height="5" rx="1"/><rect x="3" y="16" width="6" height="5" rx="1"/><rect x="15" y="16" width="6" height="5" rx="1"/><path d="M12 8v4M6 16v-4h12v4"/>',
    "journal": '<path d="M5 3h14v18H5zM9 3v18m3-13h4m-4 4h4m-4 4h4M3 7h3m-3 5h3m-3 5h3"/>',
    "commerce": '<path d="M3 9 5 3h14l2 6M3 9a3 3 0 0 0 6 0 3 3 0 0 0 6 0 3 3 0 0 0 6 0M5 12v9h14v-9M9 21v-6h6v6"/>',
    "products": '<path d="m3 7 9-4 9 4-9 4zm0 0v10l9 4 9-4V7M12 11v10M7.5 5l9 4"/>',
    "menu": '<path d="M4 3v6a3 3 0 0 0 6 0V3M7 3v18m10 0V3c-4 2-4 10 0 10h3V3h-3"/>',
    "food-orders": '<path d="M5 7h14l1 14H4zM8 7V6a4 4 0 0 1 8 0v1M8 13h8m-8 4h5"/>',
    "personalisation": '<path d="M3 6h8m6 0h4M3 12h3m6 0h9M3 18h11m6 0h1"/><circle cx="14" cy="6" r="3"/><circle cx="9" cy="12" r="3"/><circle cx="17" cy="18" r="3"/>',
    "content": '<path d="M5 3h10l4 4v14H5zM14 3v5h5M8 12h8m-8 4h5"/>',
    "menu-content": '<path d="M5 3h14v18H5zM8 7h8m-8 4h8m-8 4h4m-4 3h3m5-3 2 2-2 2"/>',
    "box-plans": '<rect x="3" y="6" width="18" height="15" rx="2"/><path d="M3 11h18M8 3v5m8-5v5M8 15h3v3H8zm7 0h1m-1 3h1"/>',
    "delivery": '<path d="M3 6h11v11H3zM14 10h4l3 4v3h-2m-5 0h-3"/><circle cx="7" cy="18" r="2"/><circle cx="17" cy="18" r="2"/>',
    "carts": '<path d="M3 3h2l3 13h11l2-9H6"/><circle cx="9" cy="20" r="1"/><circle cx="18" cy="20" r="1"/>',
    "merchandising": '<path d="M3 4h18v16H3zM3 9h18M9 9v11m5-7 1 2 2 .3-1.5 1.5.4 2-1.9-1-1.9 1 .4-2L11 15.3l2-.3z"/>',
    "discounts": '<path d="M4 3h16v6a3 3 0 0 0 0 6v6H4v-6a3 3 0 0 0 0-6zM9 16l6-8"/><circle cx="9" cy="9" r="1"/><circle cx="15" cy="15" r="1"/>',
    "storefront-config": '<path d="m3 8 2-5h14l2 5M3 8a3 3 0 0 0 6 0 3 3 0 0 0 6 0 3 3 0 0 0 6 0M5 12v9h14v-9M8 16h8m-5-2v4"/>',
    "packing": '<path d="M6 3h12v18H6zM9 8h6m-6 4 1 1 2-2m2 1h1m-6 4 1 1 2-2m2 1h1"/>',
    "inventory": '<path d="m3 8 9-5 9 5-9 5zm0 5 9 5 9-5m-18 5 9 5 9-5"/>',
    "kitchen": '<path d="M6 14a4 4 0 0 1 1-8 5 5 0 0 1 10 0 4 4 0 0 1 1 8v7H6zM6 17h12m-9-6v3m6-3v3"/>',
    "ingredients": '<path d="M12 21V6m0 5c-4 0-6-2-6-5 4 0 6 2 6 5zm0 6c-4 0-6-2-6-5 4 0 6 2 6 5zm0-9c4 0 6-2 6-5-4 0-6 2-6 5zm0 6c4 0 6-2 6-5-4 0-6 2-6 5z"/>',
    "recipes": '<path d="M4 4h7l2 2h7v15h-7l-2-2H4zM12 7v12M7 8v6m-2-4h4m7 0h1m-1 4h1"/>',
    "planning": '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M7 3v4m10-4v4M3 10h18m-14 4h3m-3 3h6"/>',
    "production": '<path d="M12 3c0 4 6 6 6 11a6 6 0 0 1-12 0c0-3 2-5 3-6 0 3 2 4 2 4s3-4 1-9z"/>',
    "low-stock": '<path d="m12 3 10 18H2zM12 9v5m0 3h.01"/>',
    "suppliers": '<path d="M3 21V9l6-3v5l6-3v13M15 13l6-3v11M2 21h20M6 15h1m3 0h1m-5 3h1m3 0h1m7-3h1m-1 3h1"/>',
    "purchase-orders": '<path d="M6 3h12v18H6zM9 7h6m-6 4h6m-4 4v4m-2-2h4"/>',
    "margin": '<path d="M3 21h18M6 17v-5m5 5V8m5 9V4m-11 4 4-4 4 2 7-4"/>',
    "wallet": '<path d="M4 7V5a2 2 0 0 1 2-2h12v4M4 7h16v14H4zM16 12h5v5h-5zM18 14.5h.01"/>',
    "savings": '<path d="M4 11a7 7 0 0 1 12-5l3-2v5l2 2v5h-3l-1 5h-3l-1-3H9l-1 3H5l-1-5-2-1v-4h2zM8 7h4m4 4h.01"/>',
    "transfers": '<path d="M3 7h17m-4-4 4 4-4 4M21 17H4m4-4-4 4 4 4"/>',
    "compliance": '<path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6zM8 12l3 3 5-6"/>',
    "documents": '<path d="M8 3h8l4 4v13H8zM15 3v5h5M4 7v15h11"/>',
    "team": '<circle cx="8" cy="7" r="3"/><path d="M2 21v-3a6 6 0 0 1 12 0v3m3-16v6m-3-3h6m-3 6v7m-3-4h6"/>',
    "users": '<circle cx="12" cy="7" r="3"/><path d="M5 21v-3a7 7 0 0 1 14 0v3"/>',
    "roles": '<rect x="3" y="5" width="18" height="15" rx="2"/><path d="M8 3v4m8-4v4M12 10l1.2 2.4 2.8.4-2 2 .5 2.7-2.5-1.3-2.5 1.3.5-2.7-2-2 2.8-.4z"/>',
    "permissions": '<circle cx="8" cy="9" r="5"/><path d="m12 13 8 8m-3-3 3-3m-6 0 3-3M6 8h.01"/>',
    "deleted-users": '<circle cx="9" cy="7" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0m2-6h5m-4 0v8h3v-8m-2-3h1"/>',
    "agents": '<rect x="4" y="7" width="16" height="14" rx="3"/><path d="M12 3v4M2 12h2m16 0h2M9 12h.01m6 0h.01M9 17h6"/>',
    "chat": '<path d="M21 15a3 3 0 0 1-3 3H9l-6 3V6a3 3 0 0 1 3-3h12a3 3 0 0 1 3 3zM7 8h10m-10 5h6"/>',
    "models": '<rect x="6" y="6" width="12" height="12" rx="2"/><rect x="10" y="10" width="4" height="4" rx=".5"/><path d="M9 3v3m6-3v3M9 18v3m6-3v3M3 9h3m-3 6h3m12-6h3m-3 6h3"/>',
    "tasks": '<rect x="5" y="5" width="14" height="16" rx="2"/><rect x="9" y="3" width="6" height="4" rx="1"/><path d="M8 11h2m3 0h3m-8 5h2m3 0h3"/>',
    "policies": '<path d="m12 3 8 3v6c0 5-8 9-8 9s-8-4-8-9V6zM9 10h6m-6 4h6"/>',
    "usage": '<path d="M3 21h18M6 17v-5m6 5V7m6 10V3"/>',
    "routing": '<path d="M5 3v9a5 5 0 0 0 5 5h10m-4-4 4 4-4 4M5 7h15m-4-4 4 4-4 4"/>',
    "playground": '<path d="M9 3h6m-5 0v7l-5 8a2 2 0 0 0 2 3h10a2 2 0 0 0 2-3l-5-8V3M8 15h8"/>',
    "runs": '<circle cx="12" cy="12" r="9"/><path d="m10 8 6 4-6 4z"/>',
    "workflows": '<rect x="3" y="3" width="6" height="5" rx="1"/><rect x="15" y="3" width="6" height="5" rx="1"/><rect x="9" y="16" width="6" height="5" rx="1"/><path d="M6 8v4h12V8m-6 4v4"/>',
    "extensions": '<path d="M4 4h5a3 3 0 1 1 6 0h5v5a3 3 0 1 0 0 6v5h-5a3 3 0 1 0-6 0H4v-5a3 3 0 1 0 0-6z"/>',
    "content-blocks": '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><path d="M14 14h7m-7 4h7m-7 3h4"/>',
    "content-wizard": '<path d="m4 20 12-12 4 4L8 24m5-13 4 4M6 3v4M4 5h4m12-3v4m-2-2h4M3 12v4m-2-2h4" transform="translate(1 -1) scale(.95)"/>',
    "media": '<rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8" cy="8" r="2"/><path d="m3 17 5-5 4 4 4-7 5 8"/>',
    "settings": '<path d="m9 3-1 3-3 1-2 4 2 2v3l4 2 1 3h4l1-3 4-2v-3l2-2-2-4-3-1-1-3z"/><circle cx="12" cy="12" r="3"/>',
    "authentication": '<rect x="5" y="10" width="14" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3M12 14v3"/>',
    "communication": '<path d="m3 11 18-8-6 18-4-7zM11 14 21 3"/>',
    "email-delivered": '<path d="M3 5h18v9M3 5l9 6 9-6M3 5v14h8m3-1 3 3 5-7"/>',
    "email-bounced": '<path d="M3 5h18v8M3 5l9 6 9-6M3 5v14h7m6-4-4 4 4 4m-4-4h6a3 3 0 0 0 3-3"/>',
    "gateways": '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="M3 10h18m-11 5h6m-3-2 3 2-3 2"/>',
    "credentials": '<rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="9" cy="10" r="3"/><path d="m11 12 5 5m-2-2 2-2"/>',
    "audit": '<path d="M5 3h10l4 4v14H5zM14 3v5h5M8 12h5m-5 4h3m3 0 1 1 3-3"/>',
    "speech": '<rect x="9" y="3" width="6" height="12" rx="3"/><path d="M5 10v2a7 7 0 0 0 14 0v-2M12 19v3m-4 0h8"/>',
    "jobs": '<path d="M3 8h18v13H3zM8 8V4h8v4"/><circle cx="12" cy="14" r="3"/><path d="M12 12v2l1 1"/>',
    "tools": '<path d="M14 3a6 6 0 0 0-7 8L3 17a3 3 0 0 0 4 4l6-6a6 6 0 0 0 8-7l-4 4-4-1-1-4z"/>',
    "notifications": '<path d="M6 8a6 6 0 0 1 12 0c0 6 3 6 3 9H3c0-3 3-3 3-9m4 13h4"/>',
    "modules": '<path d="m3 7 4-2 4 2-4 2zm0 0v5l4 2 4-2V7m2 0 4-2 4 2-4 2zm0 0v5l4 2 4-2V7m-13 9 4-2 4 2-4 2zm0 0v5l4 2 4-2v-5"/>',
    "tenants": '<path d="M4 21V3h10v18M14 9h6v12M2 21h20M8 7h2m-2 4h2m-2 4h2m-1 6v-3m8-5h.01m0 4h.01"/>',
    "observability": '<path d="M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12z"/><circle cx="12" cy="12" r="3"/>',
    "topology": '<circle cx="12" cy="12" r="3"/><circle cx="4" cy="4" r="2"/><circle cx="20" cy="4" r="2"/><circle cx="4" cy="20" r="2"/><circle cx="20" cy="20" r="2"/><path d="m6 6 4 4m4 4 4 4m0-12-4 4m-4 4-4 4"/>',
    "traces": '<circle cx="6" cy="4" r="2"/><circle cx="6" cy="20" r="2"/><circle cx="18" cy="16" r="2"/><path d="M6 6v12m0-9h6a6 6 0 0 1 6 5"/>',
    "logs": '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="m6 8 4 4-4 4m7 0h5"/>',
    "numbering": '<path d="M9 3 7 21M17 3l-2 18M3 9h18M2 15h18"/>',
    "transactions": '<path d="M4 5h16v14H4zM8 9h8m-3-2 3 2-3 2m3 4H8m3-2-3 2 3 2"/>',
    "connection": '<path d="M8 3v5m8-5v5M6 8h12v3a6 6 0 0 1-12 0zM12 17v4"/>'
  };

  // src/Aonik.AdminUi/src/lib/menuIcons.ts
  function getMenuIconPath(reference) {
    if (!reference.startsWith("menu-")) return void 0;
    const name = reference.slice(5);
    return Object.hasOwn(menuIconPaths, name) ? menuIconPaths[name] : void 0;
  }

  // src/Aonik.AdminUi/src/components/layout/aonik/AonikTemplateIcon.tsx
  var ICON_PATHS = {
    home: '<path d="M3 11l9-7 9 7"/><path d="M5 10v10h14V10"/>',
    dashboard: '<rect x="3" y="3" width="7" height="9"/><rect x="14" y="3" width="7" height="5"/><rect x="14" y="12" width="7" height="9"/><rect x="3" y="16" width="7" height="5"/>',
    ledger: '<path d="M4 4h13a2 2 0 0 1 2 2v14H6a2 2 0 0 1-2-2V4z"/><path d="M4 4v14a2 2 0 0 0 2 2"/><path d="M8 8h8M8 12h8M8 16h5"/>',
    invoice: '<path d="M6 3h9l4 4v14H6z"/><path d="M14 3v5h5"/><path d="M9 13h6M9 17h6"/>',
    bank: '<path d="M3 10l9-6 9 6"/><path d="M5 10v7M10 10v7M14 10v7M19 10v7"/><path d="M3 20h18"/>',
    payout: '<path d="M3 12h14"/><path d="M13 6l6 6-6 6"/><circle cx="4" cy="12" r="1.5"/>',
    shield: '<path d="M12 3l8 3v6c0 5-3.5 8.5-8 9-4.5-.5-8-4-8-9V6z"/>',
    chart: '<path d="M3 20h18"/><rect x="5" y="12" width="3" height="6"/><rect x="11" y="7" width="3" height="11"/><rect x="17" y="3" width="3" height="15"/>',
    users: '<circle cx="9" cy="8" r="3"/><path d="M3 20c0-3 3-5 6-5s6 2 6 5"/><circle cx="17" cy="8" r="2.5"/><path d="M14 20c0-2.5 2.5-4 4-4"/>',
    settings: '<circle cx="12" cy="12" r="3"/><path d="M19 12a7 7 0 0 0-.2-1.6l2-1.6-2-3.4-2.4.9a7 7 0 0 0-2.8-1.6L13 2h-4l-.6 2.7a7 7 0 0 0-2.8 1.6l-2.4-.9-2 3.4 2 1.6A7 7 0 0 0 3 12c0 .5.1 1.1.2 1.6l-2 1.6 2 3.4 2.4-.9a7 7 0 0 0 2.8 1.6L9 22h4l.6-2.7a7 7 0 0 0 2.8-1.6l2.4.9 2-3.4-2-1.6c.1-.5.2-1.1.2-1.6z"/>',
    search: '<circle cx="11" cy="11" r="7"/><path d="m21 21-4.3-4.3"/>',
    bell: '<path d="M6 8a6 6 0 0 1 12 0c0 7 3 8 3 8H3s3-1 3-8"/><path d="M10 20a2 2 0 0 0 4 0"/>',
    chevron: '<path d="m9 6 6 6-6 6"/>',
    chevdown: '<path d="m6 9 6 6 6-6"/>',
    plus: '<path d="M12 5v14M5 12h14"/>',
    more: '<circle cx="5" cy="12" r="1.5"/><circle cx="12" cy="12" r="1.5"/><circle cx="19" cy="12" r="1.5"/>',
    check: '<path d="M5 12l5 5L20 7"/>',
    close: '<path d="M6 6l12 12M18 6L6 18"/>',
    x: '<path d="M6 6l12 12M18 6L6 18"/>',
    sparkles: '<path d="M12 3l1.5 4.5L18 9l-4.5 1.5L12 15l-1.5-4.5L6 9l4.5-1.5z"/><path d="M19 15l.8 2.2L22 18l-2.2.8L19 21l-.8-2.2L16 18l2.2-.8z"/>',
    bot: '<rect x="4" y="8" width="16" height="12" rx="3"/><path d="M12 3v5"/><circle cx="9" cy="14" r="1"/><circle cx="15" cy="14" r="1"/><path d="M2 14h2M20 14h2"/>',
    filter: '<path d="M3 5h18l-7 9v6l-4-2v-4z"/>',
    calendar: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M3 10h18M8 3v4M16 3v4"/>',
    download: '<path d="M12 3v13"/><path d="m7 11 5 5 5-5"/><path d="M4 20h16"/>',
    upload: '<path d="M12 20V7"/><path d="m7 12 5-5 5 5"/><path d="M4 4h16"/>',
    help: '<circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 0 1 5 0c0 1.5-2.5 2-2.5 3.5"/><path d="M12 17v.01"/>',
    fullscreen: '<path d="M3 9V3h6M21 9V3h-6M3 15v6h6M21 15v6h-6"/>',
    inbox: '<path d="M22 12H16l-2 3h-4l-2-3H2"/><path d="M5 3h14l3 9v7a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2v-7z"/>',
    building: '<rect x="4" y="3" width="16" height="18" rx="1"/><path d="M8 7h2M8 11h2M8 15h2M14 7h2M14 11h2M14 15h2"/>',
    users2: '<circle cx="9" cy="8" r="3"/><path d="M3 20c0-3 3-5 6-5s6 2 6 5"/><circle cx="17" cy="8" r="2.5"/><path d="M14 20c0-2.5 2.5-4 4-4"/>',
    network: '<circle cx="12" cy="5" r="2"/><circle cx="5" cy="19" r="2"/><circle cx="19" cy="19" r="2"/><path d="M12 7v3M9 13l-3 4M15 13l3 4M9 13h6"/><rect x="8" y="10" width="8" height="4" rx="1"/>',
    globe2: '<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18"/>',
    tag: '<path d="M3 3h9l9 9-9 9-9-9z"/><circle cx="8" cy="8" r="1.5" fill="currentColor"/>',
    arrows: '<path d="M7 3v18M3 7l4-4 4 4"/><path d="M17 21V3M13 17l4 4 4-4"/>',
    book: '<path d="M4 4h13a2 2 0 0 1 2 2v14H6a2 2 0 0 1-2-2V4z"/><path d="M4 4v14a2 2 0 0 0 2 2"/><path d="M8 8h8M8 12h8M8 16h5"/>',
    receipt: '<path d="M6 3h12v18l-2-1.5-2 1.5-2-1.5-2 1.5-2-1.5-2 1.5z"/><path d="M9 8h6M9 12h6M9 16h4"/>',
    list: '<path d="M8 6h13M8 12h13M8 18h13"/><circle cx="4" cy="6" r="1" fill="currentColor"/><circle cx="4" cy="12" r="1" fill="currentColor"/><circle cx="4" cy="18" r="1" fill="currentColor"/>',
    book2: '<path d="M4 4h13a2 2 0 0 1 2 2v14H6a2 2 0 0 1-2-2V4z"/><path d="M4 4v14a2 2 0 0 0 2 2"/>',
    wrench: '<path d="M15 3a6 6 0 0 1 5 9l-12 12-4-4L16 8a6 6 0 0 1-1-5z"/>',
    landmark: '<path d="M3 21h18M5 10v10M9 10v10M15 10v10M19 10v10M2 8l10-5 10 5v2H2z"/>',
    clipcheck: '<rect x="7" y="4" width="10" height="4" rx="1"/><path d="M7 6H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V8a2 2 0 0 0-2-2h-2"/><path d="M9 14l2 2 4-4"/>',
    verified: '<path d="M12 2l3 2 3-1 1 3 3 2-1 3 1 3-3 2-1 3-3-1-3 2-3-2-3 1-1-3-3-2 1-3-1-3 3-2 1-3 3 1z"/><path d="M9 12l2 2 4-4"/>',
    activity: '<path d="M3 12h4l3-8 4 16 3-8h4"/>',
    gitbranch: '<circle cx="6" cy="5" r="2"/><circle cx="18" cy="19" r="2"/><circle cx="6" cy="19" r="2"/><path d="M6 7v10"/><path d="M18 17V9a4 4 0 0 0-4-4h-2"/>',
    terminal: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="m6 9 3 3-3 3M12 15h5"/>',
    route: '<circle cx="5" cy="6" r="2"/><circle cx="19" cy="18" r="2"/><path d="M7 6h4a4 4 0 0 1 4 4v4a4 4 0 0 0 4 4"/>',
    workflow: '<rect x="3" y="3" width="6" height="6" rx="1"/><rect x="15" y="3" width="6" height="6" rx="1"/><rect x="9" y="15" width="6" height="6" rx="1"/><path d="M6 9v3a2 2 0 0 0 2 2h4"/><path d="M18 9v3a2 2 0 0 1-2 2h-4"/>',
    // Commerce nav icons (Spec 073 §3) — ported from the approved starterkit
    // set (Templates/aonik-admin-starterkit/kit/icons-extra.jsx).
    cart: '<circle cx="9" cy="19.5" r="1.5"/><circle cx="17" cy="19.5" r="1.5"/><path d="M3 4h2l2.4 12h10.2L21 8H6"/>',
    box: '<path d="M21 8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"/><path d="M3.3 7l8.7 5 8.7-5"/><path d="M12 22V12"/>',
    sliders: '<path d="M4 6h8M18 6h2M4 12h2M10 12h10M4 18h12M20 18h.01"/><circle cx="15" cy="6" r="2"/><circle cx="8" cy="12" r="2"/><circle cx="18" cy="18" r="2"/>',
    file: '<path d="M6 3h9l4 4v14H6z"/><path d="M14 3v5h5"/>',
    stack: '<path d="M12 3l9 5-9 5-9-5z"/><path d="M3 13l9 5 9-5"/>'
  };
  function AonikTemplateIcon({
    name,
    size = 18,
    color = "currentColor",
    className,
    style,
    ...props
  }) {
    const paths = getMenuIconPath(name) ?? ICON_PATHS[name];
    if (!paths) return null;
    return /* @__PURE__ */ jsx(
      "svg",
      {
        width: size,
        height: size,
        viewBox: "0 0 24 24",
        "data-icon": name,
        fill: "none",
        stroke: color,
        strokeWidth: "1.75",
        strokeLinecap: "round",
        strokeLinejoin: "round",
        className: cn("inline-block shrink-0 align-[-3px]", className),
        style: { flex: "none", display: "inline-block", verticalAlign: "-3px", ...style },
        dangerouslySetInnerHTML: { __html: paths },
        "aria-hidden": "true",
        ...props
      }
    );
  }
  return __toCommonJS(PublishingDesignEntry_exports);
})();
