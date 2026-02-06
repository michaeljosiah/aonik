import { useEffect } from "react";
import { useLocation } from "react-router-dom";

import $ from "jquery";
import intlTelInput from "intl-tel-input";
import { Tooltip } from "bootstrap";

import intlTelInputUtilsUrl from "intl-tel-input/build/js/utils.js?url";

let select2ImportPromise: Promise<void> | null = null;
let slickImportPromise: Promise<void> | null = null;

const ensureSelect2Loaded = () => {
  // Select2 is a UMD build that expects window.jQuery/window.$ at evaluation time.
  // In ESM builds (Vite), importing at module scope can run before we assign globals.
  select2ImportPromise ??= import("select2/dist/js/select2.js").then((module) => {
    // Vite may wrap UMD/CJS as an ESM module; in that case the default export
    // can be the Select2 factory (root, jQuery) => jQuery.
    const factory = (module as { default?: unknown }).default;
    if (typeof factory === "function") {
      (factory as (root: Window, jQuery: typeof $) => typeof $)(window, $);
    }
  });

  return select2ImportPromise;
};

const ensureSlickLoaded = () => {
  slickImportPromise ??= import("slick-carousel/slick/slick.js").then((module) => {
    const factory = (module as { default?: unknown }).default;
    if (typeof factory === "function") {
      (factory as (root: Window, jQuery: typeof $) => typeof $)(window, $);
    }
  });
  return slickImportPromise;
};

