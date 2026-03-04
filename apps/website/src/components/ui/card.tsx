import * as React from "react";

type CardProps = React.HTMLAttributes<HTMLDivElement>;

type CardHeaderProps = React.HTMLAttributes<HTMLDivElement>;

type CardTitleProps = React.HTMLAttributes<HTMLHeadingElement>;

type CardContentProps = React.HTMLAttributes<HTMLDivElement>;

export function Card({ className, ...props }: CardProps) {
  return (
    <div
      className={["rounded-2xl bg-white", className].filter(Boolean).join(" ")}
      {...props}
    />
  );
}

export function CardHeader({ className, ...props }: CardHeaderProps) {
  return (
    <div className={["px-6 pt-6", className].filter(Boolean).join(" ")} {...props} />
  );
}

export function CardTitle({ className, ...props }: CardTitleProps) {
  return (
    <h3
      className={["text-lg font-semibold text-neutral-900", className]
        .filter(Boolean)
        .join(" ")}
      {...props}
    />
  );
}

export function CardContent({ className, ...props }: CardContentProps) {
  return (
    <div
      className={["px-6 pb-6", className].filter(Boolean).join(" ")}
      {...props}
    />
  );
}
