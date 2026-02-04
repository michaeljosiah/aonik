import { useMemo } from "react";

interface StaticHtmlPageProps {
  html: string;
  selector?: string;
  wrapperClassName?: string;
}

export const StaticHtmlPage = ({ html, selector = "body", wrapperClassName }: StaticHtmlPageProps) => {
  const content = useMemo(() => {
    if (typeof window === "undefined") {
      return "";
    }

    const document = new DOMParser().parseFromString(html, "text/html");
    const element = document.querySelector(selector) ?? document.body;
    const root = element.cloneNode(true) as HTMLElement;

    const rewriteRelativePath = (value: string | null) => {
      if (!value) {
        return value;
      }

      if (value.startsWith("http") || value.startsWith("mailto:") || value.startsWith("tel:") || value.startsWith("#")) {
        return value;
      }

      if (value.includes(".html")) {
        const [pathPart, hashPart] = value.split("#");
        const normalizedBase = pathPart.replace(/^\//, "");
        const normalizedPath = normalizedBase === "index.html" ? "/" : `/${normalizedBase.replace(/\.html$/, "")}`;
        return hashPart ? `${normalizedPath}#${hashPart}` : normalizedPath;
      }

      if (value.startsWith("images/")) {
        return `/${value}`;
      }

      return value;
    };

    root.querySelectorAll<HTMLElement>("[src]").forEach((node) => {
      const src = rewriteRelativePath(node.getAttribute("src"));
      if (src) {
        node.setAttribute("src", src);
      }
    });

    root.querySelectorAll<HTMLElement>("[href]").forEach((node) => {
      const href = rewriteRelativePath(node.getAttribute("href"));
      if (href) {
        node.setAttribute("href", href);
      }
    });

    root.querySelectorAll<HTMLElement>("[data-img]").forEach((node) => {
      const dataImg = rewriteRelativePath(node.getAttribute("data-img"));
      if (dataImg) {
        node.setAttribute("data-img", dataImg);
      }
    });

    root.querySelectorAll<HTMLElement>("[style]").forEach((node) => {
      const styleValue = node.getAttribute("style");
      if (!styleValue) {
        return;
      }

      const updatedStyle = styleValue.replace(/url\((['"]?)images\//g, "url($1/images/");
      node.setAttribute("style", updatedStyle);
    });

    return root.outerHTML ?? "";
  }, [html, selector]);

  return <div className={wrapperClassName} dangerouslySetInnerHTML={{ __html: content }} />;
};
