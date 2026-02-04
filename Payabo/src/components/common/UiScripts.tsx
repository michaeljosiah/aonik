import { useEffect } from "react";
import { useLocation } from "react-router-dom";

const parseMq = (element: HTMLElement) => {
  const content = window.getComputedStyle(element, "::before").getPropertyValue("content");
  return content.replace(/"/g, "").replace(/'/g, "").split(", ")[0] ?? "desktop";
};

export const UiScripts = () => {
  const location = useLocation();

  useEffect(() => {
    const cleanupCallbacks: Array<() => void> = [];

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
      cleanupCallbacks.forEach((cleanup) => cleanup());
    };
  }, [location.pathname]);

  return null;
};