const parseMq = (element: HTMLElement) => {
  const content = window.getComputedStyle(element, "::before").getPropertyValue("content");
  return content.replace(/"/g, "").replace(/'/g, "").split(", ")[0] ?? "desktop";
};

export const UiScripts = () => {
  const location = useLocation();

  useEffect(() => {
    const cleanupCallbacks: Array<() => void> = [];

    window.$ = $;
    window.jQuery = $;

    let cancelled = false;

    const handleScroll = () => {
      const scrollPosition = window.scrollY;
      document.querySelectorAll<HTMLElement>(".header, .header-top").forEach((element) => {
        element.classList.toggle("sticky", scrollPosition >= 90);
      });

      document.querySelectorAll<HTMLElement>(".navcolumn-sticky").forEach((element) => {
        const offsetTop = element.offsetTop;
        element.classList.toggle("fixed", scrollPosition > offsetTop);
      });
    };

    window.addEventListener("scroll", handleScroll, { passive: true });
    handleScroll();
    cleanupCallbacks.push(() => window.removeEventListener("scroll", handleScroll));

    const otpInputs = Array.from(document.querySelectorAll<HTMLInputElement>("#otp > *[id]"));
    otpInputs.forEach((input, index) => {
      const handleOtpKeydown = (event: KeyboardEvent) => {
        if (event.key === "Backspace") {
          input.value = "";
          if (index !== 0) {
            otpInputs[index - 1]?.focus();
          }
          return;
        }

        if (event.key.length === 1 && /[0-9a-zA-Z]/.test(event.key)) {
          input.value = event.key;
          if (index !== otpInputs.length - 1) {
            otpInputs[index + 1]?.focus();
          }
          event.preventDefault();
        }
      };

      input.addEventListener("keydown", handleOtpKeydown);
      cleanupCallbacks.push(() => input.removeEventListener("keydown", handleOtpKeydown));
    });

    const updateInputNotEmpty = (input: HTMLInputElement | HTMLTextAreaElement) => {
      if (input.value) {
        input.classList.add("not-empty");
      } else {
        input.classList.remove("not-empty");
      }
    };

    const formControls = Array.from(
      document.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>(".form .form-control")
    );
    formControls.forEach((control) => {
      const handleBlur = () => updateInputNotEmpty(control);
      control.addEventListener("blur", handleBlur);
      updateInputNotEmpty(control);
      cleanupCallbacks.push(() => control.removeEventListener("blur", handleBlur));
    });

    const formCompleteBlocks = Array.from(document.querySelectorAll<HTMLElement>(".form-complete"));
    formCompleteBlocks.forEach((block) => {
      const updateCompletion = () => {
        const fields = Array.from(block.querySelectorAll<HTMLInputElement | HTMLTextAreaElement>(
          ".form_field, .form-control"
        ));
        const hasEmptyField = fields.some((field) => !field.value);
        block.classList.toggle("is-incomplete", hasEmptyField);
        if (!hasEmptyField) {
          block.querySelectorAll<HTMLElement>(".btn, .bullet").forEach((element) => {
            element.classList.remove("disabled");
          });
        }
      };

      block.addEventListener("keydown", updateCompletion);
      block.addEventListener("input", updateCompletion);
      updateCompletion();
      cleanupCallbacks.push(() => {
        block.removeEventListener("keydown", updateCompletion);
        block.removeEventListener("input", updateCompletion);
      });
    });

    const switchInput = document.getElementById("switch") as HTMLInputElement | null;
    if (switchInput) {
      const handleSwitchChange = () => {
        document.querySelectorAll<HTMLElement>(".switch-content").forEach((element) => {
          element.classList.toggle("disabled", !switchInput.checked);
        });
      };
      switchInput.addEventListener("change", handleSwitchChange);
      handleSwitchChange();
      cleanupCallbacks.push(() => switchInput.removeEventListener("change", handleSwitchChange));
    }

    const handleTogglePassword = (event: Event) => {
      const target = event.target as HTMLElement | null;
      const toggle = target?.closest<HTMLElement>(".toggle-password");
      if (!toggle) {
        return;
      }

      toggle.classList.toggle("icon-eye-slash");
      const selector = toggle.getAttribute("toggle");
      if (!selector) {
        return;
      }
      const input = document.querySelector<HTMLInputElement>(selector);
      if (!input) {
        return;
      }
      input.type = input.type === "password" ? "text" : "password";
    };

    document.addEventListener("click", handleTogglePassword);
    cleanupCallbacks.push(() => document.removeEventListener("click", handleTogglePassword));

    const handleSmoothScroll = (event: Event) => {
      const target = event.target as HTMLElement | null;
      const link = target?.closest<HTMLAnchorElement>("a.scroll-down");
      if (!link) {
        return;
      }
      const href = link.getAttribute("href");
      if (!href || !href.startsWith("#")) {
        return;
      }
      const targetElement = document.querySelector<HTMLElement>(href);
      if (!targetElement) {
        return;
      }
      event.preventDefault();
      targetElement.scrollIntoView({ behavior: "smooth", block: "start" });
    };

    document.addEventListener("click", handleSmoothScroll);
    cleanupCallbacks.push(() => document.removeEventListener("click", handleSmoothScroll));

    const scrollToHash = (hashValue: string) => {
      if (!hashValue || hashValue.length <= 1) {
        return;
      }

      const decodedHash = decodeURIComponent(hashValue);
      let attempts = 0;
      const maxAttempts = 30;

      const tryScroll = () => {
        const targetElement = document.querySelector<HTMLElement>(decodedHash);
        if (targetElement) {
          targetElement.scrollIntoView({ behavior: "smooth", block: "start" });
          return;
        }

        attempts += 1;
        if (attempts < maxAttempts) {
          window.requestAnimationFrame(tryScroll);
        }
      };

      // Delay until after the current paint so static HTML is in the DOM.
      window.setTimeout(() => tryScroll(), 0);
    };

    scrollToHash(window.location.hash);

    const tooltipInstances: Tooltip[] = [];
    document.querySelectorAll<HTMLElement>("[data-bs-toggle='tooltip']").forEach((element) => {
      tooltipInstances.push(new Tooltip(element));
    });
    cleanupCallbacks.push(() => {
      tooltipInstances.forEach((tooltip) => tooltip.dispose());
    });

    // Bootstrap dropdowns can throw if a toggle exists without a corresponding menu.
    // Guard those clicks so the app doesn't crash.
    const handleBootstrapDropdownClick = (event: MouseEvent) => {
      const target = event.target as HTMLElement | null;
      const toggle = target?.closest<HTMLElement>("[data-bs-toggle='dropdown']");
      if (!toggle) {
        return;
      }

      const container =
        toggle.closest<HTMLElement>(".dropdown, .btn-group, .dropup, .dropend, .dropstart") ??
        toggle.parentElement;

      const menu = container?.querySelector<HTMLElement>(".dropdown-menu");
      if (!menu) {
        event.preventDefault();
        event.stopPropagation();
      }
    };

    document.addEventListener("click", handleBootstrapDropdownClick, true);
    cleanupCallbacks.push(() => document.removeEventListener("click", handleBootstrapDropdownClick, true));

    const selectElements: HTMLElement[] = [];

    const formatCountry = (item: { id: string; element?: HTMLOptionElement; text: string }) => {
      if (!item.id) {
        return item.text;
      }

      const countryCode = item.element?.value?.toLowerCase() ?? "";
      const img = $("<img>", {
        class: "rounded-circle me-3",
        width: 32,
        src: `/images/flags/${countryCode}.svg`
      });
      const span = $("<span>", { text: ` ${item.text}` });
      span.prepend(img);
      return span;
    };

    const optionFormat = (item: { id: string; element?: HTMLOptionElement; text: string }) => {
      if (!item.id) {
        return item.text;
      }

      const imgUrl = item.element?.getAttribute("data-img") ?? "";
      const span = document.createElement("span");
      span.innerHTML = `<img src="${imgUrl}" class="rounded-circle me-3" alt="img"/>${item.text}`;
      return $(span);
    };

    const optionFormatAlt = (item: { id: string; element?: HTMLOptionElement; text: string }) => {
      if (!item.id) {
        return item.text;
      }

      const imgUrl = item.element?.getAttribute("data-img") ?? "";
      const span = document.createElement("span");
      span.innerHTML = `<img src="${imgUrl}" class="me-3" alt="img"/>${item.text}`;
      return $(span);
    };

    const initSelect2 = (force = false) => {
      $(".select-box").each((_, element) => {
      const $element = $(element);
      if ($element.data("select2")) {
        if (!force) {
          return;
        }
        $element.select2("destroy");
      }
      $element.select2({ width: "100%", minimumResultsForSearch: -1 });
      selectElements.push(element);
      });

      $(".countries").each((_, element) => {
      const $element = $(element);
      if ($element.data("select2")) {
        if (!force) {
          return;
        }
        $element.select2("destroy");
      }
      $element.select2({
        width: "100%",
        templateSelection: formatCountry,
        templateResult: formatCountry
      });
      $element.off("select2:select.payaboReactSync select2:clear.payaboReactSync");
      $element.on("select2:select.payaboReactSync select2:clear.payaboReactSync", () => {
        const nativeElement = $element.get(0);
        if (nativeElement) {
          nativeElement.dispatchEvent(new Event("change", { bubbles: true }));
        }
      });
      selectElements.push(element);
      });

      $("#categories").each((_, element) => {
      const $element = $(element);
      if ($element.data("select2")) {
        if (!force) {
          return;
        }
        $element.select2("destroy");
      }
      $element.select2({
        width: "100%",
        templateSelection: optionFormat,
        templateResult: optionFormat,
        minimumResultsForSearch: -1
      });
      selectElements.push(element);
      });

      $(".categories").each((_, element) => {
      const $element = $(element);
      if ($element.data("select2")) {
        if (!force) {
          return;
        }
        $element.select2("destroy");
      }
      $element.select2({
        width: "100%",
        templateSelection: optionFormatAlt,
        templateResult: optionFormatAlt,
        minimumResultsForSearch: -1
      });
      selectElements.push(element);
      });
    };

    cleanupCallbacks.push(() => {
      selectElements.forEach((element) => {
        const $element = $(element);
        if ($element.data("select2")) {
          $element.select2("destroy");
        }
      });
    });

    const slickElements: HTMLElement[] = [];

    const initSlick = () => {
      $(".card-slider").each((_, element) => {
      const $element = $(element);
      if ($element.hasClass("slick-initialized")) {
        return;
      }
      $element
        .slick({
          autoplay: false,
          infinite: true,
          speed: 500,
          slidesToShow: 2,
          slidesToScroll: 2,
          dots: true,
          arrows: true,
          prevArrow:
            '<svg class="slick-prev" width="17" height="26" viewBox="0 0 17 26" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M12.7283 25.4555L16.9709 21.2129L4.24303 8.48497L0.000384808 12.7276L12.7283 25.4555Z" fill="currentColor"/><path d="M0.000279307 12.7281L4.24292 16.9707L16.9708 4.24278L12.7282 0.000140667L0.000279307 12.7281Z" fill="currentColor"/></svg>',
          nextArrow:
            '<svg class="slick-next" width="17" height="26" viewBox="0 0 17 26" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M16.9709 12.729L12.7283 8.48633L0.000349641 21.2142L4.24299 25.4569L16.9709 12.729Z" fill="currentColor"/><path d="M4.24313 0.00150001L0.000488281 4.24414L12.7284 16.9721L16.9711 12.7294L4.24313 0.00150001Z" fill="currentColor"/></svg>',
          responsive: [
            { breakpoint: 1199, settings: { slidesToShow: 2, slidesToScroll: 2, arrows: true, dots: true } },
            { breakpoint: 991, settings: { slidesToShow: 2, slidesToScroll: 2, arrows: true, dots: true } },
            { breakpoint: 767, settings: { slidesToShow: 1, slidesToScroll: 1, arrows: true, dots: true, autoplay: false } }
          ]
        })
        .on("setPosition", function () {
          const $slider = $(this);
          $slider.find(".slick-slide").height("auto");
          const slickTrackHeight = $slider.find(".slick-track").height() ?? 0;
          $slider.find(".slick-slide").css("height", `${slickTrackHeight}px`);
        });
      slickElements.push(element);
      });

      $(".profile-slider").each((_, element) => {
      const $element = $(element);
      if ($element.hasClass("slick-initialized")) {
        return;
      }
      $element
        .slick({
          autoplay: false,
          infinite: true,
          speed: 500,
          slidesToShow: 3,
          slidesToScroll: 3,
          dots: true,
          arrows: true,
          prevArrow:
            '<svg class="slick-prev" width="17" height="26" viewBox="0 0 17 26" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M12.7283 25.4555L16.9709 21.2129L4.24303 8.48497L0.000384808 12.7276L12.7283 25.4555Z" fill="currentColor"/><path d="M0.000279307 12.7281L4.24292 16.9707L16.9708 4.24278L12.7282 0.000140667L0.000279307 12.7281Z" fill="currentColor"/></svg>',
          nextArrow:
            '<svg class="slick-next" width="17" height="26" viewBox="0 0 17 26" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M16.9709 12.729L12.7283 8.48633L0.000349641 21.2142L4.24299 25.4569L16.9709 12.729Z" fill="currentColor"/><path d="M4.24313 0.00150001L0.000488281 4.24414L12.7284 16.9721L16.9711 12.7294L4.24313 0.00150001Z" fill="currentColor"/></svg>',
          responsive: [
            { breakpoint: 1199, settings: { slidesToShow: 2, slidesToScroll: 2, arrows: true, dots: true } },
            { breakpoint: 991, settings: { slidesToShow: 2, slidesToScroll: 2, arrows: true, dots: true } },
            { breakpoint: 767, settings: { slidesToShow: 1, slidesToScroll: 1, arrows: false, dots: true, autoplay: false } }
          ]
        })
        .on("setPosition", function () {
          const $slider = $(this);
          $slider.find(".slick-slide").height("auto");
          const slickTrackHeight = $slider.find(".slick-track").height() ?? 0;
          $slider.find(".slick-slide").css("height", `${slickTrackHeight}px`);
        });
      slickElements.push(element);
      });
    };

    const initJQueryPlugins = async () => {
      try {
        await Promise.all([ensureSelect2Loaded(), ensureSlickLoaded()]);
      } catch {
        // If plugin bundles fail to load, keep the app usable.
        return;
      }

      if (cancelled) {
        return;
      }

      try {
        if (typeof ($.fn as unknown as { select2?: unknown }).select2 === "function") {
          initSelect2();
        }

        if (typeof ($.fn as unknown as { slick?: unknown }).slick === "function") {
          initSlick();
        }
      } catch {
        // Swallow plugin init issues; these scripts are progressive enhancement.
      }
    };

    const refreshSelects = async () => {
      try {
        await ensureSelect2Loaded();
      } catch {
        return;
      }

      if (cancelled) {
        return;
      }

      try {
        if (typeof ($.fn as unknown as { select2?: unknown }).select2 === "function") {
          initSelect2(true);
        }
      } catch {
        // ignore refresh errors
      }
    };

    const handleRefreshSelects = () => {
      void refreshSelects();
    };

    window.addEventListener("payabo:refresh-selects", handleRefreshSelects);
    cleanupCallbacks.push(() => window.removeEventListener("payabo:refresh-selects", handleRefreshSelects));

    void initJQueryPlugins();

    cleanupCallbacks.push(() => {
      slickElements.forEach((element) => {
        const $element = $(element);
        if ($element.hasClass("slick-initialized")) {
          $element.slick("unslick");
        }
      });
    });

    const telInputElement = document.querySelector<HTMLInputElement>("#phone");
    let telInputInstance: ReturnType<typeof intlTelInput> | null = null;
    if (telInputElement) {
      telInputInstance = intlTelInput(telInputElement, {
        excludeCountries: ["us"],
        separateDialCode: true,
        utilsScript: intlTelInputUtilsUrl
      });
    }
    cleanupCallbacks.push(() => {
      telInputInstance?.destroy();
    });

    const setupMorphDropdown = (element: HTMLElement) => {
      const mainNavigation = element.querySelector<HTMLElement>(".main-nav");
      const dropdownList = element.querySelector<HTMLElement>(".dropdown-list");
      const dropdownBg = dropdownList?.querySelector<HTMLElement>(".bg-layer");
      const navTrigger = element.querySelector<HTMLElement>(".nav-trigger");
      const items = Array.from(mainNavigation?.querySelectorAll<HTMLElement>(".has-dropdown") ?? []);

      const updateDropdown = (dropdownItem: HTMLElement, height: number, width: number, left: number) => {
        if (!dropdownList || !dropdownBg) {
          return;
        }

        dropdownList.style.transform = `translateX(${left}px)`;
        dropdownList.style.width = `${width}px`;
        dropdownList.style.height = `${height}px`;
        dropdownBg.style.transform = `scaleX(${width}) scaleY(${height})`;
      };

      const showDropdown = (item: HTMLElement) => {
        if (parseMq(element) !== "desktop" || !dropdownList) {
          return;
        }

        const dropdownId = item.getAttribute("data-content");
        if (!dropdownId) {
          return;
        }

        const selectedDropdown = dropdownList.querySelector<HTMLElement>(`#${dropdownId}`);
        if (!selectedDropdown) {
          return;
        }

        const dropdownContent = selectedDropdown.querySelector<HTMLElement>(".content");
        const dropdownRect = selectedDropdown.getBoundingClientRect();
        const contentRect = dropdownContent?.getBoundingClientRect();
        const itemRect = item.getBoundingClientRect();
        const width = contentRect?.width ?? dropdownRect.width;
        const height = dropdownRect.height;
        const left = itemRect.left + itemRect.width / 2 - width / 2 + window.scrollX;

        updateDropdown(selectedDropdown, height, width, Math.round(left));

        element.querySelectorAll<HTMLElement>(".active").forEach((node) => node.classList.remove("active"));
        element.querySelectorAll<HTMLElement>(".move-left").forEach((node) => node.classList.remove("move-left"));
        element.querySelectorAll<HTMLElement>(".move-right").forEach((node) => node.classList.remove("move-right"));

        selectedDropdown.classList.add("active");
        item.classList.add("active");

        let sibling = selectedDropdown.previousElementSibling as HTMLElement | null;
        while (sibling) {
          sibling.classList.add("move-left");
          sibling = sibling.previousElementSibling as HTMLElement | null;
        }
        sibling = selectedDropdown.nextElementSibling as HTMLElement | null;
        while (sibling) {
          sibling.classList.add("move-right");
          sibling = sibling.nextElementSibling as HTMLElement | null;
        }

        if (!element.classList.contains("is-dropdown-visible")) {
          window.setTimeout(() => {
            element.classList.add("is-dropdown-visible");
          }, 10);
        }
      };

      const hideDropdown = () => {
        if (parseMq(element) !== "desktop") {
          return;
        }
        element.classList.remove("is-dropdown-visible");
        element.querySelectorAll<HTMLElement>(".active").forEach((node) => node.classList.remove("active"));
        element.querySelectorAll<HTMLElement>(".move-left").forEach((node) => node.classList.remove("move-left"));
        element.querySelectorAll<HTMLElement>(".move-right").forEach((node) => node.classList.remove("move-right"));
      };

      const resetDropdown = () => {
        if (parseMq(element) === "mobile") {
          dropdownList?.removeAttribute("style");
        }
      };

      const handleNavTriggerClick = (event: Event) => {
        event.preventDefault();
        element.classList.toggle("nav-open");
      };

      const handleResize = () => {
        resetDropdown();
      };

      const cleanupListeners: Array<() => void> = [];

      items.forEach((item) => {
        const handleMouseEnter = () => showDropdown(item);
        const handleMouseLeave = () => {
          window.setTimeout(() => {
            const isHoveringNav = Boolean(element.querySelector(".has-dropdown:hover"));
            const isHoveringDropdown = Boolean(element.querySelector(".dropdown-list:hover"));
            if (!isHoveringNav && !isHoveringDropdown) {
              hideDropdown();
            }
          }, 50);
        };

        const handleTouchStart = (event: TouchEvent) => {
          const dropdownId = item.getAttribute("data-content");
          if (!dropdownId || !dropdownList) {
            return;
          }
          const selectedDropdown = dropdownList.querySelector<HTMLElement>(`#${dropdownId}`);
          if (!element.classList.contains("is-dropdown-visible") || !selectedDropdown?.classList.contains("active")) {
            event.preventDefault();
            showDropdown(item);
          }
        };

        item.addEventListener("mouseenter", handleMouseEnter);
        item.addEventListener("mouseleave", handleMouseLeave);
        item.addEventListener("touchstart", handleTouchStart);
        cleanupListeners.push(() => item.removeEventListener("mouseenter", handleMouseEnter));
        cleanupListeners.push(() => item.removeEventListener("mouseleave", handleMouseLeave));
        cleanupListeners.push(() => item.removeEventListener("touchstart", handleTouchStart));
      });

      if (dropdownList) {
        const handleDropdownLeave = () => {
          window.setTimeout(() => {
            const isHoveringNav = Boolean(element.querySelector(".has-dropdown:hover"));
            const isHoveringDropdown = Boolean(element.querySelector(".dropdown-list:hover"));
            if (!isHoveringNav && !isHoveringDropdown) {
              hideDropdown();
            }
          }, 50);
        };

        dropdownList.addEventListener("mouseleave", handleDropdownLeave);
        cleanupListeners.push(() => dropdownList.removeEventListener("mouseleave", handleDropdownLeave));
      }

      if (navTrigger) {
        navTrigger.addEventListener("click", handleNavTriggerClick);
        cleanupListeners.push(() => navTrigger.removeEventListener("click", handleNavTriggerClick));
      }

      window.addEventListener("resize", handleResize);
      cleanupListeners.push(() => window.removeEventListener("resize", handleResize));

      return () => {
        cleanupListeners.forEach((cleanup) => cleanup());
      };
    };

    const morphDropdowns = Array.from(document.querySelectorAll<HTMLElement>(".cd-morph-dropdown"));
    const morphCleanups = morphDropdowns.map((element) => setupMorphDropdown(element)).filter(Boolean) as Array<() => void>;
    cleanupCallbacks.push(() => {
      morphCleanups.forEach((cleanup) => cleanup());
    });

    return () => {
      cancelled = true;
      cleanupCallbacks.forEach((cleanup) => cleanup());
    };
  }, [location.pathname, location.hash]);

  return null;
};
